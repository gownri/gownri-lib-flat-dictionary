//using HashIndex.InternalModules.Atomic;
//using System;
//using System.Linq;
//using System.Collections.Generic;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Threading;
//using static System.Net.Mime.MediaTypeNames;
//namespace HashIndex.InternalModules;

//public readonly struct ReadRefLocal<T> 
//{
//    private readonly StealableLocal<T>? localRef;
//    internal readonly bool IsNull
//        => this.localRef is null;
//    internal ReadRefLocal(StealableLocal<T> stealableLocal)
//        => this.localRef = stealableLocal;
//}

//public readonly struct EnumResult<T>
//{
//    internal readonly StealableLocal<T> item;

//}

//internal sealed class StealableLocal<T>
//{

//    // static
//    private const int defaultLiveNode = -1;
//    private const int unused = int.MaxValue;

    
//    private static int primarySize { get; } = Environment.ProcessorCount;
//    private static int gen2Size { get; } = primarySize * 2;
//    private static int hazard { get; } = unchecked(int.MaxValue + primarySize);

//    private static StealableLocal<T>[] primaryArray =
//        Enumerable
//        .Range(0, primarySize)
//        .Select(static i => new StealableLocal<T>(i + 1))
//        .ToArray();
//    private static StealableLocal<T>[] gen2List =
//        Enumerable
//        .Range(0, primarySize)
//        .Select(static i => new StealableLocal<T>(i + primarySize + 1))
//        .ToArray();

//    private const long versionMask = unchecked((long)uint.MaxValue << sizeof(int));
//    private const long versionIncrement = 1L << sizeof(int) + 1;

//    private const long changedMark = 1L << sizeof(int);
//    private static long freeNodeIndex = 0;
//    private static int gen2FreeNode = primarySize;
//    private static long gen2ListCount;
//    // instance

//    private const int contextOffset = sizeof(int);
//    private const ulong lockFlag = unchecked((ulong)(uint)int.MinValue);
//    private const long hazardLink = ~0L;

//    //private MetaTable Table { get; set; }
//    //private int Position { get; set; }
//    //private Meta Owner { get; set; }
//    private T? Item = default;
//    [property: System.Diagnostics.CodeAnalysis.MaybeNull()]
//    internal ref readonly T? Peek
//        => ref this.Item;


//    //private int slotPosition;
//    private ReaderOwnerConsistency slotToken;
//    private StealableLocal<T>? stolenLink;

//    private const int readerWaitReceptionHandle = 0;
//    private const int changeWaitHandle = 1;
//    private const int waitHandlesLength = 2;
//    private ManualResetEvent[]? waitHandles;

//    private StealableLocal(int nextFree)
//    {
//        this.slotToken = default;

//    }

//    private void Publish()
//    {
//    }
//    private void Close()
//    {
       
//    }
//    private static StealableLocal<T> Allocate(bool forceGen2 = false)
//    {
//        if (forceGen2)
//            return Gen2Allocate();
//        var arr = primaryArray;
//        while (true)
//        {
//            var comparand = Volatile.Read(ref freeNodeIndex);
//            var idx = (int)comparand;
//            if ((uint)idx >= (uint)arr.Length)
//                return Gen2Allocate();
//            else
//            {
//                var allocated = arr[idx];

//                var nextFree = (comparand & versionMask) | (long)(uint)allocated.slotToken.Index;
//                if (comparand == Interlocked.CompareExchange(ref freeNodeIndex, nextFree, comparand))
//                {
//#if DEBUG
//                    System.Diagnostics.Debug.Assert(allocated.slotToken.HasSomeone is false);
//#endif
//                    ReaderOwnerConsistency.WriteIndexOnly(ref allocated.slotToken, idx);
//                    return allocated;
//                }
//                continue;
//            }
//        }
//        static StealableLocal<T> Gen2Allocate()
//        {
//            var arr = gen2List;
//            do
//            {
//                var comparand = Volatile.Read(ref gen2ListCount);
//                var idx = (int)comparand;
//                if ((uint)idx < (uint)arr.Length)
//                {
//                    var allocated = arr[idx];
//                    if (comparand == Interlocked.CompareExchange(ref gen2ListCount, comparand + 1, comparand))
//                    {
//#if DEBUG
//                        System.Diagnostics.Debug.Assert(allocated.slotToken.HasSomeone is false);
//#endif
//                        ReaderOwnerConsistency.WriteIndexOnly(ref allocated.slotToken, idx);
//                        return allocated;
//                    }
//                }

