#define SAFEBUCKET
#define EARLYREHASH
#define MUTE
#define COLLISIONCOUNT
//#define EXTEND_SEARCH
using FlatDictionary.InternalModules.Atomic;
using System;
using System.Data;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlatDictionary.InternalModules;

internal static class MetaTableConcurrentOperateExtensions
{

    [ThreadStatic]
    private static Meta dummySlot;
    internal static MetaSlotResult Debug_GetBucket(this scoped ReadOnlySpan<Meta> table, int hash, ushort version, int index)
    {
        var entry = Meta.Data.CreateEntry(hash, version);
        if (index == 0)
            return MetaSlotResult.Progress(
                new(table.GetBucketIndex(hash)),
                table.GetBucket(hash).VolatileRead(),
                entry
            );
        var jumpType = entry.GetJumpType();
        var jump = (int)jumpType;

//#if EARLYREHASH
//        hash += table.RehashStep(entry, jumpType);
//#endif
        for (var i = 0; i < index; ++i)
        {
            entry = entry.AddJump(jumpType);
            hash += jump;

            if (entry.Version != version)
            {
                RestoreOverranEntry(
                    table,
                    jumpType,
                    version,
                    ref entry,
                    ref hash
                );
            }
        }

        return MetaSlotResult.Progress(
            new( table.GetBucketIndex(hash) ),
            table.GetBucket(hash).VolatileRead(),
            entry
        );
    }
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int RehashStep(Meta.Data data, JumpType jump, int tableLength)
        => (int)(((data.RawData * (uint)(tableLength >>> 8)) & (uint.MaxValue - 1)) * jump.ForMetaDataDistanceAddOperationValue /* == (int)jump * 256*/);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RehashStep(this ReadOnlySpan<Meta> table, Meta.Data data, JumpType jump)
        => RehashStep(data, jump, table.Length);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RehashStep(this Span<Meta> table, Meta.Data data, JumpType jump)
        => RehashStep(data, jump, table.Length);

    private static int RestoreOverranEntry(
        this scoped ReadOnlySpan<Meta> table,
        JumpType jumpType,
        ushort version,
        scoped ref Meta.Data entry,
        scoped ref int pos
#if DEBUG
        , [CallerLineNumber] int line = default,
         [CallerMemberName] string methodName = default!
#endif

    )
    {
#if DEBUG
        System.Diagnostics.Debug.Assert(entry.Version != version);
#endif
        var jump = (int)jumpType;
        var prev = entry.SubJump(jumpType);
        //gap = 0;
        while (prev.Version != version)
        {
            //++gap;
            entry = prev;
            prev = entry.SubJump(jumpType);
            pos -= (int)jumpType;
            if (table.GetBucket(pos).VolatileRead().DataReader.Version > version)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"{table.GetBucket(pos).VolatileRead().DataReader.Version} > {version}, method: {methodName}, line: {line}");
#endif
                return table.Length;
            }
        }
        entry = entry.RestoreOverflowed(version);
        //#if DEBUG
        //        System.Diagnostics.Debug.WriteLineIf(gap != 0, $"over gap:{gap}");
        //        if (gap > Meta.Data.MaxCountableDistance)
        //            System.Diagnostics.Debug.WriteLine(gap);
        //#endif
        if (entry.DynamicHash == 0)
            pos += RehashStep(entry, jumpType, table.Length);
        else
        {
            // retesting
            pos -= jump;
            entry = entry.SubJump(jumpType);
        }
        return jump switch
        {
            JumpType.One => Meta.Data.MaxCountableDistance,
            JumpType.Short => Meta.Data.MaxCountableDistance / JumpType.Short,
            JumpType.Medium => Meta.Data.MaxCountableDistance / JumpType.Medium,
            JumpType.Long => Meta.Data.MaxCountableDistance / JumpType.Long,
            _ => Meta.Data.MaxCountableDistance / jump,
        };

    }

#if DEBUG
    internal static Meta SpinWaitWhileHasEntanglement(this scoped Span<Meta> table, int slotIndex)
        => table.SpinWaitWhileHasEntanglement(slotIndex, in dummySlot, in dummySlot, in dummySlot);
    internal static Meta SpinWaitWhileHasEntanglement(this scoped ReadOnlySpan<Meta> table, int slotIndex)
    {
        var meta = table.GetBucket(slotIndex).VolatileRead();
        if (meta.HasEntanglement is false)
            return meta;
        return table.SpinWaitWhileHasEntanglement(slotIndex, in dummySlot, in dummySlot, in dummySlot);
    }
#endif
    internal static Meta SpinWaitWhileHasEntanglement(this scoped Span<Meta> table, int slotIndex
#if DEBUG
        , scoped ref readonly Meta own1, scoped ref readonly Meta own2, scoped ref readonly Meta offsetBase,
        int targetLocationIndex = -1, [CallerMemberName] string methodName = default!, [CallerLineNumber] int line = default
#endif
        )
        => SpinWaitWhileHasEntanglement(in table.GetBucket(slotIndex)
#if DEBUG
            , in own1, in own2, in offsetBase, methodName: methodName, line: line
#endif
        );
    internal static Meta SpinWaitWhileHasEntanglement(this scoped ReadOnlySpan<Meta> table, int slotIndex
#if DEBUG
        , scoped ref readonly Meta own1, scoped ref readonly Meta own2, scoped ref readonly Meta offsetBase,
        int targetLocationIndex = -1, [CallerMemberName] string methodName = default!, [CallerLineNumber] int line = default
#endif
        )
        => SpinWaitWhileHasEntanglement(in table.GetBucket(slotIndex)
#if DEBUG
            , in own1, in own2, in offsetBase, methodName: methodName, line: line
#endif
            );
    private static Meta SpinWaitWhileHasEntanglement(scoped ref readonly Meta entanglementLocation
#if DEBUG
        , scoped ref readonly Meta own1, scoped ref readonly Meta own2, scoped ref readonly Meta offsetBase
        , int entangledSlotIndex = -1, int targetLocationIndex = -1, [CallerMemberName] string methodName = default!, [CallerLineNumber] int line = default
#endif
    )
    {
        return SpinWaitWhileHasEntanglementANDEqual(in entanglementLocation, ref dummySlot, dummySlot.DataReader
#if DEBUG
          , in own1, in own2, in offsetBase, targetLocationIndex, methodName, line
#endif
            );
    }


#if DEBUG
    private static bool dump = false;
    internal static void Dump()
    {
        slots.Clear();
        dump = true;
    }

    internal static void DumpReset()
    {
        dump = false;
    }
    private static System.Collections.Concurrent.ConcurrentBag<IntPtr> slots = new();

    private static object lockObj { get; } = new();
    private const int entryLimit = 4;
    private static int waitThreads = 0;
