#define SAFEBUCKET
using FlatDictionary.InternalModules.Atomic;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlatDictionary.InternalModules;

internal static class MetaTableOperateExtensions
{
    const int overLengthMultiply = 17;
    
    /// <summary>
    /// get index of initial or insertedIndex of <paramref name="insertingIndex"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="table"></param>
    /// <param name="key"></param>
    /// <param name="version"></param>
    /// <param name="insertingIndex"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int InitialProbe<TKey>(
        this Span<Meta> table, TKey key,
        ushort version, int insertingIndex,
        scoped out ProveContext nextProveContext
    )
        where TKey : notnull
    {
        var hash =
            key.GetHashCode();
        ref var initial = 
            //ref Unsafe.Add(ref MemoryMarshal.GetReference(table), hash & (table.Length - 1));
            ref table.GetBucket(hash);
        if (initial.DataReader.Version != version)
            initial = new Meta(insertingIndex, Meta.Data.CreateEntry(hash, version));
        nextProveContext = new(hash, true);
        return initial.KeyIndex;
    }
    /// <summary>
    /// get index of initial or -1
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="table"></param>
    /// <param name="key"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int InitialProbe<TKey>(
        this Span<Meta> table, TKey key,
        ushort version, out ProveContext nextProveContext
    )
        where TKey : notnull
    {
        const int invalidIndex = -1;
        var hash =
            key.GetHashCode();
        var initial =
            //Unsafe.Add(ref MemoryMarshal.GetReference(table), hash & (table.Length - 1));
            table.GetBucket(hash);
        nextProveContext = new(hash, true);
        return initial.DataReader.Version == version
            ? initial.KeyIndex
            : invalidIndex;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Offset(this scoped Span<Meta> table, scoped ref readonly Meta slot)
        => unchecked((int)
            (Unsafe.ByteOffset(ref MemoryMarshal.GetReference(table), ref Unsafe.AsRef(in slot))
                / (nint)(uint)Unsafe.SizeOf<Meta>())
        );
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Offset(this scoped ReadOnlySpan<Meta> table, scoped ref readonly Meta slot)
        => unchecked((int)
            (Unsafe.ByteOffset(ref MemoryMarshal.GetReference(table), ref Unsafe.AsRef(in slot))
                / (nint)(uint)Unsafe.SizeOf<Meta>())
        );
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBucketIndex(this scoped Span<Meta> table, int hash)
        => hash & (table.Length - 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBucketIndex(this scoped ReadOnlySpan<Meta> table, int hash)
        => hash & (table.Length - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBucketIndex(this MetaTable table, int hash)
        => hash & (table.Size - 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref Meta GetBucket(this Span<Meta> table, int hash)
        =>
#if SAFEBUCKET
            ref table[hash & (table.Length - 1)];
#else
        ref Unsafe.Add(ref MemoryMarshal.GetReference(table), hash & (table.Length - 1));
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref Meta GetBucket(this MetaTable table, int hash)
#if SAFEBUCKET || !NET6_0_OR_GREATER
    //{
    //    var idx = MetaTable.HeaderOffset + (hash & (table.Size - 1));
    //    var arr = table.Source;
    //    if ((uint)idx < (uint)arr.Length)
    //        return ref arr[idx];
    //    else
    //        return ref Throw();
    //    ref Meta Throw()
    //        => throw new IndexOutOfRangeException();
    //}
    => ref table.Source[MetaTable.HeaderOffset + (hash & (table.Size - 1))];
#else
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(table.Source), MetaTable.HeaderOffset + (hash & (table.Size - 1)));
#endif
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    internal static ref Meta GetBucket(this Span<Meta> table, int hash, scoped out int roundedIndex)
    //    =>
    //#if SAFEBUCKET
    //        ref table[roundedIndex = hash & (table.Length - 1)];
    //#else
    //        ref Unsafe.Add(ref MemoryMarshal.GetReference(table), roundedIndex = hash & (table.Length - 1));
    //#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBucketMask(this scoped Span<Meta> table)
    {
        var length = table.Length;
        if((length & (-length)) == length
            && length != 0)
            return length - 1;
        else
            return Throw();
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Throw()
            => throw new ArgumentException($"{nameof(table)}.{nameof(table.Length)} is not power of two or zero");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetBucketMask(this scoped ReadOnlySpan<Meta> table)
    {
        var length = table.Length;
        if ((length & (-length)) == length
            && length != 0)
            return length - 1;
        else
            return Throw();
        int Throw()
            => throw new ArgumentException($"{nameof(table)}.{nameof(table.Length)}:{length} is not power of two or zero");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref Meta GetBucket(this Span<Meta> table, int hash, int cachedMask)
        =>
#if SAFEBUCKET
            ref table[hash&cachedMask];
#else
        ref Unsafe.Add(ref MemoryMarshal.GetReference(table), hash & cachedMask);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly Meta GetBucket(this ReadOnlySpan<Meta> table, int hash)
        => ref table.GetBucket(hash, table.Length - 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly Meta GetBucket(this ReadOnlySpan<Meta> table, int hash, int cachedMask)
    =>
#if SAFEBUCKET
        ref table[hash & cachedMask];
#else
        ref Unsafe.Add(ref MemoryMarshal.GetReference(table), hash & cachedMask);
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref Meta GetBucket(this Span<Meta> table, int hash, int cachedMask, scoped out int roundedIndex)
    {
        var index = hash & cachedMask;
        roundedIndex = index;
        return
#if SAFEBUCKET
            ref table[index];
#else
            ref Unsafe.Add(ref MemoryMarshal.GetReference(table), index);
#endif

    }


    internal static int FindOrInserted<TKey>(
        this Span<Meta> table, scoped ReadOnlySpan<TKey> keys,
        TKey key, ushort version,
        int insertingIndex, InsertBehavior behavior,
        scoped out bool exist,
        scoped out int collisionCount
    )
    where TKey : notnull, IEquatable<TKey>
    {
        var index = table.InitialProbe(key, version, out var context);
        if (((uint)index < (uint)keys.Length
            && keys[index].Equals(key)))
        {
            exist = true;
            collisionCount = 0;
            return index;
        }

        return table.FindOrInserted(
            keys,
            key,
            context,
            version,
            insertingIndex,
            behavior,
            out exist,
            out collisionCount
        );
    }
    internal static int FindOrInserted<TKey>(
        this Span<Meta> table, scoped ReadOnlySpan<TKey> keys,
        TKey key, ProveContext context, ushort version,
        int insertingIndex, InsertBehavior behavior,
        scoped out bool exist,
        scoped out int collisionCount
    )
    where TKey : notnull, IEquatable<TKey>
    {
        
        var pos = context.Hash;
        ref var tableRef = ref MemoryMarshal.GetReference(table);
        var entry = Meta.Data.CreateEntry(pos, version);
        var jumpType = entry.GetJumpType();
        var jump = (int)jumpType;
        long versionComparer;

        var collisionCountL = 0;

        var maskCache = table.GetBucketMask();
        ref var tombstone = ref Unsafe.NullRef<Meta>();
        ref var current =
#if SAFEBUCKET
            ref table.GetBucket(pos, maskCache, out pos);
#else
            ref Unsafe.Add(ref tableRef, pos &= maskCache);
#endif
        Meta currentTemp = current;
        int indexOfKeys;

        if (currentTemp.RawData < entry.RawData)
            goto notFoundResult;

        pos +=
            HashCode.Combine(entry.RawData);
        //entry.Hash;

        //versionComparer = ((long)entry.Version + 1) << Meta.Data.VersionOffset;
        throw new NotImplementedException();
#if DEBUG
        int versionEquality = entry.Version;
        for (var safe = 0; safe < table.Length; ++safe)
#else
        while (true)
#endif
        {
            collisionCountL++;
            entry = entry.AddJump(jumpType);
            current =
#if SAFEBUCKET
                ref table.GetBucket(pos + jump, maskCache, out pos);
#else
                ref Unsafe.Add(ref tableRef, pos = (pos + jump) & maskCache);
#endif
            currentTemp = current;

            if (currentTemp.RawData > entry.RawData)
                continue;
            if (currentTemp.RawData == entry.RawData)
            {
                indexOfKeys = currentTemp.KeyIndex;
                if ((uint)indexOfKeys >= (uint)keys.Length)
                {
                    indexOfKeys ^= Meta.UnreachableFlag;
                    if ((uint)indexOfKeys >= (uint)keys.Length)// int.MaxValue | less then keys.Length
                    {
                        tombstone = ref current;
                        continue;
                    }
                    else if (!keys[indexOfKeys].Equals(key))
                    {
                        indexOfKeys = SpinWait(ref current);

                        [MethodImpl(MethodImplOptions.NoInlining)]
                        static int SpinWait(ref Meta slot)
                        {
                            int index;
                            while (((index = slot.KeyIndex) & Meta.UnreachableFlag) != 0)
                                if(!Thread.Yield())
                                    Thread.Sleep(0);
                            return index;
                        }
                        continue;
                    }
                }
                else if (!keys[indexOfKeys].Equals(key))
                {
                    continue;
                }

                //if (behavior == InsertBehavior.SetTombstone)
                //    current = currentTemp.SetTombstone();
                collisionCount = collisionCountL;
                exist = true;
                return indexOfKeys;
            }
            else if (entry.RawData >= versionComparer)
            {
                //retesting
                entry = entry.SubJump(jumpType);
                pos -= jump;

                jumpType = JumpType.None;
                jump *= overLengthMultiply;
                continue;
            }

#if DEBUG
            if (versionEquality != entry.Version)
                throw new Exception("versionEquality != entry.Version");
            if (collisionCountL > table.Length)
                throw new InvalidOperationException($"Infinite loop detected during insertion, check your hash function or {nameof(table)} size.");
#endif
            break;
        }

    notFoundResult:;

        collisionCount = collisionCountL;
        exist = false;
        if (behavior < InsertBehavior.Insert)// none = 0, setTombstone = 1 < insert
            return -1;

        var insertItem = new Meta(insertingIndex, entry);
        //versionComparer = ((long)version) << Meta.Data.VersionOffset;
        throw new NotImplementedException();
        current = insertItem;

        if ((currentTemp.RawData - versionComparer) < 0)// less version
            return insertingIndex;
        if(!Unsafe.IsNullRef(ref tombstone))// tombstone
        {
            tombstone = tombstone.UpdateKeyIndex(insertingIndex);
            return insertingIndex;
        }

        insertItem = currentTemp;

        entry = insertItem.DataReader;
        jumpType = entry.GetJumpType();
        jump = (int)jumpType;

        collisionCountL = 0;

        long limit = Meta.Data.MaxCountableDistance - entry.DynamicHash;
        if (limit == 0)
        {
            jump *= overLengthMultiply;
            jumpType = JumpType.None;
        }

        if (limit == Meta.Data.MaxCountableDistance)
            pos +=
                HashCode.Combine(entry.RawData);
        //entry.Hash;

        do
        {
            while (true)
            {
                collisionCountL++;
                entry = entry.AddJump(jumpType);
                current =
#if SAFEBUCKET
                    ref table.GetBucket(pos + jump, maskCache, out pos);
#else
                    ref Unsafe.Add(ref tableRef, pos = (pos + jump) & maskCache);
#endif
                currentTemp = current;
#if DEBUG
                if (collisionCountL > table.Length)
                    throw new InvalidOperationException("no capacity");
#endif
                limit = unchecked(limit - jump);
                if (limit == 0)
                {
                    jump *= overLengthMultiply;
                    jumpType = JumpType.None;
                }

                if (currentTemp.RawData > entry.RawData)
                    continue;

                if ((currentTemp.RawData - versionComparer) < 0 // less version
                    || (uint)currentTemp.KeyIndex >= (uint)keys.Length) // tombstone
                {
                    current = insertItem.Update(entry);
                    collisionCount = Math.Max(collisionCountL, collisionCount);
                    return insertingIndex;
                }
                else
                {
                    current = insertItem.Update(entry);
                    insertItem = currentTemp;
                    break;
                }
            }

            collisionCountL = 0;
            entry = insertItem.DataReader;
            limit = Meta.Data.MaxCountableDistance - entry.DynamicHash;
            jumpType = entry.GetJumpType();
            jump = (int)jumpType;

            if (limit == 0)
            {
                jump *= overLengthMultiply;
                jumpType = JumpType.None;
            }

            if (limit == Meta.Data.MaxCountableDistance)
                pos +=
                    HashCode.Combine(entry.RawData);
            //entry.Hash;


        } while (true);
    }

    internal static void Rehash<TKey>(
        this Span<Meta> table, ReadOnlySpan<TKey> uniqueKeys,
        ushort version
    )
    where TKey : notnull, IEquatable<TKey>
    {
        bool exist;
        int mask = table.GetBucketMask();
        int keyIndex;
        ProveContext context;
        for (var i = 0; i < uniqueKeys.Length; i++)
        {
            keyIndex = table.InitialProbe(uniqueKeys[i], version, i, out context);
            if (keyIndex == i)
                continue;
            else
            {
                table.FindOrInserted(
                    uniqueKeys,
                    uniqueKeys[i],
                    context,
                    version,
                    i,
                    InsertBehavior.Insert,
                    out exist,
                    out _
                );
                if (exist)
                    ThrowDuplicateKeys();
            }

            static void ThrowDuplicateKeys() => throw new InvalidOperationException($"{nameof(uniqueKeys)} contains non unique items");

        }
    }

    //public static ReadOnlySpan<KeyIndexSearchMeta> AsKeyIndexSearch(this Span<Meta> container)
    //    => MemoryMarshal.Cast<Meta, KeyIndexSearchMeta>(container);

//    [Obsolete]
//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    internal static MetaSlotResult FindOrInsertPoint<TItem, TKey>(
//           this scoped ReadOnlySpan<Meta> table,
//           scoped ReadOnlySpan<TItem> items,
//           TKey key,
//           int hash,
//           ushort version,
//           MetaKeyIndexBehavior behavior = default,
//           Meta inadequateRead = default
//       )
//           where TKey : notnull, IEquatable<TItem>
//    {
//        //if (table.Length == 0)
//        //    return MetaSlotResult.Failed;
//        var maskCache = table.GetBucketMask();

//        var entry = Meta.Data.CreateEntry(hash, version);
//        var jumpType = entry.GetJumpType();
//        var jump = (int)jumpType;

//        (SlotIndex pos, Meta currentTemp, Meta.Data entry) unchartedSlotSnapshot = (new(hash & maskCache), inadequateRead, entry);
//#if ENABLE_TOMBSTONE
//#endif
//        (int pos, Meta.Data entry) postponedSnapshot = default;
//        var pos = hash
//#if EARLYREHASH
//            + table.RehashStep(entry, jumpType);
//#endif
//            ;


//        scoped ref readonly Meta current = ref Unsafe.NullRef<Meta>();
//        Meta currentTemp;
//        int indexOfItems;

//        var read = 0;
//#if DEBUG
//        var read_debug = 0;
//#endif
//        while (true)
//        {
//#if DEBUG
//            read_debug++;
//#endif

//            entry = entry.AddJump(jumpType);
//            current = ref table.GetBucket(pos += jump, maskCache);

//            currentTemp = current.VolatileRead();

//            if (currentTemp.RawData > entry.RawData)
//                continue;

//            if (currentTemp.RawData == entry.RawData)
//            {
//                indexOfItems = currentTemp.KeyIndex;
//                if ((uint)indexOfItems < (uint)items.Length)
//                {
//                    var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//                    if (key.Equals(item))
//                        return MetaSlotResult.Success(pos & maskCache, currentTemp);
//                }
//                else
//                {
//                    indexOfItems = currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//                    if ((uint)indexOfItems < (uint)items.Length)
//                    {
//                        if ((behavior & MetaKeyIndexBehavior.UnreachedVisible) != default)
//                        {
//                            var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//                            if (key.Equals(item))
//                                //if ((behavior & MetaKeyIndexBehavior.ContinueUntilBetter) == default)
//                                return MetaSlotResult.Success(pos & maskCache, currentTemp);
//                            //else
//                            //    unchartedSlotSnapshot = (
//                            //        new(pos & maskCache),
//                            //        currentTemp,
//                            //        default
//                            //    );
//                        }
//                    }
//                    else if (currentTemp.HasEntanglement)
//                    {
//                        if (postponedSnapshot.entry.IsDefault)
//                            postponedSnapshot = (pos, entry);
//                    }
//                    else// read past
//                    {

//#if ENABLE_TOMBSTONE
//                        if (unchartedSlotSnapshot.currentTemp.HasTombstone is false
//                        && unchartedSlotSnapshot.currentTemp.HasEntanglement is false)
//                        if (unchartedSlotSnapshot.currentTemp.IsDefault)
//                            unchartedSlotSnapshot = (
//                                new(
//                                    pos & maskCache,
//                                currentTemp.HasTombstone is false
//                                    true
//                                ),
//                                currentTemp,
//                                entry);
//#endif
//                    }
//                }
//                continue;
//            }

//            if (entry.Version != version)
//            {
//#if DEBUG
//                System.Diagnostics.Debug.Assert((uint)read_debug < (uint)table.Length);
//#endif
//                read += RestoreOverranEntry(
//                    table,
//                    jumpType,
//                    version,
//                    ref entry,
//                    ref pos
//                );

//                if ((uint)read > (uint)table.Length)
//                    return MetaSlotResult.Failed;

//                continue;
//            }
//#if DEBUG
//            System.Diagnostics.Debug.Assert(entry.Distance != 0);
//#endif
//            if (postponedSnapshot.entry.IsDefault)
//            {
//                if (currentTemp.HasEntanglement is false)
//                    break;
//            }
//            else
//            {
//                (pos, entry) = postponedSnapshot;
//                current = ref table.GetBucket(pos, maskCache);
//            }

//            if (current.VolatileRead().HasEntanglement)
//                SpinWaitWhileHasEntanglement(in current
//#if DEBUG
//                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
//#endif
//                );
//            postponedSnapshot = default;

//            // retesting
//            entry = entry.SubJump(jumpType);
//            pos -= jump;
//            read = 0;
//            continue;
//        }// endless

//#if DEBUG
//        indexOfItems = unchartedSlotSnapshot.currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//        if ((uint)indexOfItems < (uint)items.Length
//        && behavior.HasFlag(MetaKeyIndexBehavior.UnreachedVisible))
//        {
//            var item = AtomicHelper<TItem>.BulkLoad(in items[indexOfItems]);
//            if (key.Equals(item))
//                throw new Exception();
//        }

//#endif

//        SlotIndex slotIndex;
//#if ENABLE_TOMBSTONE
//    if (unchartedSlotSnapshot.entry.IsDefault)
//    {
//            (pos, currentTemp, entry) = unchartedSlotSnapshot;
//            slotIndex = new(pos & maskCache);
//    }
//    else
//#endif
//        if ((uint)(unchartedSlotSnapshot.currentTemp.KeyIndex & ~Meta.UnreachableFlag) < (uint)items.Length // not read past
//        || unchartedSlotSnapshot.currentTemp.IsDefault)
//            slotIndex = new(pos & maskCache);
//        else
//        {
//#if DEBUG
//            System.Diagnostics.Debug.Assert(entry.Distance == 0 || unchartedSlotSnapshot.currentTemp.IsDefault is false);
//#endif
//            //            if (unchartedSlotSnapshot.entry.Distance == 0)
//            //            {

//            //#if DEBUG
//            //                indexOfItems = unchartedSlotSnapshot.currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//            //                System.Diagnostics.Debug.Assert((uint)indexOfItems >= (uint)items.Length);
//            //#endif
//            //                if (unchartedSlotSnapshot.currentTemp.HasTombstone
//            //                && table.GetBucket(unchartedSlotSnapshot.pos, maskCache).VolatileRead().HasEntanglement)
//            //                    table.SpinWaitWhileHasEntanglement(pos);

//            //                return MetaSlotResult.Progress(new SlotIndex(pos & maskCache, true), currentTemp, entry);
//            //            }

//            if (unchartedSlotSnapshot.currentTemp.HasEntanglement)
//            {
//#if DEBUG
//                indexOfItems = unchartedSlotSnapshot.currentTemp.KeyIndex & ~Meta.UnreachableFlag;
//                System.Diagnostics.Debug.Assert((uint)indexOfItems >= (uint)items.Length);
//#endif
//                if (table.GetBucket(unchartedSlotSnapshot.pos, maskCache).VolatileRead().HasEntanglement)
//                    table.SpinWaitWhileHasEntanglement(pos);
//                return MetaSlotResult.Progress(new SlotIndex(pos & maskCache, true), currentTemp, entry);
//            }
//            (slotIndex, currentTemp, entry) = unchartedSlotSnapshot;

//            if (entry.IsDefault is false)
//                slotIndex = slotIndex.SetReadPastFlag();
//        }
//        // if entry was default, MetaSlotResult.Progress() treats it as an success result.
//#if DEBUG
//        System.Diagnostics.Debug.Assert(
//            entry.IsDefault is false
//            || MetaSlotResult.Progress(table.GetBucketIndex(pos), currentTemp, entry)
//                == MetaSlotResult.Success(table.GetBucketIndex(pos), currentTemp)
//        );

//        System.Diagnostics.Debug.Assert(
//            !slotIndex.HasReadPastFlag
//            || (uint)(currentTemp.KeyIndex & ~Meta.UnreachableFlag) >= (uint)items.Length
//        );
//#endif
//        return MetaSlotResult.Progress(slotIndex, currentTemp, entry);
//    }
}