//                Expand(arr, comparand);
//            } while (true);
//            static void Expand(StealableLocal<T>[] arr, long comparand)
//            {
//    #if DEBUG
//                System.Diagnostics.Debug.WriteLine($"grow: {arr.Length * 2}");
//    #endif
//                var idx = (int)comparand;
//                var newVersion = comparand + versionIncrement;
            
//                var newArr = new StealableLocal<T>[arr.Length * 2];
//                var rest = arr.Length;
//                while (true)
//                {
//                    if ((uint)rest >= (uint)newArr.Length)
//                        break;
//                    newArr[rest] = new StealableLocal<T>(rest + primarySize + 1);
//                    ++rest;
//                }
//                lock (arr)
//                {
//                    if (comparand != Volatile.Read(ref gen2ListCount)
//                    || object.ReferenceEquals(arr, Volatile.Read(ref gen2List)) is false)
//                        return;
//                    arr.AsSpan().CopyTo(newArr);
//                    Interlocked.CompareExchange(ref gen2List, newArr, arr);
//                    return;
//                }
//            }
//        }
//    }

//    private static void Free(StealableLocal<T> allocated)
//    {
//        var idx = allocated.slotToken.VolatileRead().Index;
//#if DEBUG
//        System.Diagnostics.Debug.Assert(allocated.slotToken.HasSomeone is false);
//        var pArr = primaryArray;
//        var gen2 = gen2List;
//        System.Diagnostics.Debug.Assert(
//            ((uint)idx < (uint)pArr.Length && object.ReferenceEquals(pArr[idx], allocated))
//            || ((uint)idx < (uint)gen2.Length && object.ReferenceEquals(pArr[idx], allocated))
//        );
//#endif
//        if (idx >= primarySize)// gen2
//        {
//            do
//            {
//                var count = Volatile.Read(ref gen2ListCount);
//#if DEBUG
//                System.Diagnostics.Debug.Assert(idx <= count);
//#endif
//                if (idx == count - 1)
//                {
//                    ReaderOwnerConsistency.WriteIndexOnly(ref allocated.slotToken, (int)count);
//                    if (count == Interlocked.CompareExchange(ref freeNodeIndex, idx, count))
//                        return;
//                    else
//                        continue;
//                }

//            } while (true);
//        }
//        else// primary
//        {
//            var comparand = Volatile.Read(ref freeNodeIndex);
//            while (true)
//            {
//                ReaderOwnerConsistency.WriteIndexOnly(ref allocated.slotToken, (int)comparand);
//                if (comparand == (comparand = Interlocked.CompareExchange(ref freeNodeIndex, idx, comparand)))
//                    return;
//            }
//        }

//    }


//    internal ReadRefLocal<T> TryRead()
//    {
//        if (ReaderOwnerConsistency.TryReaderEnter(ref this.slotToken) is false)
//            return default;


//    }

//    private static bool TrySteal(StealableLocal<T> stolenValue, StealableLocal<T> sourceComparand, bool stealAndLeave = false)
//    {
        
//        if (ReaderOwnerConsistency.TryReaderEnter(ref sourceComparand.slotToken) is false)
//            return false;
//        if (null != Interlocked.CompareExchange(ref sourceComparand, stolenValue, null))
//        { 
//            if (ReaderOwnerConsistency.ReaderExit(ref sourceComparand.slotToken) == 0)
//            {

//            }
//            return false; 
//        }

//        if (stealAndLeave)
//            return true;

//    }

//}
