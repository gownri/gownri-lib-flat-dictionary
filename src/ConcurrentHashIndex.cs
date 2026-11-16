//using HashIndex.InternalModules;
//using HashIndex.InternalModules.Atomic;
//using HashIndex.InternalModules.Interface;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics.CodeAnalysis;
//using System.Runtime.CompilerServices;
//using System.Threading;

//namespace HashIndex;

//public class ConcurrentHashIndex<TKey>
//    where TKey : notnull, IEquatable<TKey>
//{
//    private AtomicField<TKey, ValueUnit> atomicField;
//    public int GetIndex(TKey key, out bool exist)
//    {
//        var atomicField = this.atomicField;
//        var comparer = atomicField.comparer;

//        if (typeof(TKey).IsValueType)
//        {

//        }
//        else
//        {

//        }

//    }

//    private int GetIndexCore<TProxy>(scoped in TProxy proxy, AtomicField<TKey, ValueUnit> atomicField, AtomicCountAndVersion countAndVersion, FindInsertBehavior behavior)
//        where TProxy : IComparerProxy<TKey>
//    {
//        var keys = atomicField.keys;
//        var table = atomicField.table;

//        var keysSpan = new ReadOnlySpan<TKey>(keys, 0, countAndVersion.Count);
//        var tableSpan = table.AsSpan();
//        var hash = proxy.GetHashCode();
//        var version = countAndVersion.Version.Table;
//        var (index, comparand) = tableSpan.InitialProbe(hash, version);
//        MetaSlotResult result;
//        index &= ~(int)(behavior & FindInsertBehavior.UnreachedVisible);// ~(default || int.MinValue)
//        if ((uint)index >= (uint)keysSpan.Length)
//            result = MetaSlotResult.Progress(hash, comparand, Meta.Data.CreateEntry(hash, version));
//        else
//        {
//            if (proxy.Equals(keysSpan[index]))
//                return index;

//            result = tableSpan.FindOrInsertPoint(keysSpan, proxy, hash, version, behavior);
//            if (!result.IsInsertPointResult)
//                return result.Slot.KeyIndex;
//        }

//        if ((behavior & FindInsertBehavior.Insert) == default)
//            return -1;

//        return this.AddKey(
//    }

//    private int AddKey(
//        MetaSlotResult result, 
//        TKey key, 
//        int hash,
//        TKey[] keys, 
//        MetaTable table,
//        scoped Span<Meta> tableSpan,
//        AtomicCountAndVersion countAndVersion, 
//        AtomicField<TKey, ValueUnit> atomicField
//    )
//    {
//        scoped ref var header = ref table.Header;

//        var lockTaken = header.TryAcquire(TableHeader.LockType.Registration);
//        if (!lockTaken)
//            lockTaken = Wait(ref header);
//        TableHeader.LockTaken Wait(scoped ref TableHeader header)
//        {
//            throw new NotImplementedException();
//        }
//        if (object.ReferenceEquals(this.atomicField, atomicField)
//        && atomicField.VolatileCountAndVersion().AsPrimitive == countAndVersion.AsPrimitive)
//        {
//            var index = countAndVersion.Count + 1;
//            if ((uint)index < (uint)keys.Length)
//            {
//                keys[index] = key;
//            }
//        }


//    }
//}

