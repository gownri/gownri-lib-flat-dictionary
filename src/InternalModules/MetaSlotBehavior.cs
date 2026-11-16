using System;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal enum MetaSlotBehavior : int
{
    None = default,

    //Update = BridgingConstants.UpdateFlag,
    NeedRetry = 1 << 0,
    Extend = 1 << 1,
    UnreachedVisible = Meta.UnreachableFlag,
    //ContinueUntilBetter = UnreachedVisible >>> 1,
}

internal static class FindInsertBehaviorExtensions
{

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //internal static int UpdateFlagAsMSB(this MetaKeyIndexBehavior behavior)
    //   => (int)(behavior & MetaKeyIndexBehavior.Update) << BridgingConstants.UpdateFlagMoveToMSBShifter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AsUnreachedVisibleMask(this MetaSlotBehavior behavior)
        => ~(int)(behavior & MetaSlotBehavior.UnreachedVisible);

    //internal static bool IsAllowedUnreachable(this MetaSlotBehavior behavior, int keyIndex)
    //    => ((keyIndex & ~(int)behavior) & Meta.UnreachableFlag) == default;

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //internal static MetaSlotBehavior WithIndex(this MetaSlotBehavior behavior, int index)
    //    => (behavior & MetaSlotBehavior.UnreachedVisible) | (MetaSlotBehavior)index;

    //internal static int GetIndex(this MetaSlotBehavior behavior)
    //    => (int)(behavior & MetaSlotBehavior.UnreachedVisible);

    internal static MetaSlotBehavior SetNeedRetry(this MetaSlotBehavior behavior)
        => behavior | (MetaSlotBehavior)int.MaxValue;
    internal static bool IsNeedRetry(this MetaSlotBehavior behavior)
        => ((int)behavior & int.MaxValue) == int.MaxValue;


    internal static MetaSlotBehavior SetNeedRetryIfReadPast(this MetaSlotBehavior behavior, int index, int length)
        => (uint)(index & ~Meta.UnreachableFlag) >= (uint)length && (uint)index < (uint)int.MaxValue
            ? behavior.SetNeedRetry()
            : behavior;
    internal static MetaSlotBehavior SetNeedRetryIfReadPast(this MetaSlotBehavior behavior, Meta slot, int length)
        => ((uint)(slot.KeyIndex & ~Meta.UnreachableFlag) < (uint)length
            && slot.HasNotKeyIndex is false)
        || slot.HasEntanglement
            ? behavior.SetNeedRetry()
            : behavior;
}

file static class BridgingConstants
{
    private const int MoveToMSBShifter = sizeof(int) * 8 - 1;
    internal const int UpdateFlagShifter = 1;
    internal const int UpdateFlagMoveToMSBShifter = MoveToMSBShifter - UpdateFlagShifter;
    internal const int UpdateFlag = 1 << UpdateFlagShifter;

    internal const int ImmediateReturnFlagShifter = 1;
}
