using FlatDictionary.InternalModules;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

//namespace HashIndex.InternalModules;

//internal static class MetaTableConcurrentOperateExtensionsObsoleted
//{


//internal static MetaSlotResult PromptUpdateKeyIndex(
//    this scoped Span<Meta> table,
//    int keyIndex,
//    int hash,
//    ushort version,
//    int newKeyIndex
//)
//{
//    var target = new Meta(keyIndex, Meta.Data.CreateEntry(hash, version));
//    Meta read;
//    if (target
//        == (read = InterlockedMeta.CompareExchange(
//            ref table.GetBucket(hash),
//            target.UpdateKeyIndex(newKeyIndex),
//            target
//        ))
//    )
//        return MetaSlotResult.Success(table.GetBucketIndex(hash), target);

//    return Deep(
//        table,
//        hash,
//        target,
//        newKeyIndex,
//        read
//    );

//    static MetaSlotResult Deep(
//        scoped Span<Meta> table,
//        int pos,
//        Meta target,
//        int newKeyIndex,
//        Meta read
//    )
//    {
//        if (read.HasEntanglement)
//            SpinWaitWhileHasEntanglement(in table.GetBucket(pos)
//#if DEBUG
//                , in dummySlot, in dummySlot, in MemoryMarshal.GetReference(table)
//#endif
//            );
//        while (true)
//        {
//            var result = table.FindByKeyIndex(target.KeyIndex, pos, target.DataReader);
//            if (result.IsSuccessful is false)
//                return result;
//            if (result.Slot
//                == InterlockedMeta.CompareExchange(
//                    ref table.GetBucket(result.SlotIndex),
//                    new(newKeyIndex, result.Slot.DataReader),
//                    result.Slot
//                )
//            )
//                return result;
//            else
//                continue;
//        }
//        //            var maskCache = table.GetBucketMask();

//        //            scoped ref var current = ref Unsafe.NullRef<Meta>();
//        //            if(currentTemp.HasEntanglement)
//        //            {
//        //                current = ref table.GetBucket(pos, maskCache);
//        //                do
//        //                {
//        //                    SpinWaitWhileHasEntanglement(in current);
//        //                    currentTemp = InterlockedMeta.CompareExchange(
//        //                        ref current,
//        //                        target.UpdateKeyIndex(newKeyIndex),
//        //                        target
//        //                    );
//        //                    if (currentTemp == target)
//        //                        return MetaSlotResult.Success(pos & maskCache, target);
//        //                } while (currentTemp.HasEntanglement);
//        //            }

//        //            var entry = target.DataReader;
//        //            var jumpType = entry.GetJumpType();
//        //            var jump = (int)jumpType;
//        //            var version = entry.Version;
//        //            (int pos, Meta.Data entry) postponedSnapshot = default;
//        //            var read = 0;
//        //#if EARLYREHASH
//        //            if (entry.Distance == 0)
//        //                pos += InitialEarlyRehashStep(entry);
//        //#endif
//        //            while (true)
//        //            {
//        //#if DEBUG
//        //                read++;
//        //#endif

//        //                entry = entry.AddJump(jumpType);
//        //                current = ref table.GetBucket(pos += jump, maskCache);

//        //                currentTemp = IntPtr.Size == Unsafe.SizeOf<Meta>()
//        //                    ? current
//        //                    : current.VolatileRead();


//        //                if (currentTemp.RawData > entry.RawData)
//        //                    continue;

//        //                if (currentTemp.HasEntanglement)
//        //                {
//        //                    SpinWaitWhileHasEntanglement(in current);

//        //                    entry = entry.SubJump(jumpType);
//        //                    pos -= jump;
//        //                    continue;
//        //                }

//        //                if (currentTemp.RawData < entry.RawData)
//        //                {
//        //                    if (entry.Version != version)
//        //                    {
//        //#if !DEBUG
//        //                        read +=
//        //#endif
//        //                            RestoreOverranEntry(
//        //                                table
//        //                                jumpType,
//        //                                version,
//        //                                ref entry,
//        //                                ref pos,
//        //                                out _
//        //                            );

//        //                        if ((uint)read > (uint)table.Length + Meta.Data.MaxCountableDistance)
//        //                            return MetaSlotResult.Failed;

//        //                        continue;
//        //                    }
//        //                    if(postponedSnapshot.entry.IsDefault)
//        //                        return MetaSlotResult.Progress(pos & maskCache, currentTemp, entry);

//        //                    (pos, entry) = postponedSnapshot;
//        //                    postponedSnapshot = default;
//        //                    pos -= jump;
//        //                    entry = entry.SubJump(jumpType);

