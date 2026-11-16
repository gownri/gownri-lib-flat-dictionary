#define THROW_IF_DOUBLE_REMOVE
using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
namespace FlatDictionary.InternalModules.Atomic;

internal class RemoveMarkNodeMock(int size)
{
    struct TKey;// dummy
    [Flags]
    public enum State : int
    {
        None = 0,
        Disposed = 1 << 0,
    }

    private const int NodeEnd = int.MaxValue;
    private const int RemoveLockFlag = int.MinValue;
    private const int RangeStartFlag = int.MinValue;
    private const int StolenNodeMark = -1;
    public (int, int Length) keys = (default, size);// dummy
    public (int Count, int dummy) countAndVersion = (size, default); // dummy
    public int[] hashCodes = new int[size];
    public int freeNodeLinkHead = NodeEnd;
    public int removeMarkedCount = 0;
    public State state;


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool WaitWhileRemoveLock(scoped ref int counter, scoped out int newCounterComparand)
    {
        int comparand = counter;
        while (
            (comparand &= ~RemoveLockFlag)
            != (comparand = Interlocked.CompareExchange(
                ref counter,
                comparand | RemoveLockFlag,
                comparand
            ))
        )
            Thread.Yield();
        newCounterComparand = comparand;
        return true;
    }

    public bool MarkingLazyRemove(int index)
    {
        if ((uint)index >= (uint)Volatile.Read(ref this.countAndVersion.Count))
            return false;
        var hashCodes = this.hashCodes;

        scoped ref var slot = ref Unsafe.NullRef<int>();
        scoped Span<int> freeNodes;
        if ((int)index < (int)hashCodes.Length)
        {
            slot = ref hashCodes[index];
            freeNodes = hashCodes.AsSpan(0, index);
        }
        else
        {
            hashCodes = TryAllocate(hashCodes);
            slot = ref hashCodes[index];
            freeNodes = hashCodes.AsSpan(0, index);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        int[] TryAllocate(int[] comparand)
        {
            if (typeof(TKey) != typeof(int))
                throw new ArrayTypeMismatchException();
            var newArray = new int[this.keys.Length];// only use for marking. unnecessary to copy
            var original = Interlocked.CompareExchange(ref this.hashCodes, newArray, comparand);
            return object.ReferenceEquals(original, comparand) ? newArray : original;
        }

        while (true)
        {
            scoped ref var prevNode = ref this.freeNodeLinkHead;
            var nextLink = prevNode;
            var prevNodeComparand = int.MaxValue;// exclude head from range merging
            var hasRangeFlag = default(int);
            scoped ref int rangeStartRef = ref hasRangeFlag;// dummy init

#if DEBUG
            var safe = 0;
#endif
            while (true)
            {
#if DEBUG
                if (safe++ > hashCodes.Length)
                    throw new Exception();
#endif
                if ((uint)nextLink < (uint)freeNodes.Length)
                {
                    if (Unsafe.ByteOffset(ref prevNode, ref freeNodes[nextLink]) != (IntPtr)(uint)sizeof(int))
                        rangeStartRef = ref hasRangeFlag;// dummy
                    else if (Unsafe.AreSame(ref rangeStartRef, ref hasRangeFlag))
                        rangeStartRef = ref prevNode;
                    prevNodeComparand = nextLink;
                    nextLink = prevNode = ref freeNodes[nextLink];
                }
                else// end || hasRangeFlag || already marked
                {
                    prevNodeComparand = nextLink;
                    hasRangeFlag = nextLink ^ (nextLink &= ~RangeStartFlag);
                    if (hasRangeFlag != default)
                    {
                        if ((uint)nextLink >= (uint)freeNodes.Length)// was already within range of marked
#if THROW_IF_DOUBLE_REMOVE
                            DoThrowDoubleRemoved();
#else
                            return false;
#endif
                        if (Unsafe.AreSame(ref rangeStartRef, ref hasRangeFlag))
                            rangeStartRef = ref prevNode;
                        prevNodeComparand = nextLink;
                        nextLink = prevNode = ref freeNodes[nextLink];
                        continue;
                    }

                    //nextLink is above index and has not rangeFlag
                    if (nextLink == freeNodes.Length)// index was already marked
#if THROW_IF_DOUBLE_REMOVE
                        DoThrowDoubleRemoved();
#else
                        return false;
#endif
                    break;
                }
            }
#if DEBUG
            System.Diagnostics.Debug.Assert(nextLink >= 0);
#endif

            scoped ref var rangeValue = ref Unsafe.NullRef<int>();
            scoped ref var slotValue = ref nextLink;
            if (Unsafe.ByteOffset(ref prevNode, ref slot) == (IntPtr)(uint)sizeof(int))// [prevNode + 1] == [slot]
            {
                if (Unsafe.AreSame(ref rangeStartRef, ref hasRangeFlag))
                    rangeStartRef = ref prevNode;
                (rangeValue = ref index) |= RangeStartFlag;
            }
            else
            {
                (rangeValue = ref hasRangeFlag)// borrowing local variable
                    = unchecked(
                        (int)((nint)Unsafe.ByteOffset(
                            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(freeNodes),
                            ref prevNode
                        ) / (nint)(uint)sizeof(int))
                    ) | RangeStartFlag;
            }

            do// fastRetriable
            {
#if DEBUG
                if (safe++ > hashCodes.Length)
                    throw new Exception();
#endif
                if (freeNodes.Length + 1 == nextLink
                && (uint)nextLink < (uint)hashCodes.Length)
                {
                    nextLink = slotValue = ref hashCodes[nextLink];
                    if ((uint)nextLink < unchecked((uint)RangeStartFlag))// has not flag
                        (slotValue = ref nextLink) = freeNodes.Length + 1 | RangeStartFlag;
                    else if (Unsafe.AreSame(ref rangeValue, ref index))
                        rangeValue = ref slotValue;
                }
#if THROW_IF_DOUBLE_REMOVE
                Interlocked.Increment(ref this.removeMarkedCount);
                slot = slotValue;
                if (prevNodeComparand
                        != (prevNodeComparand = Interlocked.CompareExchange(
                            ref prevNode,
                            freeNodes.Length,
                            prevNodeComparand
                        )))
                {
                    if ((nextLink = prevNodeComparand) > freeNodes.Length)
                        continue;// fast retry
                    else
                        break;// restart
                }
                rangeStartRef = rangeValue | RangeStartFlag;
                return (this.state & State.Disposed) == default;
#else
                var comparand = Volatile.Read(ref this.removeMarkedCount) & ~RemoveLockFlag;
                if (comparand == Interlocked.CompareExchange(ref this.removeMarkedCount, comparand | RemoveLockFlag, comparand)
                || WaitWhileRemoveLock(ref this.removeMarkedCount, out comparand))
                {
                    if (prevNodeComparand != (prevNodeComparand = Volatile.Read(ref prevNode)))
                    {
                        Volatile.Write(ref this.removeMarkedCount, comparand);
                        if ((nextLink = prevNodeComparand) > freeNodes.Length)
                            continue;// fast retry
                        else
                            break;// restart
                    }
                    else
                    {
#if DEBUG
                        System.Diagnostics.Debug.Assert((slotValue & ~RangeStartFlag) > freeNodes.Length);
                        System.Diagnostics.Debug.Assert(
                            (rangeValue & ~RangeStartFlag) 
                            >= unchecked((int)((nint)Unsafe.ByteOffset(
                                ref System.Runtime.InteropServices.MemoryMarshal.GetReference(freeNodes),
                                ref prevNode
                            ) / (nint)(uint)sizeof(int)))
                        );
#endif
                        slot = slotValue;
                        Volatile.Write(ref prevNode, freeNodes.Length);
                        rangeStartRef = rangeValue | RangeStartFlag;
                        Volatile.Write(ref this.removeMarkedCount, comparand + 1);
                        return (this.state & State.Disposed) == default;
                    }
                }
#endif

            } while (true);// end fastRetriable

            // restart arrangement
            index = freeNodes.Length;
            slot = ref hashCodes[index];
            Interlocked.MemoryBarrier();
        }
        static bool DoThrowDoubleRemoved()
            => throw new InvalidOperationException("The specified index has already been marked for removal.");
    }
    public int CountUp()
    {
        var index = this.freeNodeLinkHead;
        var list = this.hashCodes;
        var prev = index;
        var aggregate = 0;
        while (true)
        {
            if((uint)index < (uint)list.Length)
            {
                aggregate++;
                prev = index;
                index = list[index];
            }
            else
            {
                index ^= RangeStartFlag;
                if ((uint)index < (uint)list.Length)
                {
                    aggregate += index - prev;
                    prev = index;
                    index = list[index];
                }
                else
                {
                    return aggregate;
                }

            }
        }
    }

    static class TSRandom
    {
        [ThreadStatic]
        private static Random? r;
        public static Random Shared 
            => r ??= new Random();
    }

    public void Do(
#if THROW_IF_DOUBLE_REMOVE
        int divider = 2
#else
        int multiplier = 1
#endif
        )
    {
        var ss = new SortedSet<int>();
        this.freeNodeLinkHead = NodeEnd;
        this.removeMarkedCount = 0;
        var size = this.hashCodes.Length;

#if THROW_IF_DOUBLE_REMOVE
        var pool = System.Buffers.ArrayPool<int>.Shared.Rent(size);
        var span = pool.AsSpan(0, size);
        for (var i = 0; i < span.Length; ++i)
            span[i] = i;
        Array.Sort(pool, 0, size, Comparer<int>.Create((_, _) => TSRandom.Shared.Next(-1, 2)));
        span = span.Slice(0, size / divider);
        for (var i = 0; i < span.Length; ++i)
        {
            var index = span[i];
            ss.Add(index);
            this.MarkingLazyRemove(index);
        }
        System.Buffers.ArrayPool<int>.Shared.Return(pool);
#else
        for (var i = 0; i < size * multiplier; ++i)
        {
            var index = TSRandom.Shared.Next(size);
            ss.Add(index);
            this.MarkingLazyRemove(index);
        }
#endif
        Console.WriteLine(this.ToString());
        Console.WriteLine(this.CountUp());

        this.Check(ss);
    }


    public void DoParallel(
#if THROW_IF_DOUBLE_REMOVE
        int divider = 2
#else
        int multiplier = 1
#endif
        )
    {
        var ss = new System.Collections.Concurrent.ConcurrentDictionary<int, ValueTuple>();
        this.freeNodeLinkHead = NodeEnd;
        this.removeMarkedCount = 0;
        var size = this.hashCodes.Length;
#if THROW_IF_DOUBLE_REMOVE
        var pool = System.Buffers.ArrayPool<int>.Shared.Rent(size);
        var span = pool.AsSpan(0, size);
        for(var i = 0; i < span.Length; ++i)
            span[i] = i;
        Array.Sort(pool, 0, size, Comparer<int>.Create((_, _) => TSRandom.Shared.Next(-1, 2)));
        System.Threading.Tasks.Parallel.For(0, size / divider, (i) =>
        {
            var index = pool[i];
            ss.TryAdd(index, default);
            this.MarkingLazyRemove(index);
        });
        System.Buffers.ArrayPool<int>.Shared.Return(pool);
#else
        System.Threading.Tasks.Parallel.For(0, size * multiplier, (i) =>
        {
            var index = TSRandom.Shared.Next(size);
            ss.TryAdd(index, default);
            this.MarkingLazyRemove(index);
        });
#endif
        Console.WriteLine(this.CountUp());
        Console.WriteLine(this.ToString());

        this.Check(System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.Select(ss, pair => pair.Key), v => v));
    }

