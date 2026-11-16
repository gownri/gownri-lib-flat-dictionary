using FlatDictionary.InternalModules.Atomic;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal static class MetaTableHelperExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TableHeader.LockTaken LeavingAcquire(this MetaTable table, TableHeader.LockType lockType, MetaSlotResult result)
        => table.LeavingAcquire(lockType, result, MetaSlotResult.Unused);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TableHeader.LockTaken LeavingAcquire(this MetaTable table, TableHeader.LockType lockType, MetaSlotResult first, MetaSlotResult second)
    {
        scoped ref var header = ref table.Header;
        var taken = header.TryAcquireUntilHeavyProcess(lockType);
        if (taken)
            return taken;

        ReleasePerEntry(table, first, second);
        HeavyWait(in header, table.Source);
        return default;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void HeavyWait(scoped ref readonly TableHeader header, object? lockObj)
        {
            Thread.Yield();
            if ((header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess) is false))
                return;

            Thread.Yield();
            if ((header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess) is false))
                return;

            Thread.Sleep(0);
            if (header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess) is false)
                return;
            Thread.Sleep(0);
            if (header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess) is false)
                return;
            Thread.Sleep(1);
            if (header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess) is false)
                return;
            if (lockObj is not null)
                lock (lockObj) { }
        }
    }

    internal static void ReleasePerEntry(
        this MetaTable metaTable,
        MetaSlotResult firstResult,
        MetaSlotResult secondResult)
    {
        TryRelease(metaTable, firstResult);
        TryRelease(metaTable, secondResult);

        static void TryRelease(MetaTable table, MetaSlotResult result)
        {
            var slotIndex = result.SlotIndex + MetaTable.HeaderOffset;
            if ((uint)slotIndex < (uint)table.Source.Length)
                InterlockedMeta.CompareRestore(
                    ref table.Source[slotIndex],
                    result.Slot
                );
        }
    }

    private static bool TryAcquirePerEntry(
        MetaTable table,
        MetaSlotResult first,
        MetaSlotResult second
    )
    {
        return TryAcquire(table, first) && TryAcquire(table, second);
        static bool TryAcquire(MetaTable table, MetaSlotResult result)
        {
            var idx = result.SlotIndex + MetaTable.HeaderOffset;
            if ((uint)idx >= (uint)table.Source.Length)
                return true;

            return result.Slot 
                == InterlockedMeta.CompareSetEntanglement(
                    ref table.Source[idx],
                    result.Slot
                );
        }
    }
}