//        //                    current = ref table.GetBucket(pos, maskCache);

//        //                    if(current.VolatileRead().HasEntanglement)
//        //                        SpinWaitWhileHasEntanglement(in current);
//        //                    continue;
//        //                }

//        //                if (currentTemp.KeyIndex == target.KeyIndex)
//        //                {
//        //                    if (currentTemp
//        //                        == (currentTemp = InterlockedMeta.CompareExchange(
//        //                            ref current,
//        //                            target.UpdateKeyIndex(newKeyIndex),
//        //                            currentTemp
//        //                        ))
//        //                    )
//        //                        return MetaSlotResult.Success(pos & maskCache, currentTemp);
//        //                }

//        //                if (currentTemp.HasEntanglement
//        //                && postponedSnapshot.entry.IsDefault)
//        //                    postponedSnapshot = (pos, entry);

//        //                continue;
//        //            }// endless
//    }
//}

//    private const int skipFirstInsertedSlotIndex = int.MaxValue;
//    private const int dummyFirstInsertedSlotFlag = int.MinValue;
//    [Obsolete]
//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    internal static int Insert(
//       this scoped Span<Meta> table,
//       MetaSlotResult insertPointResult,
//       int commitKeyIndex,
//       scoped ref TableHeader header,
//       TableHeader.LockTaken.ByWriterCount lockTaken)
//    {
//        // fast path
//        if ((insertPointResult.Slot.DataReader.Version < insertPointResult.Entry.Version || insertPointResult.Slot.HasTombstone)
//        && insertPointResult.Slot.PrimitivePackage
//            == Interlocked.CompareExchange(
//                ref Unsafe.As<Meta, long>(ref table.GetBucket(insertPointResult.SlotIndex)),
//                new Meta(commitKeyIndex, insertPointResult.Entry).PrimitivePackage,
//                insertPointResult.Slot.PrimitivePackage
//            )
//        )
//        {
//            return header.Release(lockTaken);
//        }

//        return CommonPath(table, insertPointResult, commitKeyIndex, ref header, lockTaken);
//        static int CommonPath(
//            scoped Span<Meta> table,
//            MetaSlotResult insertPointResult,
//            int commitKeyIndex,
//            scoped ref TableHeader header,
//            TableHeader.LockTaken.ByWriterCount lockTaken)
//        {
//            if (insertPointResult.Slot.PrimitivePackage
//                != Interlocked.CompareExchange(
//                    ref Unsafe.As<Meta, long>(ref table.GetBucket(insertPointResult.SlotIndex)),
//                    insertPointResult.Slot.SetEntanglement().PrimitivePackage,
//                    insertPointResult.Slot.PrimitivePackage
//                )
//            )
//            {
//#if DEBUG
//                if (insertPointResult.Slot.HasEntanglement)
//                    throw new Exception();
//#endif
//                insertPointResult = MetaSlotResult.Progress(
//                    insertPointResult.SlotIndex,
//                    new(commitKeyIndex, insertPointResult.Entry),
//                    insertPointResult.Entry
//                );

//                insertPointResult = table.PromptDoubleInsert(ref insertPointResult, commitKeyIndex);
//                if (insertPointResult.IsInsertEndResult)
//                {
//#if DEBUG
//                    if (insertPointResult.IsFailed)
//                        throw new Exception();
//#endif
//                    return header.Release(lockTaken);
//                }
//#if DEBUG
//                System.Diagnostics.Debug.Assert(!insertPointResult.Slot.HasEntanglement);
//                ref var testSlotInner = ref table.GetBucket(insertPointResult.SlotIndex);
//                if (!testSlotInner.HasEntanglement || testSlotInner.RawData != insertPointResult.Slot.RawData)
//                    throw new InvalidOperationException("inner");
//#endif
//            }
//#if DEBUG
//            System.Diagnostics.Debug.Assert(!insertPointResult.Slot.HasEntanglement);
//            ref var testSlot = ref table.GetBucket(insertPointResult.SlotIndex);
//            if (!testSlot.HasEntanglement || testSlot.RawData != insertPointResult.Slot.RawData)
//                throw new InvalidOperationException("outer");
//#endif

//            var second = table.PromptDoubleInsert(ref insertPointResult, commitKeyIndex, insertPointResult.SlotIndex);
//            if (second.IsInsertEndResult)
//            {
//#if DEBUG
//                if (insertPointResult.IsFailed)
//                    throw new Exception();
//#endif
//                return header.Release(lockTaken);
//            }

