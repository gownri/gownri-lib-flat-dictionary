using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal readonly struct SlotIndex
{
    private const int readPastFlag = int.MinValue;
    internal readonly int RawValue;

    internal readonly int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.RawValue & ~readPastFlag;
    }
    internal readonly bool HasReadPastFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (this.RawValue & readPastFlag) != default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SlotIndex(int index, bool readPast)
        => this.RawValue =
#if DEBUG
            (index & readPastFlag) != default
                ? throw new ArgumentOutOfRangeException(nameof(index))
                :
#endif
        readPast ? index | readPastFlag : index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SlotIndex(int index)
        => this.RawValue =
#if DEBUG
            (index & readPastFlag) != default
                ? throw new ArgumentOutOfRangeException(nameof(index))
                :
#endif
         index;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SlotIndex(int index, int flag)
        => this.RawValue = index | flag;
    internal SlotIndex SetReadPastFlag()
        => new(this.RawValue, readPastFlag);
    public static implicit operator int(SlotIndex index)
        => index.RawValue;

    internal static SlotIndex CreateUnchecked(int value)
        => new(value, default(int));
}