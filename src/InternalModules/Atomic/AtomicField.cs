//#define FORCE_SCAN_REMOVED_MAP
#define WIDE_LOCK
using FlatDictionary.InternalModules.Atomic;
using FlatDictionary.InternalModules.InLockHook;
using FlatDictionary.InternalModules.InLockHook.Interface;
using FlatDictionary.InternalModules.Interface;
using FlatDictionary.InternalModules.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;

[StructLayout(LayoutKind.Sequential)]
internal sealed class AtomicField<TKey, TValue> :  IDisposable
    where TKey : notnull
{
    //public static void CompactionCopyToTest(int size)
    //{
    //    var src = new AtomicField<TKey, TValue>(size, null, false);
    //    src.registeredLengthAndVersion.RegisteredKeysLength = size;
    //    src.publishCountAndRemoveChunkMap = new() { Count = 0, RemoveChunkMap = ~0 };
    //    if (typeof(TKey) == typeof(int))
    //    {
    //        var keys = Unsafe.As<int[]>(src.keys);
    //        for (var i = 0; i < keys.Length; i++)
    //            keys[i] = i;
    //    }
    //    var dst = new AtomicField<TKey, TValue>(size, null, false);

    //    var copied = src.CompactionCopyTo(dst, src.registeredLengthAndVersion, src.publishCountAndRemoveChunkMap);
    //    Console.WriteLine($"size: {size} dst: [len: {dst.registeredLengthAndVersion.RegisteredKeysLength}, pubCount: {dst.publishCountAndRemoveChunkMap.Count}] copied: {copied}");
    //    return;
    //}


    private const int minimumCapacity = RemoveChunks - 1;

    private const int RemoveChunks = sizeof(int) * 8;

    private const int DestroyedFlag = int.MinValue;
    private const int NotFoundKeyValueIndex = -1;

    private readonly TKey[] keys;// 8
    private readonly MetaTable table;// 8
    private readonly IEqualityComparer<TKey>? comparer;// 8

    private readonly TValue[] values;// 8
    private readonly int[] hashCodes;// 8

    private RefCount refCount;

    internal void DecrementRefCount()
    {

        if (this.refCount.Decrement())
            this.Dispose();

        return;
    }

    private CountAndChunkMap publishCountAndRemoveChunkMap;// 4 + 4
    private AtomicLengthAndVersion registeredLengthAndVersion;// 4 + 4

    internal AtomicLengthAndVersion VolatileReadCountAndVersion()
        => this.registeredLengthAndVersion.VolatileRead();
    internal TableVersion VolatileReadVersion()
        => this.registeredLengthAndVersion.Version.VolatileRead();

    internal ReadOnlySpan<TValue> Values
    {
        get
        {
            var values = this.values;
            var lav = this.registeredLengthAndVersion.VolatileRead();
            return (uint)lav.RegisteredKeysLength < (uint)values.Length
                ? new (values, 0, lav.RegisteredKeysLength)
                : new (values);
        }
    }

    internal ReadOnlySpan<TKey> Keys
    {
        get
        {
            var keys = this.keys;
            var lav = this.registeredLengthAndVersion.VolatileRead();
            return (uint)lav.RegisteredKeysLength < (uint)keys.Length
                ? new (keys, 0, lav.RegisteredKeysLength)
                : new (keys);
        }
    }

    private object SyncRoot
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.table.Source;
    }
    public int Count
        => this.publishCountAndRemoveChunkMap.Count;

    internal AtomicField(int capacity, IEqualityComparer<TKey>? comparer = null, LoadFactor loadFactor = LoadFactor.MemorySave)
        : this(
            loadFactor.ComputeTableSizeFromCapacity(capacity | minimumCapacity),
            comparer,
            loadFactor
        )
    { }

    private AtomicField(MetaTable.SqSize size, IEqualityComparer<TKey>? comparer = null, LoadFactor loadFactor = LoadFactor.MemorySave)
        : this(size, loadFactor.ComputeKeysSize(size), comparer) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AtomicField(MetaTable.SqSize size, int keysSize, IEqualityComparer<TKey>? comparer = null)
    {
        var table = MetaTable.Create(size);
        var tableCapacity = table.Size;
        var keys = new TKey[keysSize];
        this.keys = keys;

        if (typeof(TKey).IsValueType is false)
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        else if (comparer is not null
        && object.ReferenceEquals(comparer, EqualityComparer<TKey>.Default) is false)
            this.comparer = comparer;
        else
            this.comparer = default;
        if ((typeof(TKey).IsPrimitive || typeof(TKey).IsEnum)
        && comparer is null)
            this.hashCodes = [];
        else
            this.hashCodes = new int[keysSize];

        if (typeof(TValue) == typeof(ValueUnit))
            this.values = [];
        else
            this.values = new TValue[keysSize];

        this.registeredLengthAndVersion = new(0, TableVersion.Create(table.Header.SyncState.Version));
        this.publishCountAndRemoveChunkMap = default;
        this.table = table;
        this.refCount = new(true);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetValue(scoped ref readonly AtomicField<TKey, TValue> fieldLocation,
                                     TKey key,
                                     [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue value)
    {
        recursion:;
        var field = fieldLocation;
        var lav = field.registeredLengthAndVersion.VolatileRead();
        var res = field.KeyToSlot(key, lav, out _);

        if (res.IsSuccessful)
        {
            var values = field.values;
            if ((uint)res.Slot.KeyIndex < (uint)values.Length)
            {
                //value = values[res.Slot.KeyIndex];
                value = AtomicHelper<TValue>.BulkLoad(in values[res.Slot.KeyIndex]);
                return lav.Version.RawValue == field.registeredLengthAndVersion.Version.VolatileReadRawValue();
            }
        }
        else if (res.IsReadPast)
            goto recursion;

        value = default;
        return false;

    }

    internal static int GetIndex<TValueFactory, THook>(
        ref AtomicField<TKey, TValue> fieldLocation,
        TKey key,
        TValueFactory factory,
        THook? hook
        )
        where TValueFactory : struct, IAddValueFactory<TKey, TValue>
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
    {
        do
        {
            var field = Volatile.Read(ref fieldLocation);
            var lav = field.registeredLengthAndVersion.VolatileRead();
            var res = field.KeyToSlot(key, lav, out var hash);
            if (res.IsSuccessful)
                return res.Slot.KeyIndex;
            if (res.IsReadPast)
                continue;
            var result = hook is null || Hook.IsEmpty<THook, TKey, TValue>()
                ? TryRegistrationInternal(field, res, key, factory.Invoke(key), hash, lav, Hook.CreateDummy(field), ref fieldLocation)
                : TryRegistrationInternal(field, res, key, factory.Invoke(key), hash, lav, Hook.CreateRegistration(hook, field), ref fieldLocation);
            if (result.IsSuccess)
                return ~result.Index;
        } while (true);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    internal static TValue GetOrAdd<TFactory, THook>(
        ref AtomicField<TKey, TValue> fieldLocation,
        TKey key,
        TFactory addValueFactory,
        THook? hook)
        where TFactory : struct, IAddValueFactory<TKey, TValue>
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
    {
        var field = fieldLocation;
        var lav = field.registeredLengthAndVersion.VolatileRead();
        var res = field.KeyToSlot(key, lav, out var hash);

        if (res.IsSuccessful)
        {
            TValue returnValue;
            returnValue = AtomicHelper<TValue>.BulkLoad(in field.values[res.Slot.KeyIndex]);
            if (lav.Version.RawValue == field.registeredLengthAndVersion.Version.VolatileReadRawValue())
                return returnValue;

            res = MetaSlotResult.Failed;
        }

        return RegistrationLoop(res, field, lav, key, addValueFactory, hash, hook, ref fieldLocation);

        static TValue RegistrationLoop(
            MetaSlotResult result,
            AtomicField<TKey, TValue> field,
            AtomicLengthAndVersion comparand,
            TKey key,
            TFactory valueFactory,
            int hash,
            THook? hook,
            scoped ref AtomicField<TKey, TValue> fieldLocation
        )
        {
            var returnValue = valueFactory.Invoke(key);
            do
            {
#if DEBUG
                System.Diagnostics.Debug.Assert(result.IsSuccessful is false);
#endif
                if (result.IsReadPast is false)
                {
                    var rRes = hook is null || Hook.IsEmpty<THook, TKey, TValue>()
                        ? TryRegistrationInternal(field, result, key, returnValue, hash, comparand, Hook.CreateDummy(field), ref fieldLocation)
                        : TryRegistrationInternal(field, result, key, returnValue, hash, comparand, Hook.CreateRegistration(hook, field), ref fieldLocation);
                    if (rRes.IsSuccess)
                        return returnValue;
                }

                field = Volatile.Read(ref fieldLocation);
                comparand = field.registeredLengthAndVersion.VolatileRead();
                result = field.KeyToSlot(key, comparand, out hash);
                if (result.IsSuccessful)
                {
                    returnValue = AtomicHelper<TValue>.BulkLoad(in field.values[result.Slot.KeyIndex]);
                    if (comparand.Version.RawValue == field.registeredLengthAndVersion.Version.VolatileReadRawValue())
                        return returnValue;
                    else
                        result = MetaSlotResult.Failed;
                }
            } while (true);
        }

    }


    internal static TValue AddOrUpdate<T, U, THook>(
            scoped ref AtomicField<TKey, TValue> fieldLocation,
            TKey key,
            T addValueFactory,
            U updateValueFactory,
            THook? hook)
        where T : struct, IAddValueFactory<TKey, TValue>
        where U : struct, IUpdateValueFactory<TKey, TValue>
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
    {
    recursion:;
        var field = Volatile.Read(ref fieldLocation);
        var lav = field.registeredLengthAndVersion.VolatileRead();
        var res = field.KeyToSlot(key, lav, out var hash);
        var values = field.values;
        int index;
        TValue returnValue;
        RegistrationResult regRes;
        if (res.IsSuccessful)
        {
            index = res.Slot.KeyIndex;
            if ((uint)index >= (uint)values.Length)
                return Throw();
            static TValue Throw()
                => throw new Exception();
            returnValue = updateValueFactory.UpdateValueFactory(key, values[index]);
            regRes = hook is null || Hook.IsEmpty<THook, TKey, TValue>()
                ? TryRegistrationInternal(
                    field,
                    res,
                    key,
                    returnValue,
                    hash,
                    lav,
                    Hook.CreateDummy(field),
                    ref fieldLocation)
                : TryRegistrationInternal(
                    field,
                    res,
                    key,
                    returnValue,
                    hash,
                    lav,
                    Hook.CreateUpdate(hook, field),
                    ref fieldLocation
                );
        }
        else
        {
            if (res.IsReadPast)
                goto recursion;

            returnValue = addValueFactory.Invoke(key);
            regRes = hook is null || Hook.IsEmpty<THook, TKey, TValue>()
                ? TryRegistrationInternal(
                    field,
                    res,
                    key,
                    returnValue,
                    hash,
                    lav,
                    Hook.CreateDummy(field),
                    ref fieldLocation)
                : TryRegistrationInternal(
                    field,
                    res,
                    key,
                    returnValue,
                    hash,
                    lav,
                    Hook.CreateRegistration(hook, field),
                    ref fieldLocation
                );
        }
        if (regRes.IsFailed)
            goto recursion;

        return returnValue;
    }

    internal static bool TryUpdate<THook>(ref AtomicField<TKey, TValue> fieldLocation, TKey key, TValue newValue, TValue comparisonValue, THook? hook)
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
    {
    recursion:;
        var field = Volatile.Read(ref fieldLocation);
        var lav = field.registeredLengthAndVersion.VolatileRead();
        var res = field.KeyToSlot(key, lav, out var hash);
        if (res.IsSuccessful)
        {
            var values = field.values;
            if ((uint)res.Slot.KeyIndex >= (uint)values.Length)
                return Throw();
            bool Throw() => throw new Exception();

            var value = values[res.Slot.KeyIndex];
            if (field.registeredLengthAndVersion.Version.VolatileRead().RawValue == lav.Version.RawValue
            && EqualityComparer<TValue>.Default.Equals(value, comparisonValue))
            {
                if ((hook is null || Hook.IsEmpty<THook, TKey, TValue>()
                    ? TryRegistrationInternal(
                        field,
                        res,
                        key,
                        newValue,
                        hash,
                        lav,
                        Hook.CreateDummy(field),
                        ref fieldLocation
                    )
                    : TryRegistrationInternal(
                        field,
                        res,
                        key,
                        newValue,
                        hash,
                        lav,
                        Hook.CreateUpdate(hook, field),
                        ref fieldLocation
                    )).IsFailed
                )
                    goto recursion;
                return true;
            }
            return false;
        }

        if (res.IsReadPast)
            goto recursion;
        else
            return false;
    }
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MetaSlotResult KeyToSlot(
       TKey key,
       AtomicLengthAndVersion lav,
       scoped out int hashCode,
       MetaSlotBehavior behavior = default)
    {
        int hash;
        MetaSlotResult result;
        //var comparer = this.comparer;
        if (typeof(TKey).IsValueType)
        {
            if (comparer is null)
            {
                if (AtomicHelper<TKey>.IsWordSize)
                {
                    var proxy = AtomicCompareProxy.CreateInWordSize(key);
                    hash = proxy.GetHashCode();
                    result = Find(this, lav, hash, behavior, proxy);
                }
                else
                {
                    var proxy = AtomicCompareProxy.CreateOutWordSize(key, this, lav.Version);
                    hash = proxy.GetHashCode();
                    result = Find(this, lav, hash, behavior, proxy);
                }
            }
            else
            {
                if (AtomicHelper<TKey>.IsWordSize)
                {
                    var proxy = AtomicCompareProxy.CreateInWordSize(key, comparer);
                    hash = proxy.GetHashCode();
                    result = Find(this, lav, hash, behavior, proxy);
                }
                else
                {
                    var proxy = AtomicCompareProxy.CreateOutWordSize(key, comparer, this, lav.Version);
                    hash = proxy.GetHashCode();
                    result = Find(this, lav, hash, behavior, proxy);
                }
            }
        }
        else
        {
#if DEBUG
            if (comparer is null)
                throw new NullReferenceException();
#endif
            var proxy = AtomicCompareProxy.CreateInWordSize(key, this.comparer!);
            hash = proxy.GetHashCode();
            result = Find(this, lav, hash, behavior, proxy);
        }


        hashCode = hash;
#if DEBUG
        if(/*result.IsFailed ||*/ result.Slot.HasEntanglement)
            throw new Exception();
        //if ((uint)(result.SlotIndex & int.MinValue) >= (uint)table.AsSpan().Length)
        //    throw new Exception();
#endif
        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static MetaSlotResult Find<TProxy>(
                AtomicField<TKey, TValue> field,
                AtomicLengthAndVersion lengthAndVersion,
                int hash,
                MetaSlotBehavior behavior,
                TProxy proxy
        )
            where TProxy : struct, IEquatable<TKey>
        {
            return field.table.AsSpan().FindOrInsertPoint(
                field.keys.AsSpan(0, lengthAndVersion.RegisteredKeysLength),
                proxy,
                hash,
                lengthAndVersion.Version.Table,
                behavior
            );
            //var metaTable = field.table;
            //var itemArray = field.keys;
//            var initial = metaTable.GetBucket(hash).VolatileRead();

//            if (initial.DataReader.VersionEqualsOrEntanglement(lengthAndVersion.Version.Table) is false)
//            {
//                //if (initial.DataReader.Version < lengthAndVersion.Version.Table)
//                //    return MetaSlotResult.Progress(
//                //        new(metaTable.GetBucketIndex(hash)),
//                //        initial,
//                //        Meta.Data.CreateEntry(hash, lengthAndVersion.Version.Table)
//                //    );
//                //else
//                //    return MetaSlotResult.Progress(
//                //        new (metaTable.GetBucketIndex(hash), true),
//                //        initial,
//                //        Meta.Data.CreateEntry(hash, lengthAndVersion.Version.Table)
//                //    );
//                return MetaSlotResult.Progress(
//                       new(metaTable.GetBucketIndex(hash), initial.DataReader.Version > lengthAndVersion.Version.Table),
//                       initial,
//                       Meta.Data.CreateEntry(hash, lengthAndVersion.Version.Table)
//                   );
//                //behavior = behavior.SetNeedRetry();
//            }
//            else if (initial.DataReader.Distance == 0)
//            {
//                var index = initial.KeyIndex & behavior.AsUnreachedVisibleMask();
//                if ((uint)index < (uint)itemArray.Length
//                //&& (uint)index < (uint)lengthAndVersion.RegisteredKeysLength
//                )
//                {
//#if DEBUG
//                    System.Diagnostics.Debug.Assert((uint)index < (uint)lengthAndVersion.RegisteredKeysLength);
//#endif
//                    var item = AtomicHelper<TKey>.BulkLoad(in itemArray[index]);
//                    if (proxy.Equals(item))
//                        return MetaSlotResult.Success(metaTable.GetBucketIndex(hash), initial);
//                }
//                else
//                    behavior = behavior.SetNeedRetryIfReadPast(index, itemArray.Length);
//            }
//            if ((uint)lengthAndVersion.RegisteredKeysLength > (uint)itemArray.Length)
//                return MetaSlotResult.Failed;
//            return metaTable.FindOrInsertPoint(
//                new ReadOnlySpan<TKey>(itemArray, 0, lengthAndVersion.RegisteredKeysLength), 
//                proxy, 
//                hash,
//                lengthAndVersion.Version.Table,
//                behavior
//            );
        }
    }

    private static RegistrationResult TryRegistrationInternal<THook>(
        AtomicField<TKey, TValue> field,
        MetaSlotResult result,
        TKey key,
        TValue value,
        int hash,
        AtomicLengthAndVersion countsComparand,
        THook hook,
        scoped ref AtomicField<TKey, TValue> fieldLocation
    )
        where THook : struct, IRegistrationPolicy, IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
    {
        const int countIncrementor = 1;
        var table = field.table;
        
#if DEBUG
        if (result.IsReadPast)
        {
            throw new InvalidOperationException();
        }

        //if (behavior.HasFlag(MetaKeyIndexBehavior.Update) && result.Slot.KeyIndex >= countsComparand.RegisteredKeysLength)
        //{
        //    throw new InvalidOperationException();
        //}

        if ((result.IsSuccessful && hook.IsUpdate is false) || result.Slot.HasEntanglement)
        {
            throw new ArgumentException();
        }
#endif
        int removedPosition;
        int addCounter;

        var keys = field.keys;

        if (hook.IsUpdate)
        {
            removedPosition = Helper.IndexToBitPosition(keys.Length, table.Size, result.Slot.KeyIndex);
            addCounter = default;
        }
        else
        {
            removedPosition = default;
            addCounter = countIncrementor;
        }

        var guarantee = table.LockPerSlot(ref result);

        var tableIndex = result.SlotIndex + MetaTable.HeaderOffset;
        if ((uint)tableIndex >= (uint)table.Source.Length)
        {
            return default;
        }

        scoped ref var insertSlot = ref table.Source[tableIndex];
        scoped ref var header = ref table.Header;

        var lockTaken = header.TryAcquire(TableHeader.LockType.Registration);
        if (!lockTaken)
            if (!(lockTaken = header.TryAcquireUntilHeavyProcess(TableHeader.LockType.Registration)))
            {
                InterlockedMeta.ReleaseLockPerEntry(table, result, guarantee);
                if (header.VolatileReadState().Contains(TableHeader.LockType.HeavyProcess))
                    HeavyWait(ref header, field.SyncRoot);
                [MethodImpl(MethodImplOptions.NoInlining)]
                static void HeavyWait(scoped ref readonly TableHeader header, object lockObj)
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
                    lock (lockObj) { }
                }

                return default;
            }

        // in to critical section.

        var latestLenVer = field.registeredLengthAndVersion;
       

        if (countsComparand.Version.RawValue != latestLenVer.Version.RawValue
        || object.ReferenceEquals(field, Volatile.Read(ref fieldLocation)) is false)
        {
            header.Release(lockTaken, default);
            InterlockedMeta.ReleaseLockPerEntry(table, result, guarantee);

            return default;
        }
        var index = latestLenVer.RegisteredKeysLength;
#if DEBUG
        System.Diagnostics.Debug.Assert(countsComparand.RegisteredKeysLength <= index);
#endif
        if ((uint)index >= (uint)keys.Length)
        {
            header.Release(lockTaken, default);
            InterlockedMeta.ReleaseLockPerEntry(table, result, guarantee);

            CompactionGrow(
                (uint)index >= (uint)keys.Length
                    ? (table.Size << 1, keys.Length << 1)
                    : (table.Size, keys.Length),
                ref fieldLocation, hook
            );

            return default;
        }

        keys[index] = key;
        var values = field.values;
        if ((uint)index < (uint)values.Length)
            values[index] = value;
        var hashCodes = field.hashCodes;
        if ((uint)index < (uint)hashCodes.Length)
            hashCodes[index] = hash;

        Volatile.Write(ref field.registeredLengthAndVersion.RegisteredKeysLength, index + 1);

        tableIndex = guarantee.SlotIndex + MetaTable.HeaderOffset;
        if ((uint)tableIndex >= (uint)table.Source.Length)
        {
#if DEBUG
            //if (behavior.HasFlag(MetaKeyIndexBehavior.Update))
            //    throw new InvalidOperationException();

            //if (result.SlotIndex != table.GetBucketIndex(hash))
            //    Console.WriteLine(result.SlotIndex);
            if (insertSlot.VolatileRead().HasEntanglement is false)
                System.Diagnostics.Debug.Fail($"{nameof(insertSlot)}");
#endif
            Volatile.Write(
                ref field.publishCountAndRemoveChunkMap.Count,
                field.publishCountAndRemoveChunkMap.Count + countIncrementor
            );
            //Interlocked.Increment(ref field.publishCountAndRemoveChunkMap.Count);
            if (hook.IsDummy)
            {
                header.Release(lockTaken, TableHeader.Context.Clean);
                InterlockedMeta.Write(ref insertSlot, new Meta(index, result.Entry));
            }
            else
            {
                var w = header.ReleaseAtomicIncrementWriterCount(lockTaken, TableHeader.Context.Clean);
                hook.Add(key, value, index);
                InterlockedMeta.Write(ref insertSlot, new Meta(index, result.Entry));
                header.Release(w);
            }

            if (hook.IsDummy is false)
                hook.ThrowIfExcept();

            return new(index);
        }

        scoped ref var advanceSlot = ref table.Source[tableIndex];

        var countAndChunkMap = field.publishCountAndRemoveChunkMap;

        countAndChunkMap.Count += addCounter;
        countAndChunkMap.RemoveChunkMap |= removedPosition;

        Volatile.Write(
            ref field.publishCountAndRemoveChunkMap.AsPrimitive,
            countAndChunkMap.AsPrimitive
        );

        // half release
        var writeLock = header.ReleaseAtomicIncrementWriterCount(lockTaken);

        if ((uint)(field.refCount.BurntLength | Meta.UnreachableFlag) <= (uint)result.Slot.KeyIndex)// over write
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(insertSlot.VolatileRead() == result.Slot.SetEntanglement());
            System.Diagnostics.Debug.Assert(advanceSlot.VolatileRead() == guarantee.Slot.SetEntanglement());
            System.Diagnostics.Debug.Assert(result.Slot.HasNotKeyIndex);
#endif
            InterlockedMeta.CompareRestore(ref advanceSlot, guarantee.Slot);
            if (hook.IsDummy is false)
                if (hook.IsUpdate)
                    hook.Update(new(field), result.Slot.KeyIndex, index);
                else
                    hook.Add(key, value, index);

            InterlockedMeta.Write(ref insertSlot, new(index, result.Entry));
        }
        else// insert
        {
            if (hook.IsDummy is false)
                if (hook.IsUpdate)
                    hook.Update(new(field), result.Slot.KeyIndex, index);
                else
                    hook.Add(key, value, index);

#if DEBUG
            System.Diagnostics.Debug.WriteLineIf(false ==
#endif
            table.ClosingInsert(
                ref insertSlot,
                new Meta(index, result.Entry),
                ref advanceSlot,
                guarantee,
                hook.IsUpdate
                    ? result.Slot.KeyIndex | Meta.UnreachableFlag
                    : result.Slot.KeyIndex
            )
#if DEBUG
            , $"false insert: {DateTime.Now.ToString("hh:mm:ss.ffff")}"
            );
#else
    ;
#endif
#if DEBUG
            var debug_field = Volatile.Read(ref fieldLocation);
            var lav = debug_field.registeredLengthAndVersion.VolatileRead();
            var res = debug_field.KeyToSlot(key, lav, out _, MetaSlotBehavior.UnreachedVisible);
            while (res.IsReadPast)
                res = debug_field.KeyToSlot(key, lav, out _, MetaSlotBehavior.UnreachedVisible);
            System.Diagnostics.Debug.Assert(res.IsSuccessful);
#endif
        }

        // full release
        header.Release(writeLock);

        if (hook.IsDummy is false)
            hook.ThrowIfExcept();

        return new(index);


    }



    internal static bool TryRemove<THook>(
            scoped ref AtomicField<TKey, TValue> fieldLocation,
            TKey key,
            [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue value,
            THook? hook)
        where THook : IInLockRemoveHook<TKey, TValue>
    {
    recursion:;
        var field = Volatile.Read(ref fieldLocation);

        var lav = field.registeredLengthAndVersion.VolatileRead();
        var foundResult = field.KeyToSlot(key, lav, out _);
        if (foundResult.IsSuccessful is false)
        {
            if (foundResult.IsReadPast)
                goto recursion;
            value = default;
            return false;
        }
        var values = field.values;
        var keyIndex = foundResult.Slot.KeyIndex;
        var foundValue = (uint)keyIndex < (uint)values.Length
            ? values[keyIndex] 
            : default!;
        if (hook is null || Hook.IsEmpty<THook, TKey, TValue>()
            ? Remove(field, ref fieldLocation, foundResult, lav, key, foundValue, Hook.CreateDummy<TKey, TValue>())
            : Remove(field, ref fieldLocation, foundResult, lav, key, foundValue, Hook.CreateRemove(hook, field)))
        {
            value = foundValue;
            return true;
        }
        else
        {
            goto recursion;
        }
    }

    internal static bool TryRemove<THook>(scoped ref AtomicField<TKey, TValue> fieldLocation, KeyValuePair<TKey, TValue> pair, THook? hook)
        where THook : IInLockRemoveHook<TKey, TValue>
    {
    recursion:;
        var field = Volatile.Read(ref fieldLocation);
        var lav = field.registeredLengthAndVersion.VolatileRead();
        var foundResult = field.KeyToSlot(pair.Key, lav, out _);
        if (foundResult.IsSuccessful is false)
        {
            if (foundResult.IsReadPast)
                goto recursion;
            return false;
        }

        var values = field.values;
        var valueIndex = foundResult.Slot.KeyIndex;

        if ((uint)valueIndex >= (uint)values.Length)
            return false;
        else
        {
            if (AtomicHelper<TValue>.IsWordSize)
            {
                var value = AtomicHelper<TValue>.BulkLoad(in values[valueIndex]);
                if (EqualityComparer<TValue>.Default.Equals(value, pair.Value) is false)
                    return false;
            }
            else
            {
                var value = values[valueIndex];
                if (field.registeredLengthAndVersion.Version.VolatileReadRawValue() != lav.Version.RawValue)
                    goto recursion;
                if (EqualityComparer<TValue>.Default.Equals(value, pair.Value) is false)
                    return false;
            }
        }
        if (hook is null || Hook.IsEmpty<THook, TKey, TValue>()
            ? Remove(field, ref fieldLocation, foundResult, lav, pair.Key, pair.Value, Hook.CreateDummy<TKey, TValue>())
            : Remove(field, ref fieldLocation, foundResult, lav, pair.Key, pair.Value, Hook.CreateRemove(hook, field)))
            return true;
        else
            goto recursion;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Remove<T>(AtomicField<TKey, TValue> field,
                                  scoped ref readonly AtomicField<TKey, TValue> fieldLocation,
                                  MetaSlotResult foundResult,
                                  AtomicLengthAndVersion lav,
                                  TKey key,
                                  TValue value,
                                  T hook)
        where T : IInLockRemoveHook<TKey, TValue>, IHookPolicy
    {
        var table = field.table;
        scoped ref var header = ref table.Header;
        var removedMap = Helper.IndexToBitPosition(field.keys.Length, table.Size, foundResult.Slot.KeyIndex);

        var lockTaken = header.AcquireWithDoubleWait(TableHeader.LockType.Removing, field.SyncRoot);

        var countAndChunkMap = field.publishCountAndRemoveChunkMap;
        if (object.ReferenceEquals(field, Volatile.Read(ref Unsafe.AsRef(in fieldLocation)))
            && field.registeredLengthAndVersion.Version.RawValue == lav.Version.RawValue)
            if (hook.IsDummy)
            {
                if (foundResult.Slot
                    == InterlockedMeta.CompareSetUnreachable(
                        ref table.GetBucket(foundResult.SlotIndex),
                        foundResult.Slot
                    ))
                {
    #if DEBUG
                    var chunkSize = Helper.GetChunkSize(field.keys.Length, table.Size);
                    var start = chunkSize * Helper.GetIndexOfPowerOfTwo(removedMap);
                    System.Diagnostics.Debug.Assert(start <= foundResult.Slot.KeyIndex && (start + chunkSize) > foundResult.Slot.KeyIndex);
    #endif
                    Volatile.Write(
                        ref field.publishCountAndRemoveChunkMap.AsPrimitive,
                        new CountAndChunkMap()
                        {
                            Count = countAndChunkMap.Count - 1,
                            RemoveChunkMap = countAndChunkMap.RemoveChunkMap | removedMap,
                        }.AsPrimitive
                    );

                    header.Release(lockTaken, default);

                    return true;
                }
            }
            else
            {
                scoped ref var slot = ref table.GetBucket(foundResult.SlotIndex);
                if (foundResult.Slot
                    == InterlockedMeta.CompareSetEntanglement(
                        ref slot,
                        foundResult.Slot
                    ))
                {
    #if DEBUG
                    var chunkSize = Helper.GetChunkSize(field.keys.Length, table.Size);
                    var start = chunkSize * Helper.GetIndexOfPowerOfTwo(removedMap);
                    System.Diagnostics.Debug.Assert(start <= foundResult.Slot.KeyIndex && (start + chunkSize) > foundResult.Slot.KeyIndex);
    #endif
                    Volatile.Write(
                        ref field.publishCountAndRemoveChunkMap.AsPrimitive,
                        new CountAndChunkMap()
                        {
                            Count = countAndChunkMap.Count - 1,
                            RemoveChunkMap = countAndChunkMap.RemoveChunkMap | removedMap,
                        }.AsPrimitive
                    );
                    var w = header.ReleaseAtomicIncrementWriterCount(lockTaken);
                    hook.Remove(key, value, foundResult.Slot.KeyIndex);
                    InterlockedMeta.WriteUnreachable(ref slot, foundResult.Slot);

                    header.Release(w);
                    hook.ThrowIfExcept();
                    return true;
                }
            }
        header.Release(lockTaken, default);

        return false;
    }

    internal static void Clear<THook>(scoped ref AtomicField<TKey, TValue> fieldLocation, THook? hook, bool clearAll = false)
        where THook : IInLockClearHook
    {
        if (clearAll)
        {
            if (hook is null || Hook.IsEmpty<THook, TKey, TValue>())
                ExchangeClear(ref fieldLocation, Hook.CreateDummy<TKey, TValue>());
            else
                ExchangeClear(ref fieldLocation, Hook.CreateClear<THook>(hook));
        }
        if (hook is null || Hook.IsEmpty<THook, TKey, TValue>())
            Clear(ref fieldLocation, Hook.CreateDummy<TKey, TValue>());
        else
            Clear(ref fieldLocation, Hook.CreateClear<THook>(hook));
    }
    private static void Clear<THook>(scoped ref AtomicField<TKey, TValue> fieldLocation, THook hook)
        where THook : IHookPolicy, IInLockClearHook
    {
        var field = Volatile.Read(ref fieldLocation);
        scoped ref var header = ref field.table.Header;
        var lockTaken = header.AcquireInZeroWriter(TableHeader.LockType.Clearing, field.SyncRoot);

        if (lockTaken.CurrentState.Contains(TableHeader.Context.Clean))
        {
            header.Release(lockTaken);
            return;
        }

        var newVersion = field.registeredLengthAndVersion.Version.IncrementTableVersion(out var overflowed);
        if (object.ReferenceEquals(field, Volatile.Read(ref fieldLocation)) is false
        || overflowed
        || field.refCount.IsReferenced)
        {
            header.Release(lockTaken);
            ExchangeClear(ref fieldLocation, hook);
            return;
        }

        if (hook.IsDummy is false)
            hook.Clear(Volatile.Read(ref field.publishCountAndRemoveChunkMap.Count));


        //field.publishCountAndRemoveChunkMap = default;
        Volatile.Write(
            ref field.publishCountAndRemoveChunkMap.AsPrimitive,
            default
        );
        Volatile.Write(
            ref field.registeredLengthAndVersion.AsPrimitive,
            new AtomicLengthAndVersion(
                0,
                newVersion
            ).AsPrimitive
        );
        header.ReleaseAtomicUpdateVersion(lockTaken, newVersion.Table);

        if (hook.IsDummy is false)
            hook.ThrowIfExcept();
    }
    private static void ExchangeClear<THook>(scoped ref AtomicField<TKey, TValue> fieldLocation, THook hook)
        where THook : IInLockClearHook, IHookPolicy
    {
        AtomicField<TKey, TValue>? exchangeField = null;
    recursion:;

        var field = Volatile.Read(ref fieldLocation);
        TableHeader.LockTaken lockTaken = default;
        var objLockTaken = false;

        scoped ref var header = ref field.table.Header;
        //if (header.SyncState.Contains(TableHeader.Context.Clean))
        //    return;
        try
        {
            Monitor.Enter(field.SyncRoot, ref objLockTaken);
            if (object.ReferenceEquals(field, Volatile.Read(ref fieldLocation)) is false)
                goto recursion;

            if (exchangeField is null
            || exchangeField.keys.Length < field.keys.Length)
                exchangeField = new(field.table.Size, field.keys.Length, field.comparer);

            lockTaken = header.AcquireInZeroWriter(TableHeader.LockType.Clearing);

            // into critical section

            if (lockTaken.CurrentState.Contains(TableHeader.Context.Clean))
                return;

            if (hook.IsDummy is false)
                hook.Clear(
                    Volatile.Read(ref field.publishCountAndRemoveChunkMap.Count)
                );

            Volatile.Write(ref fieldLocation, exchangeField);
            lockTaken = header.Update(lockTaken, addContext: TableHeader.Context.Leave);
        }
        finally
        {
            if (lockTaken)
                header.Release(lockTaken, default);
            lockTaken = default;
            if (objLockTaken)
                Monitor.Exit(field.SyncRoot);
            objLockTaken = false;
        }

        if (hook.IsDummy is false)
            hook.ThrowIfExcept();
    }

    #region DEBUG_METHODS

    internal (int, int) Debug_Removed(IEnumerable<TKey> valid)
    {
        var removed = new HashSet<TKey>(this.keys);
        var vCount = 0;
        var lav = this.registeredLengthAndVersion;
        foreach (var key in valid)
        {
            System.Diagnostics.Debug.Assert(removed.Remove(key));
            if (this.KeyToSlot(key, lav, out _).IsSuccessful is false)
                vCount++;
        }

        var rCount = 0;
        foreach(var key in removed)
        {
            var res = this.KeyToSlot(key, lav, out _);
            if (res.IsSuccessful)
                rCount++;
        }

        return (vCount, rCount);
    }
    internal void Debug_CollisionCount()
    {
        //var table = this.table;
        //var version = this.registeredLengthAndVersion.VolatileRead().Version;
        //var collisions = table.Source
        //    .Where(meta => meta.DataReader.Version == this.registeredLengthAndVersion.Version.Table && meta.DataReader.DynamicHash != 0)
        //    .GroupBy(meta => meta.DataReader.DynamicHash)
        //    .OrderBy(group => group.Key);
        //var sum = 0;
        //foreach (var group in collisions)
        //{
        //    var c = group.Count();
        //    Console.WriteLine($"distance: {group.Key}, count: {c}");
        //    sum += c;
        //}
        //Console.WriteLine($"sum: {sum} in {this.publishCountAndRemoveChunkMap.Count}");
    }

    internal IEnumerable<TKey> Debug_Keys()
    {
        return this.keys.Take(this.registeredLengthAndVersion.RegisteredKeysLength);
    }
    internal (int count, AtomicLengthAndVersion lav) Debug_Count(bool stopIfNotFound = false, MetaSlotBehavior behavior = default)
    {
        var lav = this.registeredLengthAndVersion.VolatileRead();
        //var keys = this.keys.AsSpan(0, lav.RegisteredKeysLength);
        var count = 0;
        MetaSlotResult res;
        var span = this.Keys;
        var table = this.table.AsReadOnlySpan();
        var c = this.comparer;
        for (var i = 0; i < span.Length; ++i)
        {
            res = table.FindByKeyIndex(i, c is null ? span[i].GetHashCode() : c.GetHashCode(span[i]), lav.Version.Table);
            //res = this.KeyToSlot(span[i], lav, out _);
            if (res.IsSuccessful)
                count++;
            else if (stopIfNotFound)
                System.Diagnostics.Debug.Fail(res.ToString());
        }
        //foreach (var key in this.keys.Take(lav.RegisteredKeysLength).Distinct())
        //{
        //    while ((res = this.KeyToSlot(key, lav, out _, behavior)).IsReadPast)
        //        Thread.Yield();
            
        //    if (res.IsSuccessful)
        //        count++;
        //    else if (stopIfNotFound)
        //        System.Diagnostics.Debug.Fail(res.ToString());
            
        //}



        return (count, lav);
    }

    internal string Debug_Dump(string path = @".\dump.txt")
    {
#if DEBUG
        var src = this.table.Source
            .Skip(MetaTable.HeaderOffset)
            .Where(meta => meta.DataReader.Version == this.registeredLengthAndVersion.Version.Table)
            .Select(static meta => meta.ToString());
        System.IO.File.WriteAllLines(path, src);
        return System.IO.Path.GetFullPath(path);
#else
        return "";
#endif
    }

    internal IEnumerable<(int index, Meta meta)> Debug_Find(TKey key)
    {
#if DEBUG
        var idx = Array.IndexOf(this.keys, key);
        return this.table.Source
            .Skip(MetaTable.HeaderOffset)
            .Select(static (meta, idx) => (idx, meta))
            .Where(
                tuple => tuple.meta.DataReader.Version == this.registeredLengthAndVersion.Version.Table 
                && tuple.meta.KeyIndex == idx
            );
#else
        return [];
#endif
    }

    internal IEnumerable<Meta> Debug_Versions(ushort version)
    {
        return this.table.Source
            .Where(meta => meta.DataReader.Version == version);
    }
    #endregion
    #region CopyMethods

    private static AtomicField<TKey, TValue> CompactionGrow<THook>(
        (MetaTable.SqSize tableSize, int keysSize) newSize,
        scoped ref AtomicField<TKey, TValue> location,
        THook hook,
        bool cleanup = false)
        where THook : IHookPolicy, IInLockReallocateHook<TKey, TValue>
    {
        var source = Volatile.Read(ref location);
#if DEBUG
        var retryCount = 0;
#endif
    retry:;
        var prevCopied = 0;
        lock (source.SyncRoot)
        {
            if (object.ReferenceEquals(source, Volatile.Read(ref location)) is false)
            {
                source = Volatile.Read(ref location);
                goto retry;
            }


            var sourceKeysLength = source.keys.Length;
            if (newSize.keysSize <= sourceKeysLength)
            {
                if (Volatile.Read(ref source.publishCountAndRemoveChunkMap.RemoveChunkMap) != default && cleanup)
                    newSize = (source.table.Size, sourceKeysLength);
                else
                    return source;
            }

            scoped ref TableHeader header = ref source.table.Header;
            TableHeader.LockTaken lockTaken = default;

            var newField = new AtomicField<TKey, TValue>(newSize.tableSize, newSize.keysSize, source.comparer);
        inLockRetry:;
            try
            {
#if WIDE_LOCK
                lockTaken = header.AcquireWithSpinWait(TableHeader.LockType.CompactionGrow);
#endif
                var lav = source.registeredLengthAndVersion.VolatileRead();
                var publishCount = source.publishCountAndRemoveChunkMap.VolatileRead();
#if DEBUG
                System.Diagnostics.Debug.Assert(retryCount++ <= 1000);
#endif
                var copied = publishCount.Count > 0
                    ? source.CompactionCopyTo(newField, lav, publishCount)
                    : 0;

                if ((RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()
                    || RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                && copied < prevCopied)
                {
                    Trim(newField, copied);
                    prevCopied = 0;
                    static void Trim(AtomicField<TKey, TValue> atomicField, int validLength)
                    {

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                        {
                            var hasRefTypeKeys = atomicField.keys;
                            if ((uint)validLength < (uint)hasRefTypeKeys.Length)
                                hasRefTypeKeys.AsSpan(validLength).Clear();
                        }

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                        {
                            var hasRefTypeValues = atomicField.values;
                            if ((uint)validLength < (uint)hasRefTypeValues.Length)
                                hasRefTypeValues.AsSpan(validLength).Clear();
                        }
                    }
                }
                prevCopied = copied;

#if !WIDE_LOCK
                lockTaken = header.AcquireWithSpinWait(TableHeader.LockType.CompactionGrow);
#endif
#if DEBUG
                System.Diagnostics.Debug.Assert(object.ReferenceEquals(source, Volatile.Read(ref location)));
#endif
                if (header.VolatileReadWriterCount() != 0)
                    lockTaken = header.WaitWhileExistWriter(lockTaken, TableHeader.LockType.CompactionGrow);

                var latestLAV = source.registeredLengthAndVersion;
                var latestPublishCount = source.publishCountAndRemoveChunkMap;
                if (latestLAV.AsPrimitive == lav.AsPrimitive
                && latestPublishCount.Count == prevCopied)
                {
#if DEBUG
                    System.Diagnostics.Debug.Assert(latestPublishCount.Count == newField.registeredLengthAndVersion.RegisteredKeysLength, retryCount.ToString());

                    System.Diagnostics.Debug.Assert(newField.publishCountAndRemoveChunkMap.Count == newField.registeredLengthAndVersion.RegisteredKeysLength);

                    //var (count, lav) = newField.Debug_Count();
                    //System.Diagnostics.Debug.Assert(count == lav.RegisteredKeysLength);
#endif
                    if (hook.IsDummy is false && latestPublishCount.RemoveChunkMap != default)
                        hook.Reallocated(new(newField));

                    Volatile.Write(ref location, newField);

                    header.Release(lockTaken, addContext: TableHeader.Context.Leave);
                    lockTaken = default;

                    if (hook.IsDummy is false)
                        hook.ThrowIfExcept();
                    return newField;
                }

                header.Release(lockTaken, default);
                lockTaken = default;
            }
            finally
            {
                if (lockTaken)
                    header.Release(lockTaken, default);
                lockTaken = default;
            }

            var clearedLenAndVersion = new AtomicLengthAndVersion(
                0,
                newField.registeredLengthAndVersion.Version.IncrementTableVersion(out var overflowed)
            );

            if (overflowed)
            {
                prevCopied = 0;
                newField = new(newSize.tableSize, newSize.keysSize, source.comparer);
            }
            else
            {
                newField.publishCountAndRemoveChunkMap = default;
                newField.registeredLengthAndVersion = clearedLenAndVersion;
            }

            goto inLockRetry;
        }
    }

    private int CompactionCopyTo(
        AtomicField<TKey, TValue> destination,
        AtomicLengthAndVersion lengthAndVersion,
        CountAndChunkMap countAndChunkMap)
    {
        var size = lengthAndVersion.RegisteredKeysLength;
        var dstHashCodes = destination.hashCodes;
        var dstKeys = destination.keys;
        if (countAndChunkMap.RemoveChunkMap == default)
            AtomicField<TKey, TValue>.Copy(this, 0, destination, 0, size);
        else
        {
            var len = lengthAndVersion.RegisteredKeysLength;
            var srcHashCodes = this.hashCodes;
            var srcKeys = this.keys;

            size = (uint)len <= (uint)srcHashCodes.Length
                ? CompactionCopy(
                    this.table,
                    srcHashCodes.AsSpan(0, len),
                    srcKeys,
                    this.values,
                    dstHashCodes,
                    dstKeys,
                    destination.values,
                    lengthAndVersion.Version.Table,
                    srcHashCodes.Length,
                    countAndChunkMap.RemoveChunkMap)
                : CompactionCopy(
                    this.table,
                    (uint)len <= (uint)srcKeys.Length
                        ? srcKeys.AsSpan(0, len)
                        : srcKeys.AsSpan(),
                    srcHashCodes,
                    this.values,
                    dstKeys,
                    dstHashCodes,
                    destination.values,
                    lengthAndVersion.Version.Table,
                    srcKeys.Length,
                    countAndChunkMap.RemoveChunkMap
                );
            var comparer = destination.comparer;
            if (object.ReferenceEquals(this.comparer, comparer) is false)
                RehashingCopy(destination.keys, destination.hashCodes, size, comparer);
        }

        static int CompactionCopy<T, U, V>(
            MetaTable srcMetaTable,
            scoped ReadOnlySpan<T> keysSrc,
            U[] uSrc,
            V[] vSrc,
            T[] keysDstArray,
            U[] uDst,
            V[] vDst,
            ushort version,
            int keysMaxLength,
            int map)
            where T : notnull
        {
#if FORCE_SCAN_REMOVED_MAP
            map = ~0;
#endif
            //mock
            static int ScanMock(MetaTable srcMetaTable,
                            scoped ReadOnlySpan<T> keysSrc,
                            ushort version,
                            T[] keysDstArray = default!,
                            U[] uSrc = default!,
                            V[] vSrc = default!,
                            U[] uDst = default!,
                            V[] vDst = default!)
            {
                keysDstArray ??= [];
                uSrc ??= [];
                vSrc ??= [];
                uDst ??= [];
                vDst ??= [];

                var count = 0;
                var s = (ReadOnlySpan<Meta>)srcMetaTable.AsSpan();
                for (var i = 0; i < keysSrc.Length; i++)
                {
                    var hash = keysSrc[i].GetHashCode();
                    var comparand = s.GetBucket(hash).VolatileRead();

                    MetaSlotResult resultDump = default;
                    if ((resultDump = s.FindByKeyIndex(i, hash, version)).IsSuccessful)
                    {
                        if ((uint)count < (uint)keysDstArray.Length)
                            keysDstArray[count] = keysSrc[i];
                        if ((uint)i < (uint)uSrc.Length
                        && (uint)count < (uint)uDst.Length)
                            uDst[count] = uSrc[i];
                        if ((uint)i < (uint)vSrc.Length
                        && (uint)count < (uint)vDst.Length)
                            vDst[count] = vSrc[i];
                        count++;
                    }
                }
                return count;
            }
            return ScanMock(srcMetaTable, keysSrc, version, keysDstArray, uSrc, vSrc, uDst, vDst);
            var srcTable = (ReadOnlySpan<Meta>)srcMetaTable.AsSpan();
#if DEBUG
            var keysSrcOriginal = keysSrc;
            if ((keysSrc.Length > keysDstArray.Length && keysSrc.Length != 0)
            || (uSrc.Length > uDst.Length && keysSrc.Length != 0)
            || (vSrc.Length > vDst.Length && keysSrc.Length != 0))
                return Throw();
            var movedCount = 0;
            var debug_count = 0;
            var foundCount = 0;
            var removedCount = 0;
            var copyCount = 0;
            byte[]? rent = default;
            Span<byte> accessCounts = keysSrc.Length <= 1024
                ? stackalloc byte[keysSrc.Length]
                : (rent = System.Buffers.ArrayPool<byte>.Shared.Rent(keysSrc.Length)).AsSpan(0, keysSrc.Length);
            accessCounts.Clear();
            var unScopedLineCount = 0;
            //System.Diagnostics.Debug.Assert((keysMaxLength & minimumCapacity) == 0);
#endif
            var chunkSize = Helper.GetChunkSize(keysMaxLength, srcTable.Length);

            var offset = 0;
            var start = 0;
            var dstCount = 0;
            var lineCount = 0;
            var keysDst = keysDstArray.AsSpan();
            while (map != default)
            {
                var position = map & -map;
                map ^= position;

                if (position == default)
                    continue;

                start = chunkSize * Helper.GetIndexOfPowerOfTwo(position);

                lineCount += start - (offset + lineCount);// add skipped
#if DEBUG
                System.Diagnostics.Debug.Assert(
                    lineCount >= 0
                );
                var prevLineCount = lineCount;
#endif
                var chunkEnd = chunkSize + lineCount;
                scoped ReadOnlySpan<T> span;
                if ((uint)chunkEnd <= (uint)keysSrc.Length)
                    span = keysSrc.Slice(0, chunkEnd);
                else
                {
                    map = default;

                    span = keysSrc;
                    chunkEnd = keysSrc.Length;
                }

                var i = lineCount;
                do
                {
                    if ((uint)i >= (uint)span.Length)
                        break;
#if DEBUG
                    debug_count++;
#endif

                    var hash = span[i].GetHashCode();
                    var comparand = srcTable.GetBucket(hash).VolatileRead();
                    var idx = i + offset;

                    ++i;
#if DEBUG
                    if ((uint)idx < (uint)accessCounts.Length)
                        checked { System.Diagnostics.Debug.Assert(accessCounts[idx]++ == 0); }
                    else
                        System.Diagnostics.Debug.Fail(nameof(accessCounts));
                    MetaSlotResult resultDump = default;
#endif
                    if (comparand.Equals(idx, version)
                    || (comparand.HasEntanglement && srcTable.SpinWaitWhileHasEntanglement(hash).Equals(idx, version))
                    ||
#if DEBUG
                    (resultDump = srcTable.FindByKeyIndex(
                        idx,
                        hash,
                        version
                    )).IsSuccessful
#else
                    srcTable.FindByKeyIndex(
                        idx,
                        hash,
                        version
                    ).IsSuccessful
#endif
                    )
                    {
                        ++lineCount;
#if DEBUG
                        foundCount++;
#endif
                        continue;
                    }
#if DEBUG
                    else
                        removedCount++;
                    System.Diagnostics.Debug.Assert(
                        !resultDump.IsSuccessful
                    );
#endif
                    if (lineCount == 0)
                        continue;

                    if ((uint)dstCount > (uint)keysDst.Length
                    //|| (uint)lineCount > (uint)span.Length
                    )
                        return Throw();
                    const int scratch = 1;
                    if (span.TrySlice((i - scratch) - lineCount, lineCount).TryCopyTo(keysDst.Slice(dstCount)) is false)
                        return Throw();
                    if (uSrc.Length != 0)
                        Array.Copy(uSrc, idx - lineCount, uDst, dstCount, lineCount);
                    if (vSrc.Length != 0)
                        Array.Copy(vSrc, idx - lineCount, vDst, dstCount, lineCount);

                    dstCount += lineCount;
#if DEBUG
                    movedCount += lineCount;
                    copyCount++;
#endif
                    lineCount = 0;
                } while (true);

                chunkEnd -= lineCount;
                if ((uint)chunkEnd <= (uint)keysSrc.Length)
                    keysSrc = keysSrc.Slice(chunkEnd);
#if DEBUG
                else
                    System.Diagnostics.Debug.Fail(nameof(chunkEnd));

                unScopedLineCount = lineCount;
#endif
                offset += chunkEnd;
                //span = default;
                continue;
            }

            if (keysSrc.Length != 0)
            {
                if ((uint)dstCount > (uint)keysDst.Length
                || keysSrc.TryCopyTo(keysDst.Slice(dstCount)) is false)
                    return Throw();

                if (uSrc.Length != 0)
                    Array.Copy(uSrc, offset, uDst, dstCount, keysSrc.Length);
                if (vSrc.Length != 0)
                    Array.Copy(vSrc, offset, vDst, dstCount, keysSrc.Length);
            }
#if DEBUG
#if WIDE_LOCK
#if FORCE_SCAN_REMOVED_MAP
                    System.Diagnostics.Debug.Assert(debug_count == keysSrcOriginal.Length);
#endif

                var mockResult = ScanMock(srcMetaTable, keysSrcOriginal, version);
                System.Diagnostics.Debug.Assert(
                    mockResult == dstCount + keysSrc.Length,
                    copyCount.ToString()
                );
#endif
            if (rent is not null)
                System.Buffers.ArrayPool<byte>.Shared.Return(rent);

#endif

            return dstCount + keysSrc.Length;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static int Throw()
                => throw new ArgumentException($"destination is less than source");
        }

        destination.registeredLengthAndVersion.RegisteredKeysLength = size;
        destination.publishCountAndRemoveChunkMap.Count = size;


        lengthAndVersion = destination.registeredLengthAndVersion;
#if DEBUG
        if (lengthAndVersion.RegisteredKeysLength != size
        || ((uint)size > (uint)dstHashCodes.Length
            && destination.comparer is not null))
            throw new Exception();

        //System.Diagnostics.Debug.Assert(lengthAndVersion.RegisteredKeysLength < countAndChunkMap.Count);


        
#endif

        destination.table.AsSpan().PromptInsertRangeInPrivate(
            dstHashCodes,
            dstKeys,
            lengthAndVersion.Version.Table,
            0,
            lengthAndVersion.RegisteredKeysLength
        );

#if DEBUG
        //System.Diagnostics.Debug.Assert(destination.keys.Take(lengthAndVersion.RegisteredKeysLength).Distinct().Count() == lengthAndVersion.RegisteredKeysLength);
        System.Diagnostics.Debug.Assert(destination.Debug_Count() == (lengthAndVersion.RegisteredKeysLength, lengthAndVersion));
#endif
        return lengthAndVersion.RegisteredKeysLength;
    }


    private static void Copy(AtomicField<TKey, TValue> source, int sourceIndex, AtomicField<TKey, TValue> destination, int destinationIndex, int length)
    {
        var srcKeys = source.keys;
        var srcValues = source.values;
        var srcHashCodes = source.hashCodes;

        var dstKeys = destination.keys;
        var dstValues = destination.values;
        var dstHashCodes = destination.hashCodes;

        Array.Copy(srcKeys, sourceIndex, dstKeys, destinationIndex, length);

        if (srcValues.Length != 0)
            Array.Copy(srcValues, sourceIndex, dstValues, destinationIndex, length);
        if (srcHashCodes.Length != 0)
        {
            var dstComparer = destination.comparer;
            if (object.ReferenceEquals(source.comparer, dstComparer))
                Array.Copy(srcHashCodes, sourceIndex, dstHashCodes, destinationIndex, length);
            else
                RehashingCopy(srcKeys, dstHashCodes, destinationIndex + length, dstComparer);
        }
    }
    static void RehashingCopy(TKey[] sourceKeys, int[] destinationHashCodes, int length, IEqualityComparer<TKey>? comparer)
    {
        var span = (uint)length <= (uint)sourceKeys.Length
            ? sourceKeys.AsSpan(0, length)
            : sourceKeys.AsSpan();
        if (comparer is null)
            for (var i = 0; i < span.Length && i < destinationHashCodes.Length; ++i)
                destinationHashCodes[i] = span[i].GetHashCode();
        else
            for (var i = 0; i < span.Length && i < destinationHashCodes.Length; ++i)
                destinationHashCodes[i] = comparer.GetHashCode(span[i]);
    }
    #endregion

    internal static Snapshot CreateSnapshot(ref AtomicField<TKey, TValue> fieldLocation)
    {
        var field = Volatile.Read(ref fieldLocation);


    retry:;
        ref var header = ref field.table.Header;

        var lockTaken = header.AcquireWithDoubleWait(TableHeader.LockType.Readonly, field.SyncRoot);

        var lav = field.registeredLengthAndVersion;
        var remove = field.publishCountAndRemoveChunkMap;
        if (remove.RemoveChunkMap != default)
        {
            header.Release(lockTaken, default);
            lockTaken = default;
            field = CompactionGrow(default, ref fieldLocation, Hook.CreateDummy<TKey, TValue>());
            goto retry;
        }

        field.refCount.Increment(lav.RegisteredKeysLength);

        header.Release(lockTaken, default);
        lockTaken = default;

        return new(field, lav);
    }

    public void Dispose()
    {
        //not implemented
    }

    internal class Snapshot : IDisposable
    {
        private AtomicField<TKey, TValue> atomicField;
        private readonly AtomicLengthAndVersion lengthAndVersion;

        internal ReadOnlySpan<TKey> Keys
        {
            get
            {
                var f = this.atomicField;
                var lav = this.lengthAndVersion;
                return (uint)lav.RegisteredKeysLength <= (uint)f.keys.Length
                    ? f.keys.AsSpan(0, lav.RegisteredKeysLength)
                    : f.keys.AsSpan();
            }
        }

        internal ReadOnlySpan<TValue> Values
        {
            get
            {
                var f = this.atomicField;
                var lav = this.lengthAndVersion;
                return (uint)lav.RegisteredKeysLength <= (uint)f.values.Length
                    ? f.values.AsSpan(0, lav.RegisteredKeysLength)
                    : f.values.AsSpan();
            }
        }

        internal Snapshot(AtomicField<TKey, TValue> field, AtomicLengthAndVersion lengthAndVersion)
        {
            this.atomicField = field;
            this.lengthAndVersion = lengthAndVersion;
        }


        internal bool TryGetValue(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue value)
        {
            var field = this.atomicField;
            var lav = field.registeredLengthAndVersion.VolatileRead();
            var ret = field.KeyToSlot(key, lav, out _, MetaSlotBehavior.UnreachedVisible);

            var values = field.values;
            if (ret.IsSuccessful
            && (uint)ret.Slot.KeyIndex < (uint)values.Length)
                value = values[ret.Slot.KeyIndex];
            else
            {
                value = default;
                return false;
            }
            return field.registeredLengthAndVersion.Version.VolatileRead().RawValue == lav.Version.RawValue;
        }

        public void Dispose()
        {
            var temp = this.atomicField;
            if (temp is not null)
            {
                temp.DecrementRefCount();
                this.atomicField = null!;
            }
        }

    }

    internal readonly struct UnsafeSnapshot
    {
        private readonly AtomicField<TKey, TValue> atomicField;

        internal ReadOnlySpan<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.atomicField.keys;
        }
        internal ReadOnlySpan<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.atomicField.values;
        }

        internal UnsafeSnapshot(AtomicField<TKey, TValue> field)
        {
            this.atomicField = field;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TKey GetKey(int index)
        {
            var keys = this.atomicField.keys;
            return (uint)index < (uint)keys.Length
                ? keys[index]
                : Throw<TKey>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TValue GetValue(int index)
        {
            var values = this.atomicField.values;
            return (uint)index < (uint)values.Length
                ? values[index]
                : Throw<TValue>();
        }

        private static T Throw<T>()
            => throw new IndexOutOfRangeException();


        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(defaultKey))]
        internal TKey GetKeyOrDefault(int index, TKey defaultKey)
        {
            var keys = this.atomicField.keys;
            return (uint)index < (uint)keys.Length
                ? keys[index]
                : defaultKey;
        }

        [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(defaultValue))]
        internal TValue GetValueOrDefault(int index, TValue defaultValue)
        {
            var values = this.atomicField.values;
            return (uint)index < (uint)values.Length
                ? values[index]
                : defaultValue;
        }

        internal bool TryGetValue(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue valueResult)
        {
        retry:;
            var f = this.atomicField;
            var lav = f.registeredLengthAndVersion.VolatileRead();
            var res = f.KeyToSlot(key, lav, out _, MetaSlotBehavior.UnreachedVisible);
            var values = atomicField.values;

            if (res.IsSuccessful
            && (uint)res.Slot.KeyIndex < (uint)values.Length)
                valueResult = values[res.Slot.KeyIndex];
            else if (res.IsReadPast)
                goto retry;
            else
            {
                valueResult = default;
                return false;
            }

            return atomicField.registeredLengthAndVersion.Version.VolatileRead().RawValue == lav.Version.RawValue;
        }
    }
}

internal readonly record struct ValueUnit;