//            table.ClosingInsert(
//                ref table.GetBucket(insertPointResult.SlotIndex),
//                new Meta(commitKeyIndex, insertPointResult.Entry),
//                ref table.GetBucket(second.SlotIndex),
//                insertPointResult.Slot.Update(second.Entry),
//                second.SlotIndex,
//                second.Slot
//            );
//            return header.Release(lockTaken);
//        }
//    }
//    [Obsolete]
//    /// <summary>
//    /// <paramref name="firstInsertedResult"/>: <see cref="MetaSlotResult.Slot"/> = kicked,
//    /// <see cref="MetaSlotResult.SlotIndex"/> = kicked Meta slot index
//    /// <see cref="MetaSlotResult.Entry"/> = when insert successful, insert to table[<paramref name="firstInsertedSlotIndex"/>] by entry + <paramref name="commitKeyIndex"/>
//    /// </summary>
//    /// <param name="table"></param>
//    /// <param name="firstInsertedResult"></param>
//    /// <param name="commitKeyIndex"></param>
//    /// <param name="firstInsertedSlotIndex"></param>
//    /// <returns></returns>
//    /// <exception cref="InvalidOperationException"></exception>
//    private static MetaSlotResult PromptDoubleInsert(
//        this scoped Span<Meta> table,
//        scoped ref MetaSlotResult firstInsertedResult,
//        int commitKeyIndex,
//        int firstInsertedSlotIndex = skipFirstInsertedSlotIndex | dummyFirstInsertedSlotFlag
//    )
//    {
//        var maskCache = table.GetBucketMask();
//        var firstResultLocal = firstInsertedResult;

//    recursion:;
//        var pos = firstResultLocal.SlotIndex;
//        var first = firstResultLocal.Slot;
//#if DEBUG
//        if (firstResultLocal.IsDefault || firstResultLocal.IsFailed || first.HasEntanglement)
//            throw new InvalidOperationException($"{nameof(first)} is default or invalid values");
//        if ((uint)firstInsertedSlotIndex < (uint)table.Length)
//        {
//            if (firstInsertedSlotIndex != firstResultLocal.SlotIndex)
//                throw new ArgumentException();
//            scoped ref var testSlot = ref table[firstInsertedSlotIndex];
//            if (!testSlot.HasEntanglement)
//                throw new InvalidOperationException($"{nameof(testSlot)} must have entanglement at the caller.");
//            if (firstResultLocal.IsSuccessful || testSlot.RawData > firstResultLocal.Entry.RawData)
//                throw new InvalidOperationException($"{nameof(firstInsertedResult)}: assumptions not met");
//            if (testSlot.RawData != firstResultLocal.Slot.RawData)
//                throw new InvalidOperationException($"{nameof(testSlot)}.{nameof(Meta.RawData)} != {nameof(firstInsertedResult)}.{nameof(Meta.RawData)}");
//        }
//#endif

//        var entry = first.DataReader;
//        var jumpType = entry.GetJumpType();
//        var jump = (int)jumpType;
//        var version = entry.Version;

//        scoped ref var current = ref Unsafe.NullRef<Meta>();
//        Meta currentTemp;

//        var read = 0;
//#if EARLYREHASH
//        if (entry.Distance == 0)
//            pos += MetaTableConcurrentOperateExtensions.InitialEarlyRehashStep(entry);
//#endif
//        while (true)
//        {
//#if DEBUG
//            read++;
//#endif

//            entry = entry.AddJump(jumpType);
//            current = ref table.GetBucket(pos += jump, maskCache);

//            currentTemp = current.VolatileRead();


//            if (currentTemp.RawData > entry.RawData)
//                continue;

//            if (currentTemp.HasTombstone && entry.Version == version)
//            {
//                if (currentTemp.PrimitivePackage == Interlocked.CompareExchange(
//                    ref Unsafe.As<Meta, long>(ref current),
//                    first.Update(entry).PrimitivePackage,
//                    currentTemp.PrimitivePackage
//                ))
//                {
//                    if ((uint)firstInsertedSlotIndex < (uint)table.Length)
//                        Volatile.Write(
//                            ref Unsafe.As<Meta, long>(ref table[firstInsertedSlotIndex]),
//                            new Meta(commitKeyIndex, firstResultLocal.Entry).PrimitivePackage
//                        );
//                    return MetaSlotResult.Success(pos & maskCache, currentTemp);
//                }

//                // retest
//                entry = entry.SubJump(jumpType);
//                pos -= jump;
//                continue;
//            }

//            if (currentTemp.RawData == entry.RawData)
//                continue;

//            if (entry.Version != version)
//            {
//#if !DEBUG
//                read +=
//MetaTableConcurrentOperateExtensions.
//#endif
//                    RestoreOverranEntry(
//                        jumpType,
//                        version,
//                        table.Length,
//                        ref entry,
//                        ref pos,
//                        out _
//                    );