#endif
    private static Meta SpinWaitWhileHasEntanglementANDEqual(scoped ref readonly Meta entanglementLocation, scoped ref readonly Meta location2, Meta.Data entry
#if DEBUG
        , scoped ref readonly Meta own1, scoped ref readonly Meta own2, scoped ref readonly Meta offsetBase,
        int targetLocationIndex = -1, [CallerMemberName] string methodName = default!, [CallerLineNumber] int line = default
#endif
    )
    {
#if DEBUG
        var duped = true;
        //entanglementTracer.AddOrUpdate(
        //    entangledSlotIndex,
        //    (key, arg) => arg,
        //    (key, value, arg) => arg,
        //    (Thread.CurrentThread.ManagedThreadId, targetLocationIndex, methodName)
        //);
        var sp = new SpinWait();
        Interlocked.Increment(ref waitThreads);
#endif
        Meta read;
        while ((read = entanglementLocation.VolatileRead()).HasEntanglement
            && location2.VolatileRead().RawData == entry.RawData)
        {

#if DEBUG
            if (dump && duped)
            {
                var s = slots;
                s.Add(Unsafe.ByteOffset(ref Unsafe.AsRef(in offsetBase), ref Unsafe.AsRef(in own1)));
                s.Add(Unsafe.ByteOffset(ref Unsafe.AsRef(in offsetBase), ref Unsafe.AsRef(in own2)));
                duped = false;
            }
#endif

            //if (Volatile.Read(ref waitThreads) > entryLimit)
            //    sp.SpinOnce();
            //else
                Thread.Yield();
        }
#if DEBUG
        Interlocked.Decrement(ref waitThreads);
#endif
        return read;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //internal static MetaSlotResult FindOrInsertPoint<TItem, TKey>(
    //        this MetaTable metaTable,
    //        TItem[] itemArray,
    //        TKey proxy,
    //        int hash,
    //        AtomicLengthAndVersion lengthAndVersion,
    //        MetaSlotBehavior behavior = default)
    //where TKey : struct, IEquatable<TItem>
    //{
    //    var entry = Meta.Data.CreateEntry(hash, lengthAndVersion.Version.Table);
    //    var initial = metaTable.GetBucket(hash).VolatileRead();
    //    hash = metaTable.GetBucketIndex(hash);
    //    if (initial.DataReader.Hypothesis().RawData == entry.RawData)
    //    {
    //        var index = initial.KeyIndex & behavior.AsUnreachedVisibleMask();
    //        if ((uint)index >= (uint)lengthAndVersion.RegisteredKeysLength)
    //            behavior = behavior.SetNeedRetryIfReadPast(initial, lengthAndVersion.RegisteredKeysLength);
    //        else 
    //        { 
    //            var item = AtomicHelper<TItem>.BulkLoad(in itemArray[index]);
    //            if (proxy.Equals(item))
    //                return MetaSlotResult.Success(hash, initial.Hypothesis());
    //        }
    //    }
    //    else if (initial.DataReader.Version != lengthAndVersion.Version.Table)
    //    {
    //        if (initial.HasEntanglement)
    //            behavior = behavior.SetNeedRetry();
    //        else
    //            return MetaSlotResult.Progress(
    //                new(hash, initial.DataReader.Version > lengthAndVersion.Version.Table),
    //                initial.Hypothesis(),
    //                entry
    //            );
    //    }
    //    if ((uint)lengthAndVersion.RegisteredKeysLength > (uint)itemArray.Length)
    //        return MetaSlotResult.Failed;
    //    return metaTable.AsSpan().FindOrInsertPoint(
    //        new ReadOnlySpan<TItem>(itemArray, 0, lengthAndVersion.RegisteredKeysLength),
    //        proxy,
    //        (new(hash), initial, entry),
    //        behavior
    //    );
    //}
//    static MetaSlotResult FindOrInsertPoint<TItem, TKey>(
//           this scoped ReadOnlySpan<Meta> table,
//           scoped ReadOnlySpan<TItem> items,
//           TKey key,
//           (SlotIndex pos, Meta currentTemp, Meta.Data entry) slotBox,
//           MetaSlotBehavior behavior = default)
//       where TKey : struct, IEquatable<TItem>
//    {
//        //if (table.Length == 0)
//        //    return MetaSlotResult.Failed;

//#if DEBUG
//        System.Diagnostics.Debug.Assert(table.Length != 0);
//#endif
//        var maskCache = table.GetBucketMask();

//        var entry = slotBox.entry;
//        var version = entry.Version;
//        var jumpType = entry.GetJumpType();
//        var jump = (int)jumpType;
//        var pos = slotBox.pos.RawValue;
//            //#if EARLYREHASH
//            //            + table.RehashStep(entry, jumpType);
//            //#endif


//        //scoped ref readonly Meta current = ref Unsafe.NullRef<Meta>();
//        Meta currentTemp;
//        int indexOfItems;

//        var read = 0;
//#if DEBUG
//        var read_debug = 0;
//#endif
//        while (true)
//        {

//            entry = entry.AddJump(jumpType);
//            scoped ref readonly var current = ref table.GetBucket(pos += jump, maskCache);

//            currentTemp = current.VolatileRead();

//            if (currentTemp.RawData > entry.RawData)
//                continue;
//#if DEBUG
//            read_debug++;
//#endif
//            if (currentTemp.RawData > entry.RawData)
//                continue;
//            if (currentTemp.RawData < entry.RawData)
//            {
//                if (currentTemp.RawData == entry.SetEntanglementFlag().RawData)
//                {
//                    currentTemp = currentTemp.UnsetEntanglement();
//                    // go to currentTemp.RawData == entry.RawData
//                }
//                else if (entry.Version != version)
//                {
//#if DEBUG
//                    System.Diagnostics.Debug.Assert((uint)read_debug < (uint)(table.Length + Meta.Data.MaxCountableDistance));
//#endif
//                    read += RestoreOverranEntry(
//                        table,
//                        jumpType,
//                        version,
//                        ref entry,
//                        ref pos
//                    );

//                    if ((uint)read > ((uint)table.Length + Meta.Data.MaxCountableDistance))
//                        return MetaSlotResult.Failed;

//                    continue;
//                }
//                else
//                {
//                    if (currentTemp.HasEntanglement)
//                    {
//                        if (current.VolatileRead().HasEntanglement)
//                            SpinWaitWhileHasEntanglement(in current
//#if DEBUG
//                                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
//#endif
//                            );
//                        entry = entry.SubJump(jumpType);
//                        pos -= jump;
//                        continue;
//                    }
//                    break;
//                }
//            }

//            indexOfItems = currentTemp.KeyIndex;

//            // currentTemp.RawData == entry.RawData
//            if ((uint)indexOfItems < (uint)items.Length)
//            {
//                var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//                if (key.Equals(item))
//                    return MetaSlotResult.Success(pos & maskCache, currentTemp);
//            }
//            else
//            {
//                indexOfItems = currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//                if ((uint)indexOfItems < (uint)items.Length)
//                {
//                    if ((behavior & MetaSlotBehavior.UnreachedVisible) != default)
//                    {
//                        var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//                        if (key.Equals(item))
//                            //if ((behavior & MetaKeyIndexBehavior.ContinueUntilBetter) == default)
//                            return MetaSlotResult.Success(pos & maskCache, currentTemp);
//                    }
//                    if (slotBox.currentTemp.IsDefault)
//                        slotBox = (
//                            new(
//                                pos & maskCache
//                            ),
//                            currentTemp,
//                            entry
//                        );
//                }
//                else// read past
//                {
//                    if (slotBox.currentTemp.IsDefault)
//                        slotBox = (
//                            new(
//                                pos & maskCache,
//#if ENABLE_TOMBSTONE
//                                !currentTemp.HasTombstone
//#else
//                                true
//#endif
//                            ),
//                            currentTemp,
//                            entry
//                        );
//                }
//            }
//            continue;
//        }// endless

//#if DEBUG
//        indexOfItems = slotBox.currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//        if ((uint)indexOfItems < (uint)items.Length
//        && behavior.HasFlag(MetaSlotBehavior.UnreachedVisible))
//        {
//            var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//            if (key.Equals(item))
//                throw new Exception();
//        }

//#endif
//        if (behavior.IsNeedRetry())
//            return MetaSlotResult.Progress(
//                new(pos & maskCache, true),
//                currentTemp,
//                entry
//            );
//        else if (slotBox.currentTemp.IsDefault)
//            return MetaSlotResult.Progress(new(pos & maskCache), currentTemp, entry);
//        else
//            return MetaSlotResult.Progress(
//                    slotBox.pos,
//                    slotBox.currentTemp,
//                    slotBox.entry
//                );

//    }
    internal static MetaSlotResult FindOrInsertPoint<TItem, TKey>(
            this scoped ReadOnlySpan<Meta> table,
            scoped ReadOnlySpan<TItem> items,
            TKey key,
            int hash,
            ushort version,
            MetaSlotBehavior behavior = default)
        where TKey : struct, IEquatable<TItem>
    {
        //if (table.Length == 0)
        //    return MetaSlotResult.Failed;

#if DEBUG
        System.Diagnostics.Debug.Assert(table.Length != 0);
#endif
        var maskCache = table.GetBucketMask();

        var entry = Meta.Data.CreateEntry(hash, version);
        var jumpType = entry.GetJumpType();
        var jump = (int)jumpType;
        //(SlotIndex pos, Meta currentTemp, Meta.Data entry) swapPotential = default;
        var swapPotentialMeta = default(Meta);
        var swapPotentialPos = default(SlotIndex);
        var pos = hash
//#if EARLYREHASH
//            + table.RehashStep(entry, jumpType);
//#endif
            ;


        //scoped ref readonly Meta current = ref Unsafe.NullRef<Meta>();
        Meta currentTemp;
        int indexOfItems;
#if COLLISIONCOUNT
        var read = 0;
#endif
#if DEBUG
        var read_debug = 0;
#endif
        while (true)
        {

            entry = entry.AddJump(jumpType);
            scoped ref readonly var current = ref table.GetBucket(pos += jump, maskCache);

            currentTemp = current.VolatileRead();

            if (currentTemp.RawData > entry.RawData)
                continue;
#if DEBUG
            read_debug++;
#endif
            if (currentTemp.RawData > entry.RawData)
                continue;
            if (currentTemp.RawData < entry.RawData)
            {
                if (currentTemp.RawData == entry.SetEntanglementFlag().RawData)
                {
                    currentTemp = currentTemp.UnsetEntanglement();
                    // go to currentTemp.RawData == entry.RawData
                }
                else if (entry.Version != version)
                {
#if DEBUG
                    System.Diagnostics.Debug.Assert((uint)read_debug < (uint)(table.Length + Meta.Data.MaxCountableDistance));
#endif
#if COLLISIONCOUNT
                    read +=
#endif
                        RestoreOverranEntry(
                        table,
                        jumpType,
                        version,
                        ref entry,
                        ref pos
                    );
#if COLLISIONCOUNT
                    if ((uint)read > ((uint)table.Length + Meta.Data.MaxCountableDistance))
                        return MetaSlotResult.Failed;
#endif
                    continue;
                }
                else
                {
                    if (currentTemp.HasEntanglement)
                    {
                        if (current.VolatileRead().HasEntanglement)
                            SpinWaitWhileHasEntanglement(in current
#if DEBUG
                                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
#endif
                            );
                        entry = entry.SubJump(jumpType);
                        pos -= jump;
                        continue;
                    }
                    break;
                }
            }

            indexOfItems = currentTemp.KeyIndex; 

            // currentTemp.RawData == entry.RawData
            if ((uint)indexOfItems < (uint)items.Length)
            {
                var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
                if (key.Equals(item))
                    return MetaSlotResult.Success(pos & maskCache, currentTemp);
            }
            else
            {
                indexOfItems = currentTemp.KeyIndex & ~Meta.UnreachableFlag;
                if ((uint)indexOfItems < (uint)items.Length)
                {
                    var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
                    if (key.Equals(item))
                    {
                        if ((behavior & MetaSlotBehavior.UnreachedVisible) != default)
                            return MetaSlotResult.Success(pos & maskCache, currentTemp);

                        if (swapPotentialMeta.IsDefault)
                        {
                            swapPotentialMeta = currentTemp;
                            swapPotentialPos = new(pos & maskCache);
                        }
                    }
                    //if (swapPotential.currentTemp.IsDefault)
                    //    swapPotential = (
                    //        new(
                    //            pos & maskCache
                    //        ),
                    //        currentTemp,
                    //        entry
                    //    );
                    
                }
                else// read past
                {
                    //                    if (swapPotential.currentTemp.IsDefault)
                    //                        swapPotential = (
                    //                            new(
                    //                                pos & maskCache,
                    //#if ENABLE_TOMBSTONE
                    //                                !currentTemp.HasTombstone
                    //#else
                    //                                true
                    //#endif
                    //                            ),
                    //                            currentTemp,
                    //                            entry
                    //                        );
                    if (swapPotentialMeta.IsDefault)
                    {
                        swapPotentialMeta = currentTemp;
                        swapPotentialPos = new(
                            pos & maskCache,
#if ENABLE_TOMBSTONE
                          !currentTemp.HasTombstone
#else
                            true
#endif
                        );
                    }
                }
            }
            continue;
        }// endless

#if DEBUG
        //indexOfItems = swapPotential.currentTemp.KeyIndex & ~Meta.UnreachableFlag;
        indexOfItems = swapPotentialMeta.KeyIndex & ~Meta.UnreachableFlag;
        if ((uint)indexOfItems < (uint)items.Length
        && behavior.HasFlag(MetaSlotBehavior.UnreachedVisible))
        {
            var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
            if (key.Equals(item))
                throw new Exception();
        }

#endif
            if (behavior.IsNeedRetry())
            return MetaSlotResult.Progress(
                new(pos & maskCache, true),
                currentTemp,
                entry
            );
            //else if (swapPotential.currentTemp.IsDefault)
        else if (swapPotentialMeta.IsDefault)
            return MetaSlotResult.Progress(new(pos & maskCache), currentTemp, entry);
        else
            //return MetaSlotResult.Progress(
            //        swapPotential.pos,
            //        swapPotential.currentTemp,
            //        swapPotential.entry
            //    );
            return MetaSlotResult.Progress(
                    swapPotentialPos,
                    swapPotentialMeta,
                    swapPotentialMeta.DataReader
                );

    }

    
    internal static MetaSlotResult FindByKeyIndex(this scoped ReadOnlySpan<Meta> table, int index, int hash, ushort version)
    {
        //var initial = table.GetBucket(hash).VolatileRead();
        ////do
        ////{
        //    if (initial.Equals(index, version))
        //        return MetaSlotResult.Success(table.GetBucketIndex(hash), initial.Hypothesis());
        //    if (initial.HasEntanglement is false)
        //        break;
        //    initial = table.SpinWaitWhileHasEntanglement(hash);
        //} while (true);
        return table.FindByKeyIndex(
            index,
            hash,
            Meta.Data.CreateEntry(hash, version)
        );
    }

    //internal static MetaSlotResult FindByKeyIndex(this scoped Span<Meta> table, int index, int hash, ushort version)
    //{
    //    var entry = Meta.Data.CreateEntry(hash, version);
    //    return ((ReadOnlySpan<Meta>)table).FindByKeyIndex(
    //        index,
    //        hash,
    //        entry
    //    );
    //}
    
    //private static MetaSlotResult FindByKeyIndex(this scoped Span<Meta> table, int keyIndex, int pos, Meta.Data entry)
    //    => ((ReadOnlySpan<Meta>)table).FindByKeyIndex(keyIndex, pos, entry);
    private static MetaSlotResult FindByKeyIndex(this scoped ReadOnlySpan<Meta> table, int keyIndex, int pos, Meta.Data entry)
    {
        //if (table.Length == 0)
        //    return MetaSlotResult.Failed;
#if DEBUG
        System.Diagnostics.Debug.Assert(table.Length != 0);
#endif
        var maskCache = table.GetBucketMask();
        var jumpType = entry.GetJumpType();
        var jump = (int)jumpType;
        var version = entry.Version;
#if DEBUG
        //System.Diagnostics.Debug.Assert(entry.Distance == 0);
        var read_debug = 0;
#endif
//#if EARLYREHASH
//        pos += table.RehashStep(entry, jumpType);
//#endif
        //(int pos, Meta.Data entry) postponedSnapshot = default;

        var read = 0;
        while (true)
        {
#if DEBUG
                read_debug++;
#endif

            entry = entry.AddJump(jumpType);
            scoped ref readonly var current = ref table.GetBucket(pos += jump, maskCache);

            var currentTemp = current.VolatileRead();

            if (currentTemp.RawData > entry.RawData)
                continue;

            if (currentTemp.RawData < entry.RawData)
            {
                if (currentTemp.RawData == entry.SetEntanglementFlag().RawData)
                {
                    currentTemp = currentTemp.UnsetEntanglement();
                    // go to currentTemp.RawData == entry.RawData
                }
                else if (entry.Version != version)
                {
#if DEBUG
                //System.Diagnostics.Debug.Assert((uint)read_debug < (uint)table.Length);
#endif
                    read += RestoreOverranEntry(
                        table,
                        jumpType,
                        version,
                        ref entry,
                        ref pos
                    );

                    if ((uint)read > (uint)table.Length + Meta.Data.MaxCountableDistance)
                        return MetaSlotResult.Failed;

                    continue;
                }
                else
                {
                    if (currentTemp.HasEntanglement)
                    {
                        if (current.VolatileRead().HasEntanglement)
                            SpinWaitWhileHasEntanglement(in current
#if DEBUG
                                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
#endif
                            );
                        entry = entry.SubJump(jumpType);
                        pos -= jump;
                        continue;
                    }
                    return MetaSlotResult.Progress(new(pos & maskCache), currentTemp, entry);
                }
            }

            //if (currentTemp.RawData == entry.RawData)
            //{
            //    if (currentTemp.KeyIndex == keyIndex)
            //        return MetaSlotResult.Success(pos & maskCache, currentTemp);
            //    else if (currentTemp.HasEntanglement
            //    && postponedSnapshot.entry.IsDefault)
            //        postponedSnapshot = (pos, entry);

            //    continue;
            //}
            if (currentTemp.KeyIndex == keyIndex)
                return MetaSlotResult.Success(pos & maskCache, currentTemp);
            //else 
            //    return MetaSlotResult.Progress(new (pos & maskCache), currentTemp, entry);

            //            if (postponedSnapshot.entry.IsDefault)
            //            {
            //                if (currentTemp.HasEntanglement is false)
            //                    return MetaSlotResult.Progress(pos & maskCache, currentTemp, entry);
            //            }
            //            else
            //            {
            //                (pos, entry) = postponedSnapshot;
            //                postponedSnapshot = default;
            //                current = ref table.GetBucket(pos, maskCache);
            //            }

            //            if (current.VolatileRead().HasEntanglement)
            //                SpinWaitWhileHasEntanglement(in current
            //#if DEBUG
            //                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
            //#endif

            //                );

            //            pos -= jump;
            //            entry = entry.SubJump(jumpType);
            //            read = 0;
            //            continue;
        }// loop
    }

    //private static MetaSlotResult monoGuarantee { get; } 
    //    = MetaSlotResult.Unused;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MetaSlotResult LockPerSlot(this MetaTable table, scoped ref MetaSlotResult insertPointResult)
    {
        var resultLocal = insertPointResult;
        if (resultLocal.Slot != InterlockedMeta.CompareSetEntanglement(
                ref table.GetBucket(resultLocal.SlotIndex), 
                resultLocal.Slot
            ))
        {
            if (RetryWhileLessSlot(table, resultLocal, ref insertPointResult) is false)
                return MetaSlotResult.Failed;
            resultLocal = insertPointResult;
            [MethodImpl(MethodImplOptions.NoInlining)]
            static bool RetryWhileLessSlot(MetaTable table, MetaSlotResult local,  scoped ref MetaSlotResult insertPointResult)
            {
                scoped ref var slot = ref table.GetBucket(local.SlotIndex);
                while (true)
                {
                    var comparand = slot.VolatileRead();
                    if (comparand.HasEntanglement)
                        comparand = SpinWaitWhileHasEntanglement(in slot
#if DEBUG
                            , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table.AsSpan())
#endif
                        );
                    
                    if (comparand.RawData >= local.Entry.RawData)
                        break;

                    if (comparand == InterlockedMeta.CompareSetEntanglement(ref slot, comparand))
                    {
                        //insertPointResult = MetaSlotResult.Progress(local.SlotIndex, comparand, local.Entry);
                        insertPointResult = MetaSlotResult.Progress(new(local.SlotIndex), comparand, local.Entry);
                        return true;
                    }
                }

                insertPointResult = MetaSlotResult.Failed;
                return false;
            }
        }

        if(resultLocal.Slot.DataReader.Version < resultLocal.Entry.Version)
            return MetaSlotResult.Unused;
        else
            return HoldCompletionGuarantee(table, ref insertPointResult);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static MetaSlotResult HoldCompletionGuarantee(
            MetaTable metaTable,
            scoped ref MetaSlotResult firstInsertedResult)
        {
            var table = metaTable.AsSpan();
            var maskCache = table.GetBucketMask();
            var firstResultLocal = firstInsertedResult;

        recursion:;
            var pos = firstResultLocal.SlotIndex;
            var first = firstResultLocal.Slot;
#if DEBUG
            var read_debug = 0;
            if (firstResultLocal.IsDefault || firstResultLocal.IsFailed || first.HasEntanglement)
                throw new InvalidOperationException($"{nameof(first)} is default or invalid values");
#endif

            var entry = first.DataReader;
            var jumpType = entry.GetJumpType();
            var jump = (int)jumpType;
            var version = entry.Version;

            //scoped ref var current = ref Unsafe.NullRef<Meta>();
            Meta currentTemp;

            var read = 0;
//#if EARLYREHASH
//            if (entry.Distance == 0)
//                pos += table.RehashStep(entry, jumpType);
//#endif
            while (true)
            {
#if DEBUG
            read_debug++;
#endif

                entry = entry.AddJump(jumpType);
                scoped ref var current = ref table.GetBucket(pos += jump, maskCache);

                currentTemp = current.VolatileRead();

                if (currentTemp.RawData >= entry.RawData)
                    continue;

                if (entry.Version != version)
                {
#if DEBUG
                    System.Diagnostics.Debug.Assert((uint)read_debug < (uint)table.Length);
#endif
                    read += RestoreOverranEntry(
                        table,
                        jumpType,
                        version,
                        ref entry,
                        ref pos
                    );

                    if ((uint)read > (uint)table.Length)
                    {
                        InterlockedMeta.CompareRestore(ref table.GetBucket(firstResultLocal.SlotIndex), firstInsertedResult.Slot);
                        firstInsertedResult = MetaSlotResult.Failed;

                        return MetaSlotResult.Failed;
                    }
                    continue;
                }

                if (currentTemp.HasEntanglement)
                {
                    if (current.VolatileRead().HasEntanglement)
                    {
                        if (firstResultLocal
                            != (firstResultLocal = EvacuationWait(
                                table,
                                firstResultLocal,
                                in current
                            )))
                        {
                            if(firstResultLocal.IsFailed)
                            {
                                firstInsertedResult = MetaSlotResult.Failed;
                                return MetaSlotResult.Failed;
                            }

                            firstInsertedResult = firstResultLocal;
                            goto recursion;
                        }
                    }

                    static MetaSlotResult EvacuationWait(
                        scoped Span<Meta> table,
                        MetaSlotResult firstSlotResult,
                        scoped ref readonly Meta current)
                    {
                        var slotOriginal = firstSlotResult.Slot;

                        scoped ref var slot = ref table.GetBucket(firstSlotResult.SlotIndex);
#if DEBUG
                        if (slotOriginal.HasEntanglement)
                            throw new Exception();
#endif

                        //Volatile.Write(ref Unsafe.As<Meta, long>(ref slot), slotOriginal.PrimitivePackage);
                        //InterlockedMeta.Write(ref slot, slotOriginal);
                        if (slotOriginal != InterlockedMeta.CompareRestore(ref slot, slotOriginal))
                            return MetaSlotResult.Failed;
                        SpinWaitWhileHasEntanglementANDEqual(in current, in slot, slotOriginal.DataReader
#if DEBUG
                        , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
#endif
                            );
                        while (slotOriginal 
                            != (slotOriginal = InterlockedMeta.CompareSetEntanglement(ref slot, slotOriginal)))
                        {
                            if (slotOriginal.HasEntanglement)
                                slotOriginal = SpinWaitWhileHasEntanglement(in slot
#if DEBUG
                                    , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
#endif
                                );
                            if (slotOriginal.RawData >= firstSlotResult.Entry.RawData)
                                return MetaSlotResult.Failed;

                            firstSlotResult = MetaSlotResult.Progress(new(firstSlotResult.SlotIndex), slotOriginal, firstSlotResult.Entry);
                        }

                        return firstSlotResult;
                    }// end evacuationWait method
                }// end hasEntanglement
                else if (currentTemp == InterlockedMeta.CompareSetEntanglement(ref current, currentTemp))
                    return MetaSlotResult.Progress(
                        new SlotIndex(pos & maskCache),
                        currentTemp,
                        entry
                    );

                // retest
                entry = entry.SubJump(jumpType);
                pos -= jump;
                continue;
            }// endless
        }
    }

    private enum EntanglementSolution : byte
    {
        Continue,
        DoAdvance,
        DoRollback,
        KickSelf,
        VersionOver,
        FalseProbeEndToEnd,
        __DEBUG__Ravage,
    }
    internal static bool ClosingInsert(
        this MetaTable metaTable,
        scoped ref Meta rollbackGuarantee, Meta commitValue,
        scoped ref Meta advanceGuarantee,
        MetaSlotResult advanceSlotResult, int advancedKeyIndex)
    {
        var advancedValue = new Meta(advancedKeyIndex, advanceSlotResult.Entry);
        if (advanceSlotResult.Slot.DataReader.Version < advanceSlotResult.Entry.Version)
        {
            InterlockedMeta.Write(ref rollbackGuarantee, commitValue);
            InterlockedMeta.Write(ref advanceGuarantee, advancedValue);
            return true;
        }
        return metaTable.ClosingInsert(
            ref rollbackGuarantee, commitValue,
            ref advanceGuarantee, advancedValue, 
            advanceSlotResult.SlotIndex,
            advanceSlotResult.Slot
        );
    }
