using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlatDictionary.InternalModules;

//[Obsolete]
//internal readonly struct EntryPerLockResult
//{
//    private const int lostFlag = int.MinValue;
//    internal static EntryPerLockResult Lost 
//        => new(-1, Meta.EntanglementKeyIndex | int.MinValue);

//    internal readonly int SlotIndex;
//    private readonly int keyIndexAndFlag;
//    internal readonly int OriginalKeyIndex
//        => this.keyIndexAndFlag & ~lostFlag;

//    internal readonly bool IsLost
//    {
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        get => (this.OriginalKeyIndex & lostFlag) != 0; 
//    }

//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private EntryPerLockResult(int slotIndex, int originalKeyIndex)
//    {
//        this.SlotIndex = slotIndex;
//        this.keyIndexAndFlag = originalKeyIndex;
//    }

//    internal EntryPerLockResult Secured(int slotIndex, int originalKeyIndex)
//        => new(slotIndex, originalKeyIndex);


//}