    private void Check(IEnumerable<int> sorted)
    {
        var index = this.freeNodeLinkHead;
        var list = this.hashCodes;
        var prev = index;
        Console.Write("{");
        foreach (var v in sorted)
        {
            Console.Write(v);
            Console.Write(", ");
        }
        Console.WriteLine("}");
        foreach (var min in sorted)
        {

            if((uint)index < (uint)list.Length)
            {
                if (index != min)
                    Console.WriteLine($"{index} != {min} : step");
                prev = index;
                index = list[index];
            }
            else
            {
                index ^= RangeStartFlag;
                if ((uint)index < (uint)list.Length)
                {
                    var temp = list[index] & ~RangeStartFlag;
                    prev++;
                    if (min > index)
                        Console.WriteLine($"{prev} != {min} : in range");
                    if (min == index)
                        index = temp;
                    else
                        index ^= RangeStartFlag;
                }
                else
                {
                    Console.WriteLine($"{index} != {min} : node end");
                }
            }

        }

        Console.WriteLine("end");
    }
    System.Text.StringBuilder? builder;
    public override string ToString()
    {
        var b = this.builder ??= new();
        var list = this.hashCodes;
        b.Append("markedCount:").Append(this.removeMarkedCount)
        .AppendLine()
        .Append(", head:[").Append(this.freeNodeLinkHead).Append(']')
        .Append(" {");
        foreach (var v in this.hashCodes)
            b.Append((uint)(v & ~RangeStartFlag) < (uint)list.Length
                ? v & ~RangeStartFlag
                : "e"
            ).Append((uint)v > (uint)int.MaxValue ? "w" : "").Append(", ");
        b.AppendLine("} ");
        var ret = b.ToString();
        b.Clear();
        return ret;
    }
}


