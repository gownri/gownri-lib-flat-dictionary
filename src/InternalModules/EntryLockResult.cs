using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal readonly struct EntryLockResult
{
    internal static EntryLockResult Failed { get; } = new(false);
    internal static EntryLockResult Successful { get; } = new(true);

    private const int failedFlag = int.MinValue;
    private readonly int value;
    internal readonly int SecondaryKeyIndex;
    internal readonly int FailedFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.value & failedFlag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntryLockResult(bool isLockSuccessful, int secondaryLock = int.MaxValue, int secondaryKeyIndex = int.MaxValue)
    {
        this.value = isLockSuccessful 
            ? secondaryLock 
            : secondaryLock | failedFlag;
        this.SecondaryKeyIndex = secondaryKeyIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int operator |(int index, EntryLockResult result)
        => index | result.FailedFlag;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int operator |(EntryLockResult result, int index)
        => index | result.FailedFlag;
}
