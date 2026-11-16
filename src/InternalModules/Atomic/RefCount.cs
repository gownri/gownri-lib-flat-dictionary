using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;

internal struct RefCount
{
    // [        counters: 32                      |       len        ]
    // [refCount: 30 | overflowFlag: 1 | owner: 1 | burntLength: 32  ]
    private const int CountersBlockShifter = sizeof(int) * 8;
    private const long CountersBlockMask = -1L << CountersBlockShifter;
    private const long OwnerReferenceFlag = 1L << CountersBlockShifter;
    private const int OverflowedFlagShifter = CountersBlockShifter + 1;
    private const long CounterSticksFlag = 1L << OverflowedFlagShifter;
    private const int RefCounterShifter = OverflowedFlagShifter + 1;
    private const long ZeroRefCountMask = -1L << RefCounterShifter;
    private const long CounterIncremental = 1L << RefCounterShifter;
    private const ulong OverfrowCount = unchecked((ulong)-1L << RefCounterShifter);
    private const long UnderflowCount = (1 << RefCounterShifter) | (1 << OverflowedFlagShifter);

    private long count;
    
    internal readonly int BurntLength
        => unchecked((int)Volatile.Read(ref Unsafe.AsRef(in this.count)));
    internal readonly bool IsReferenced
        => (this.count >>> RefCounterShifter) != default;

    internal RefCount(bool owner)
        => this.count = OwnerReferenceFlag;
        
    //internal bool OwnerIncrement()
    //    => (Interlocked.Add(ref this.count, OwnerReference | CounterIncremental) & OwnerReference) != default;
    internal bool OwnerDecrement()
        => (Interlocked.Add(ref this.count, -OwnerReferenceFlag) & CountersBlockMask) == default;

    internal void Increment(int length = int.MaxValue)
    {
        if ((uint)length > (uint)int.MaxValue)
            ArgOutRange();
        do
        {
            var comparand = Volatile.Read(ref this.count);
            var value = comparand + CounterIncremental;
            if (unchecked((int)comparand) < length)
                value = (value & CountersBlockMask) | (long)(uint)length;
            if ((value & ZeroRefCountMask) == default)
                value |= CounterSticksFlag;
            if (comparand == Interlocked.CompareExchange(ref this.count, value, comparand))
                return;
        } while (true);
    }
    internal bool Decrement()
    {
        do
        {
            var comparand = Volatile.Read(ref this.count);
            var value = comparand - CounterIncremental;
            if ((ulong)comparand < (ulong)UnderflowCount)
                value |= CounterSticksFlag;
            if ((value & (ZeroRefCountMask | CounterSticksFlag)) == default)
            {
                value = comparand & (OwnerReferenceFlag | CounterSticksFlag);
            }
            if (comparand == Interlocked.CompareExchange(ref this.count, value, comparand))
                return value == default;
        } while (true);
    }
    private static void ArgOutRange()
        => throw new ArgumentOutOfRangeException(nameof(RefCount));

    public readonly override string ToString()
        => $"""
        "count": {this.count >>> RefCounterShifter}, "owner": {(this.count & OwnerReferenceFlag) != default}, "overflow": {(this.count & CounterSticksFlag) != default}, "burntLength": {(int)this.count}
        """;
}