//                if ((uint)read > (uint)table.Length + Meta.Data.MaxCountableDistance)
//#if DEBUG
//                    throw new Exception();
//#else
//                    return MetaSlotResult.Failed;
//#endif
//                continue;
//            }

//            if (currentTemp.HasEntanglement)
//            {
//                Thread.Yield();
//                if (current.VolatileRead().HasEntanglement)
//                {
//                    if ((uint)firstInsertedSlotIndex >= (uint)table.Length)
//                        MetaTableConcurrentOperateExtensions.SpinWaitWhileHasEntanglement(in current
//#if DEBUG
//                            , firstInsertedSlotIndex, table.GetBucketIndex(pos)
//#endif
//                                                    );
//                    else if (firstResultLocal
//                        != (firstResultLocal = MetaTableConcurrentOperateExtensions.EvacuationWait(
//                            table,
//                            ref table[firstInsertedSlotIndex],
//                            firstResultLocal,
//                            commitKeyIndex,
//                            in current
//                        ))
//                    )
//                    {
//                        firstInsertedResult = firstResultLocal;

//                        if (firstResultLocal.IsSuccessful)
//                            return firstResultLocal;

//                        if ((uint)firstInsertedSlotIndex < unchecked((uint)dummyFirstInsertedSlotFlag))
//                            firstInsertedSlotIndex = firstResultLocal.SlotIndex;
//                        goto recursion;
//                    }
//                }

//                // retest
//                entry = entry.SubJump(jumpType);
//                pos -= jump;
//                continue;

//                [MethodImpl(MethodImplOptions.NoInlining)]
//                static MetaSlotResult EvacuationWait(
//                    scoped Span<Meta> table,
//                    scoped ref Meta slotOfEntangled,
//                    MetaSlotResult firstResult,
//                    int commitKeyIndex,
//                    scoped ref readonly Meta current)
//                {
//                    var slotOriginal = firstResult.Slot;
//#if DEBUG
//                    if (slotOriginal.HasTombstone || slotOriginal.HasEntanglement)
//                        throw new Exception();
//                    if (slotOfEntangled.RawData != slotOriginal.RawData)
//                        throw new Exception();
//#endif
//                    Volatile.Write(ref Unsafe.As<Meta, long>(ref slotOfEntangled), slotOriginal.PrimitivePackage);
//                    MetaTableConcurrentOperateExtensions.SpinWaitWhileHasEntanglementANDEqual(in current, in slotOfEntangled, slotOriginal.DataReader
//#if DEBUG
//                        , firstResult.SlotIndex, table.Offset(in current)
//#endif
//                                            );
//                    if (slotOriginal.PrimitivePackage != Interlocked.CompareExchange(
//                        ref Unsafe.As<Meta, long>(ref slotOfEntangled),
//                        slotOriginal.SetEntanglement().PrimitivePackage,
//                        slotOriginal.PrimitivePackage
//                    ))
//                    {
//                        var temp = new Meta(commitKeyIndex, firstResult.Entry);
//                        firstResult = MetaSlotResult.Progress(
//                            table.Offset(in slotOfEntangled),
//                            temp,
//                            temp.DataReader
//                        );

//                        firstResult = table.PromptDoubleInsert(
//ref firstResult,
//                            (int)MetaAttributeTag.Tombstone
//                        );
//                    }

//                    return firstResult;
//                }
//            }// end hasEntanglement

//            if (currentTemp.PrimitivePackage
//                == Interlocked.CompareExchange(
//                    ref Unsafe.As<Meta, long>(ref current),
//                    currentTemp.SetEntanglement().PrimitivePackage,
//                    currentTemp.PrimitivePackage
//                )
//            )
//            {
//                if (currentTemp.DataReader.Version != version)
//                {
//                    if ((uint)firstInsertedSlotIndex < (uint)table.Length)
//                        Volatile.Write(
//                            ref Unsafe.As<Meta, long>(ref table[firstInsertedSlotIndex]),
//                            new Meta(commitKeyIndex, firstResultLocal.Entry).PrimitivePackage
//                        );
//                    Volatile.Write(
//                        ref Unsafe.As<Meta, long>(ref current),
//                        first.Update(entry).PrimitivePackage
//                    );
//                    return MetaSlotResult.Success(pos & maskCache, currentTemp);
//                }
//                return MetaSlotResult.Progress(pos & maskCache, currentTemp, entry);// if entry was default, MetaSlotResult.Progress() treats it as an succeed result.
//            }
//            else
//            {
//                // retest
//                entry = entry.SubJump(jumpType);
//                pos -= jump;
//            }

//        }// endless
//    }
//}