#if DEBUG
    private static int collisionCount = 0;
    private static int swapCount = 0;
    private static int hashCollisionCount = 0;
#endif
    internal static bool ClosingInsert(
        this MetaTable metaTable,
        scoped ref Meta rollbackGuarantee, Meta commitValue,
        scoped ref Meta advanceGuarantee, Meta advancedValue,
        int pos,
        Meta kickedMeta
        )
    {
        var table = metaTable.AsSpan();
#if DEBUG
        if (!rollbackGuarantee.HasEntanglement || !advanceGuarantee.HasEntanglement)
            throw new InvalidOperationException();
        if (commitValue.HasEntanglement || advancedValue.HasEntanglement)
            throw new InvalidOperationException();
        if (kickedMeta.HasEntanglement)
            throw new InvalidOperationException();

#endif
        var maskCache = table.GetBucketMask();
        var version = kickedMeta.DataReader.Version;
        scoped ref var current = ref Unsafe.NullRef<Meta>();

        Meta currentTemp;
        Meta.Data entry;
        JumpType jumpType;
        int jump;

#if DEBUG
            var collisions = 0;
            var swaps = 0;
       
#endif
        while(true)
        {
#if DEBUG
            var hashes = 0;
#endif
            entry = kickedMeta.DataReader;
            jumpType = entry.GetJumpType();
            jump = (int)jumpType;
            //#if EARLYREHASH
            //            if (entry.Distance == 0)
            //                pos += table.RehashStep(entry, jumpType);
            //#endif
            do
            {
                entry = entry.AddJump(jumpType);
                current = ref table.GetBucket(pos += jump, maskCache);

                currentTemp = current.VolatileRead();
#if DEBUG
                collisions++;
                if (currentTemp.RawData == entry.RawData)
                    hashes++;
#endif
                if (currentTemp.RawData >= entry.RawData)
                    continue;
                //if (currentTemp.RawData < entry.RawData)
                if (entry.Version != version)
                {
                    //if (versionObservation.VolatileReadState().Version != version)
                    //    return false;

                    if ((uint)RestoreOverranEntry(
                        table,
                        jumpType,
                        version,
                        ref entry,
                        ref pos
                    ) >= (uint)table.Length)
                    {
#if DEBUG
                        System.Diagnostics.Debug.Fail($" name: {nameof(RestoreOverranEntry)}, v: {version}, e: {entry}, p: {pos}");
#endif
                        InterlockedMeta.Write(ref rollbackGuarantee, new(advancedValue.KeyIndex, rollbackGuarantee.DataReader));
                        InterlockedMeta.Write(ref advanceGuarantee, kickedMeta);

                        return false;
                    }

#if DEBUG
                    collisions--;
#endif
                    continue;
                }

                if (currentTemp.HasEntanglement)
                {
#if DEBUG
#if !MUTE
                    System.Diagnostics.Debug.WriteLine(
                        $"{nameof(MetaTableConcurrentOperateExtensions.EntanglementSolution)}: {DateTime.Now.ToString("hh:mm:ss:fff")}" +
                        $"\n\tcurrent: {{pos: {table.GetBucketIndex(pos)}, kick: {kickedMeta}, entry: {entry}}}" +
                        $"\n\tr: {{pos: {table.Offset(in rollbackGuarantee)}, meta: {commitValue}}}, " +
                        $"\n\ta: {{pos: {table.Offset(in advanceGuarantee)}, meta: {advancedValue}}}"
                    );
#endif
                    EntanglementSolution solution;
                    switch (
                        solution = EntanglementResolve(
                            table,
                            ref current, currentTemp,
                            ref rollbackGuarantee,
                            ref advanceGuarantee,
                            version
                        )
                    )
#else
                    switch (
                        EntanglementResolve(
                            table,
                            ref current, currentTemp,
                            ref rollbackGuarantee,
                            ref advanceGuarantee,
                            version
                        )
                    )
#endif
                    {
                        case EntanglementSolution.Continue:
#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(EntanglementSolution.Continue)} from {solution} {{ r:{commitValue}, a:{advancedValue}, pos:{pos}, curr:{current}}}");
#endif
                            // retesting
                            pos -= jump;
                            entry = entry.SubJump(jumpType);

#if DEBUG
                            collisions--;
#endif
                            continue;
                        case EntanglementSolution.KickSelf:
#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(EntanglementSolution.KickSelf)}{{ r:{commitValue}, a:{advancedValue}, pos:{pos}, curr:{current}}}");
                            System.Diagnostics.Debug.Assert(Unsafe.AreSame(ref current, ref rollbackGuarantee), nameof(EntanglementSolution) + "." + nameof(EntanglementSolution.KickSelf));
#endif
                            if (commitValue.RawData < entry.RawData)
                            {

                                currentTemp = commitValue;
                                // entanglement to entanglement
                                //commitValue = Meta.CreateWithEntanglement(entry);
                                commitValue = kickedMeta.Update(entry).SetEntanglement();
                                break;// logical swapped
                            }
#if DEBUG
                            swaps++;
#endif
                            continue;// skip
                        case EntanglementSolution.DoAdvance:
                            //Volatile.Write(ref Unsafe.As<Meta, long>(ref rollbackGuarantee), commitValue.PrimitivePackage);
                            InterlockedMeta.Write(ref rollbackGuarantee, commitValue);
                            Thread.Yield();
                            rollbackGuarantee = ref dummySlot;
#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(EntanglementSolution.DoAdvance)}");
                            //throw new Exception();
#endif
                            goto case EntanglementSolution.Continue;
                        case EntanglementSolution.DoRollback:

#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(EntanglementSolution.DoRollback)}{{ r:{commitValue}, a:{advancedValue}, pos:{pos}, curr:{current}}}");
                            //throw new Exception();
#endif
                            entry = advanceGuarantee.DataReader;
                            //Volatile.Write(ref Unsafe.As<Meta, long>(ref advanceGuarantee), kickedItem.PrimitivePackage);
                            InterlockedMeta.Write(ref advanceGuarantee, kickedMeta);
                            Thread.Yield();
                            // countermeasures against live lock
                            if (current.VolatileRead().HasEntanglement
                            && advanceGuarantee.VolatileRead().RawData == entry.RawData)
                                SpinWaitWhileHasEntanglementANDEqual(in current, in advanceGuarantee, entry
#if DEBUG
                                    , in rollbackGuarantee, in advanceGuarantee, in MemoryMarshal.GetReference(table)
#endif
                                    );

                            jumpType = advancedValue.DataReader.GetJumpType();
                            pos = table.Offset(ref advanceGuarantee);// to next
                            pos -= (int)jumpType;// retest
                            advancedValue = advancedValue.Update(advancedValue.DataReader.SubJump(jumpType));// retest end
                            currentTemp = advancedValue;// to next end

                            current = ref rollbackGuarantee;// to advance
                            kickedMeta = commitValue;
                            entry = commitValue.DataReader;// to advance end

                            rollbackGuarantee = ref dummySlot;
                            advanceGuarantee = ref dummySlot;
                            break;// logical swapped
                        case EntanglementSolution.VersionOver:
#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(EntanglementSolution.VersionOver)}{{ r:{commitValue}, a:{advancedValue}, pos:{pos}, curr:{current}}}");
#endif
                            InterlockedMeta.CompareRestore(ref rollbackGuarantee, new(advancedValue.KeyIndex, rollbackGuarantee.DataReader));
                            InterlockedMeta.CompareRestore(ref advanceGuarantee, kickedMeta);

                            return false;
                        case EntanglementSolution.FalseProbeEndToEnd:
#if DEBUG
                            //System.Diagnostics.Debug.WriteLine(nameof(EntanglementSolution) + "." + nameof(EntanglementSolution.FalseProbeEndToEnd));
                            System.Diagnostics.Debug.Fail($"{EntanglementSolution.FalseProbeEndToEnd}: {collisions}");
#endif
                            return false;
                        case EntanglementSolution.__DEBUG__Ravage:

#if DEBUG
                            System.Diagnostics.Debug.Fail($"{nameof(EntanglementSolution.__DEBUG__Ravage)}: {collisions}");
#endif
                            //Volatile.Write(ref Unsafe.As<Meta, long>(ref rollbackGuarantee), commitValue.PrimitivePackage);
                            //Volatile.Write(ref Unsafe.As<Meta, long>(ref advanceGuarantee), advancedValue.PrimitivePackage);
                            InterlockedMeta.Write(ref rollbackGuarantee, commitValue);
                            InterlockedMeta.Write(ref advanceGuarantee, advancedValue);
                            rollbackGuarantee = ref dummySlot;
                            advanceGuarantee = ref dummySlot;
                            goto case EntanglementSolution.Continue;

                        default:
                            goto case EntanglementSolution.FalseProbeEndToEnd;
                    }

                    break;// logical swapped path relay to move transaction

                    static EntanglementSolution EntanglementResolve(
                        scoped ReadOnlySpan<Meta> table,
                        scoped ref readonly Meta currentFocus, Meta currentComparand,
                        scoped ref readonly Meta rollback,
                        scoped ref readonly Meta advance,
                        ushort currentVersion)
                    {
#if DEBUG
                        //return EntanglementSolution.__DEBUG__Ravage;
#endif
                        if (Unsafe.AreSame(ref Unsafe.AsRef(in rollback), ref Unsafe.AsRef(in currentFocus)))
                            return EntanglementSolution.KickSelf;


                        if (Unsafe.AreSame(ref Unsafe.AsRef(in rollback), ref dummySlot))// stuck
                            return StuckWait(in currentFocus, currentComparand
#if DEBUG
                                , in rollback, in advance, in MemoryMarshal.GetReference(table)
#endif
                            );

                        if (Unsafe.AreSame(ref Unsafe.AsRef(in advance), ref Unsafe.AsRef(in currentFocus)))
                            return EntanglementSolution.FalseProbeEndToEnd;

                        static EntanglementSolution StuckWait(scoped ref readonly Meta focus, Meta focusComparand
#if DEBUG
                            , scoped ref readonly Meta own1, scoped ref readonly Meta own2, scoped ref readonly Meta offsetBase,
                            [CallerLineNumber] int line = default
#endif
                            )
                        {
#if DEBUG && !MUTE
                            System.Diagnostics.Debug.WriteLine($"{nameof(StuckWait)}: {{ thread:{Thread.CurrentThread.ManagedThreadId}, {focus}, from:{line}}}");
#endif
                            //var spin = new SpinWait();
                            //while (focus.VolatileRead() == focusComparand)
                            //{
                            //    if (spin.NextSpinWillYield)
                            //        Thread.Sleep(0);
                            //    else
                            //    {
                            //        Thread.Yield();
                            //        spin.SpinOnce();
                            //    }
                            //}
                            //spin.SpinOnce();
                            //Thread.Sleep(0);
                            var dummy = focus.VolatileRead();
                            SpinWaitWhileHasEntanglementANDEqual(in dummy, in focus, focusComparand.DataReader
#if DEBUG
                                , in own1, in own2, in offsetBase
#endif
                            );
                            return EntanglementSolution.Continue;
                        }

                        var spin = new SpinWait();
                        while (spin.NextSpinWillYield is false)
                        {
                            if (currentFocus.VolatileRead() != currentComparand)
                                return EntanglementSolution.Continue;
                            spin.SpinOnce();
                        }

                        return TraceLockedNode(table, in currentFocus, currentComparand, in rollback, in advance, currentVersion);

                        [MethodImpl(MethodImplOptions.NoInlining)]
                        static EntanglementSolution TraceLockedNode(
                            scoped ReadOnlySpan<Meta> table,
                            scoped ref readonly Meta currentFocus,
                            Meta currentComparand,
                            scoped ref readonly Meta rollback,
                            scoped ref readonly Meta advance,
                            ushort currentVersion
                        )
                        {
                            var tPos = table.Offset(in currentFocus);

                            if ((uint)tPos >= (uint)table.Length)
                                return EntanglementSolution.FalseProbeEndToEnd;

                            //var selfProbe = true;
                            scoped ref readonly var tCurrent = ref currentFocus;
                            var tCurrentTemp = currentComparand;
                            do
                            {
                                var tEntry = tCurrentTemp.DataReader;
                                var tVersion = tEntry.Version;

                                if (tVersion < currentVersion)
                                    return StuckWait(in currentFocus, currentComparand
#if DEBUG
                                            , in rollback, in advance, in MemoryMarshal.GetReference(table)
#endif
                                    );

                                var tJumpType = tEntry.GetJumpType();
                                var tJumpLength = (int)tJumpType;
                                //#if EARLYREHASH
                                //                                if (tEntry.Distance == 0)
                                //                                    tPos += table.RehashStep(tEntry, tJumpType);
                                //#endif
                                while (true)
                                {
                                    Thread.Yield();
#if DEBUG
                                    if (testBreaker)
                                        return EntanglementSolution.__DEBUG__Ravage;
#endif
                                    if (currentFocus.VolatileRead().PrimitivePackage != currentComparand.PrimitivePackage)
                                        return EntanglementSolution.Continue;

                                    tEntry = tEntry.AddJump(tJumpType);
                                    tCurrent = ref table.GetBucket(tPos += tJumpLength);
                                    tCurrentTemp = tCurrent.VolatileRead();

                                    if (tCurrentTemp.RawData > tEntry.RawData)
                                        continue;

                                    if (tCurrentTemp.RawData < tEntry.RawData)
                                    {
                                        if (tEntry.Version != tVersion)
                                        {
                                            if ((uint)RestoreOverranEntry(
                                                table,
                                                tJumpType,
                                                tVersion,
                                                ref tEntry,
                                                ref tPos
                                            ) >= (uint)table.Length)
                                                return StuckWait(in currentFocus, currentComparand
#if DEBUG
                                                , in rollback, in advance, in MemoryMarshal.GetReference(table)
#endif
                                                );
                                            continue;
                                        }

                                        if (Unsafe.AreSame(ref Unsafe.AsRef(in rollback), ref Unsafe.AsRef(in tCurrent)))
                                            return EntanglementSolution.DoAdvance;

                                        if (Unsafe.AreSame(ref Unsafe.AsRef(in advance), ref Unsafe.AsRef(in tCurrent)))
                                            return EntanglementSolution.DoRollback;
                                        if (tCurrentTemp.HasEntanglement)
                                            break;
                                        else
                                            return StuckWait(in currentFocus, currentComparand
#if DEBUG
                                            , in rollback, in advance, in MemoryMarshal.GetReference(table)
#endif
                                            );
                                    }
                                }
                            } while (true);

                        }
                    }
                }// entanglement resolve

                if (currentTemp.DataReader.Version != version)// end at lesser version
                {
                    if (currentTemp == InterlockedMeta.CompareExchange(
                        ref current,
                        kickedMeta.Update(entry),
                        currentTemp)
                    )
                    {
                        //Volatile.Write(ref Unsafe.As<Meta, long>(ref rollbackGuarantee), commitValue.PrimitivePackage);
                        //Volatile.Write(ref Unsafe.As<Meta, long>(ref advanceGuarantee), advancedValue.PrimitivePackage);
                        InterlockedMeta.Write(ref rollbackGuarantee, commitValue);
                        InterlockedMeta.Write(ref advanceGuarantee, advancedValue);
                        return true;
                    }
                }
                else// swap
                {
                    if (currentTemp == InterlockedMeta.CompareSetEntanglement(
                        ref current,
                        currentTemp)
                    )
                        break;// swapped
                }

                // retest

#if DEBUG
                collisions--;
#endif
                pos -= jump;
                entry = entry.SubJump(jumpType);
                continue;
            } while (true);// end loop

            // move transaction

      
            InterlockedMeta.Write(ref rollbackGuarantee, commitValue);
            rollbackGuarantee = ref advanceGuarantee;
            commitValue = advancedValue;

            advanceGuarantee = ref current;
            advancedValue = kickedMeta.Update(entry);

            kickedMeta = currentTemp;
#if DEBUG
            if (commitValue.HasEntanglement || advancedValue.HasEntanglement || kickedMeta.HasEntanglement)
                System.Diagnostics.Debug.Fail($"{commitValue}, {advancedValue}, {kickedMeta}");

            if (collisionCount < collisions)
            {
                collisionCount = collisions;
#if !MUTE
                System.Diagnostics.Debug.WriteLine($"New collision record: {collisions} at {DateTime.Now.ToString("hh:mm:ss:fff")}");
#endif
            }

            swaps++;
            if (swapCount < swaps)
            {
                swapCount = swaps;
#if !MUTE
                System.Diagnostics.Debug.WriteLine($"New swap record: {swaps} at {DateTime.Now.ToString("hh:mm:ss:fff")}");
#endif
            }
            if (hashCollisionCount < hashes)
            {
                hashCollisionCount = hashes;
#if !MUTE
                System.Diagnostics.Debug.WriteLine($"New hash collision record: {hashes} at {DateTime.Now.ToString("hh:mm:ss:fff")}");
#endif
            }
#endif
        }

            // insertion is guaranteed to succeed and consistency by the caller
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void PromptInsertRangeInPrivate<TKey, UKey>(
        this scoped Span<Meta> table,
        TKey[] keysArray,
        UKey[] secondaryKeysArray,
        ushort version,
        int startIndex,
        int length
        )
        where TKey : notnull
        where UKey : notnull
    {
        if ((uint)length <= (uint)keysArray.Length)
            table.PromptInsertRangeInPrivate(
                new ReadOnlySpan<TKey>(keysArray, 0, length),
                version,
                startIndex
            );
        else if ((uint)length <= (uint)secondaryKeysArray.Length)
            table.PromptInsertRangeInPrivate(
                new ReadOnlySpan<UKey>(secondaryKeysArray, 0, length),
                version,
                startIndex
            );
#if DEBUG
        else
            throw new ArgumentOutOfRangeException(nameof(length), $"The {nameof(length)} is out of range of both arrays.");
#endif
    }

    internal static void PromptInsertRangeInPrivate<TKey>(
        this scoped Span<Meta> table,
        scoped ReadOnlySpan<TKey> allKeys,
        ushort version,
        int startOffset = 0)
        where TKey : notnull
    {
        const string throwMsg = $"{nameof(table)} was published";
        for (var i = startOffset; (uint)i < (uint)allKeys.Length; ++i)
        {
            //var hash = allKeys[i].GetHashCode();
//            scoped ref var slot = ref table.GetBucket(hash);
//            var entry = Meta.Data.CreateEntry(hash, version);

//        retry:;
//            var initial = slot.VolatileRead();
//#if DEBUG
//            if (initial.HasEntanglement)
//                throw new InvalidOperationException(throwMsg);
//#endif
//            if (initial.DataReader.Version >= version)
                //InsertInBlind(table, hash, ref slot, i, entry, version);
            //else if (initial != InterlockedMeta.CompareExchange(
            //    ref slot,
            //    new(i, entry),
            //    initial
            //))
            //    goto retry;

            var hash = allKeys[i].GetHashCode();
            InsertInBlind(table, hash, i, Meta.Data.CreateEntry(hash, version));
        }

        return;

        static void InsertInBlind(scoped Span<Meta> table, int pos, int keyIndex, Meta.Data entry)
        {
            var maskCache = table.GetBucketMask();
            var version = entry.Version;
            var jumpType = entry.GetJumpType();
            var jump = (int)jumpType;
//#if EARLYREHASH
//            pos += table.RehashStep(entry, jumpType);
//#endif
            Meta currentTemp;

            while (true)
            {
                entry = entry.AddJump(jumpType);
                scoped ref var current = ref table.GetBucket(pos += jump, maskCache);

                currentTemp = current.VolatileRead();

#if DEBUG
                if (currentTemp.HasEntanglement)
                    throw new InvalidOperationException(throwMsg);
#endif

                if (currentTemp.RawData >= entry.RawData)
                    continue;

                if (entry.Version != version)
                {
                    if ((uint)RestoreOverranEntry(
                        table,
                        jumpType,
                        version,
                        ref entry,
                        ref pos
                    ) >= (uint)table.Length)
                    {
                        Throw();
                        static void Throw()
                            => throw new Exception();
                    }
                    continue;
                }

                if (currentTemp == InterlockedMeta.CompareExchange(ref current, new(keyIndex, entry), currentTemp))
                {
                    if(currentTemp.DataReader.Version != version)
                        return;
                    else
                    {
                        keyIndex = currentTemp.KeyIndex;
                        entry = currentTemp.DataReader;

                        jumpType = entry.GetJumpType();
                        jump = (int)jumpType;
//#if EARLYREHASH
//                        if (entry.Distance == 0)
//                            pos += table.RehashStep(entry, jumpType);
//#endif
                        continue;
                    }
                }

                entry = entry.SubJump(jumpType);
                pos -= jump;
                continue;
            }
        }
    }

    // tests
    internal static bool testBreaker { get; set; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, (int threadID, nint to, string name)> entanglementTracer = new();
    internal static void ResetEntanglementTracer() => entanglementTracer.Clear();
    internal static void Test(this scoped Span<Meta> table, int keyIndex, int hash, ushort version)
    {
        //var slotTemp = table.GetBucket(hash);
        //var entry = Meta.Data.CreateEntry(hash, version);
        //if (slotTemp.HasEntanglement)
        //    slotTemp = SpinWaitWhileHasEntanglement(in table.GetBucket(hash));
        //var resultMock = MetaSlotResult.Progress(
        //    table.GetBucketIndex(hash),
        //    slotTemp.RawData < entry.RawData ? slotTemp : default,
        //    entry
        //);
        //var dummyHeader = new TableHeader(version);
        //table.Insert(resultMock, keyIndex, ref dummyHeader, default);
    }

    //internal static System.Collections.Generic.List<(int index, Meta meta, int to)> Check(this scoped Span<Meta> table)
    //{
    //    var list = new System.Collections.Generic.List<(int index, Meta meta, int to)>(table.Length / 10);
    //    for (var i = 0; i < table.Length; i++)
    //    {
    //        var meta = table[i];
    //        if (meta.HasEntanglement)
    //            list.Add(
    //                (
    //                    i,
    //                    meta,
    //                    table.PromptUpdateKeyIndex(
    //                        i + (int)meta.DataReader.GetJumpType(),
    //                        new(meta.KeyIndex, meta.DataReader.AddJump(meta.DataReader.GetJumpType())),
    //                        meta.KeyIndex
    //                    ).SlotIndex
    //                )
    //            );
    //    }
    //    return list;
    //}

}
internal class TableModelMock(int size, int step)
{
    public readonly int size = size;
    public readonly int step = step;
    public readonly System.Collections.Generic.SortedSet<int> ss = new();
    public readonly int[] table = new int[1 << size];
    public int[] Table 
        => this.table;
    public void Do()
    {
#if !NET6_0_OR_GREATER
        var Random = new { Shared = new Random() };
#endif
        var ss = this.ss;
        var table = this.table;


        // full scan test size = 0..30 step = odd

        ss.Clear();
        var len = 1 << this.size;
        var jump = this.step;
        var entry = 0;
        var v = Random.Shared.Next();
        var h = Random.Shared.Next();
        var start = v;
        var count = 0;
        for (var i = 0; i < len; i++)
        {
            v += (int)(((h * (len >>> 8)) & ~1) * (jump * 256));
            entry += jump;
            if (ss.Add((v += jump) & (len - 1)))
                table[v & (len - 1)] = entry;
            else
                count++;

            for (; (entry & 255) != 0; entry += jump, i++)
                if (ss.Add((v += jump) & (len - 1)))
                    table[v & (len - 1)] = entry;
                else
                    count++;
        }
        var dup = count;
        count = 0;
        var prev = ss.Min;
        foreach (var vv in ss)
        {
            if ((prev + 1) != vv)
            {
                Console.WriteLine(vv);
                count++;
            }
            prev = vv;
        }
        Console.WriteLine($"duplicate: {dup} count: {count} ss: {ss.Count} h: {h} s:{start}, allowable:{dup < 256}");
    }
}