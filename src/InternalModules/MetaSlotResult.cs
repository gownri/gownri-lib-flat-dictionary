using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatDictionary.InternalModules;

//[StructLayout(LayoutKind.Explicit)]
internal readonly struct MetaSlotResult
{
    internal const int failedSlotIndex = -1 - MetaTable.HeaderOffset | readPastFlag; // value + headerOffset = -1
    private const int readPastFlag = int.MinValue;
    private const int failedSlotKeyIndex = int.MaxValue;
    internal static MetaSlotResult Failed { get; } 
        = new(
            failedSlotIndex,
            Meta.CreateSentinel(failedSlotKeyIndex, Meta.Data.Sentinel),
            Meta.Data.Sentinel.SubJump(Meta.Data.Sentinel.GetJumpType())
        );// result.Slot.RawData > result.Entry.RawData is Failed

    internal static MetaSlotResult Unused { get; }
        = new(
            int.MaxValue,
            default,
            default
        );
    //[FieldOffset(0)]
    //internal readonly Meta Slot; //8byte
    //[FieldOffset(sizeof(long))]
    //internal readonly Meta.Data Entry; // 4byte
    //[FieldOffset(sizeof(long) + sizeof(int))]
    //internal readonly int SlotIndex; //4byte

    internal readonly Meta Slot { get; }
    internal readonly Meta.Data Entry { get; }
    internal readonly int SlotIndex { get; }

    internal readonly bool IsDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Slot == default && this.Entry.RawData == default;
    }

    internal readonly bool IsSuccessful
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Entry.IsDefault;
    }

    internal readonly bool IsFailed
    {       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Slot.PrimitivePackage == Failed.Slot.PrimitivePackage;
    }

    internal readonly bool IsReadPast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (this.SlotIndex & readPastFlag) != default;
    }

    /// <summary>
    /// IsSuccessful || IsFailed
    /// </summary>
    internal readonly bool IsInsertEndResult
    {
        //get => this.IsSuccessful || this.IsFailed;
        get => this.Slot.RawData >= this.Entry.RawData;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MetaSlotResult(int slotIndex, Meta slot, Meta.Data entry)
    {
        this.SlotIndex = slotIndex;
#if DEBUG
        System.Diagnostics.Debug.Assert(slot.HasEntanglement is false);
#endif
        this.Slot = slot;
        this.Entry = entry;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MetaSlotResult Success(int slotIndex, Meta slot)
        => new(
#if DEBUG
            (slotIndex & readPastFlag) != default
                ? throw new ArgumentOutOfRangeException(nameof(slotIndex))
                :
#endif
            slotIndex, 
            slot,
            default
        );
    internal static MetaSlotResult Progress(SlotIndex slotIndex, Meta slot, Meta.Data entry)
        => new(
            slotIndex.RawValue,
            slot,
            entry
        );
    public readonly override string ToString()
        => $"slotIndex:{this.SlotIndex} slot:{this.Slot} entry:{this.Entry}";

    public override bool Equals(object? obj)
        => obj is MetaSlotResult result && this == result;

    public override int GetHashCode()
        => HashCode.Combine(this.Slot.PrimitivePackage, this.Entry.RawData, this.SlotIndex);

    public static bool operator ==(MetaSlotResult a, MetaSlotResult b)
        => a.SlotIndex == b.SlotIndex
        && a.Slot.PrimitivePackage == b.Slot.PrimitivePackage
        && a.Entry.RawData == b.Entry.RawData;
    public static bool operator !=(MetaSlotResult a, MetaSlotResult b)
        => a.SlotIndex != b.SlotIndex
        || a.Slot.PrimitivePackage != b.Slot.PrimitivePackage
        || a.Entry.RawData != b.Entry.RawData;
}
