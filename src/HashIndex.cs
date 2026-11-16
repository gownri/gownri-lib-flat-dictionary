using FlatDictionary.InternalModules;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Threading;
using System.Diagnostics;
namespace IndexFriendlyCollections;


public class HashIndex<TKey> : IReadOnlyDictionary<TKey, int>, IDisposable
    where TKey : notnull, IEquatable<TKey>
{
    public const int SupportedTableSize = 0x4000_0000;
    public const int SupportedMaxCapacity = SupportedTableSize - 1;
    public System.Collections.Generic.IEnumerable<string> HashTableDebugDisplay()
    {
        var table = this.table;
        foreach (var v in table)
            yield return $"key:{((uint)v.KeyIndex < (uint)this.keys.Length
                    ? this.keys[v.KeyIndex].ToString()
                    : "null"
                ),12} {{{v}}}";

    }

#if DEBUG
    public int MaxCollisions { get; private set; }
#endif
    private FlatDictionary.InternalModules.TableVersion version;
    private Meta[] table;
    private TKey[] keys;
    internal ref TKey[] KeysRef
        => ref this.keys;
    private int count = 0;
    public int HashTableSize => this.table.Length;
    public int Capacity => this.keys.Length;
    public int Count => this.count;
    public ReadOnlySpan<TKey> Keys => this.keys.AsSpan(0, this.count);
    public uint VersionToken => this.version.RawValue;
    public HashIndex(int capacity = Helper.KeysFloor, int hashTableSize = -1, bool usePoolingTable = true)
    {
        this.version = FlatDictionary.InternalModules.TableVersion.Create();
        this.table = Array.Empty<Meta>(); 
        this.keys = Array.Empty<TKey>();
        hashTableSize = hashTableSize > capacity
            ? hashTableSize
            : capacity * 3 | Helper.TableFloor;
        this.Expand(capacity, hashTableSize, false, usePoolingTable);
#if DEBUG
        this.table.AsSpan().Fill(new(unchecked((int)0xABADBEEF), default));
#endif
    }

    public void Expand(int newKeysCapacity, int newTableSize = 0, bool forceRehash = false, bool usePoolingTable = true)
    {
        if ((uint)newKeysCapacity > (uint)SupportedMaxCapacity)
            ThrowKeysCapacity();

        forceRehash |= this.ExpandCore(newKeysCapacity, newTableSize, usePoolingTable);
        if (forceRehash)
            this.Rehash();

        static void ThrowKeysCapacity()
            => throw new ArgumentOutOfRangeException($"arg:{nameof(newKeysCapacity)} was outside the range of 0 to {nameof(SupportedMaxCapacity)}:{SupportedMaxCapacity}.");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>IsNeedRehash(hashTableExpanded)</returns>
    private bool ExpandCore(int newKeysCapacity, int newTableSize = -1, bool usePoolingTable = true)
    {
        var keys = this.keys;
        var table = this.table;
        if (newKeysCapacity > keys.Length)
        {
            var newKeys = new TKey[newKeysCapacity];
            Array.Copy(keys, newKeys, keys.Length);
            this.keys = newKeys;
            keys = newKeys;
        }

        if(keys.Length >= table.Length
            && keys.Length >= newTableSize)
        {
            newKeysCapacity = keys.Length * 2;
            if((uint)newKeysCapacity > (uint)SupportedTableSize)
            {
                newKeysCapacity = SupportedTableSize;
                if (newKeysCapacity < keys.Length)
                    return NotSupported();
                static bool NotSupported()
                    => throw new NotSupportedException();
            }

            newTableSize = Math.Max(newTableSize, newKeysCapacity);
        }
        
        if (newTableSize > table.Length)
        {
            HashTablePool.Return(table, this.version);
            if (usePoolingTable)
                table = HashTablePool.Rent(newTableSize, out this.version);
            else
            {
                table = new Meta[Helper.GetTableSize(newTableSize)];
                this.version = FlatDictionary.InternalModules.TableVersion.Create();
            }
            this.table = table.Length == 0
                ? new Meta[Helper.GetTableSize(newTableSize)]
                : table;

            return true;
        }

        return false;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Rehash()
    {
        var keys = new ReadOnlySpan<TKey>(this.keys, 0, this.count);
        this.Clear();
        if (keys.Length > 0)
        {
            var dummyHeader = new TableHeader(this.version.Table);
            this.table.AsSpan().Rehash(
                keys,
                this.version.Table
            );
        }
        this.count = keys.Length;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(bool ClearAllKeys)
    {
        this.Clear();
        if (ClearAllKeys)
            Array.Clear(this.keys, 0, this.keys.Length);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        this.count = 0;
        this.version = this.version.IncrementTableVersion(out var isOverflow);
        if (isOverflow)
            Array.Clear(this.table, 0, this.table.Length);
#if DEBUG
        this.MaxCollisions = 0;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetIndex(TKey key,[MaybeNullWhen(false)] out int index)
    {
        var table = this.table.AsSpan();
        var keys = this.keys.AsSpan(..this.count);
        var version = this.version.Table;

        var indexL = table.InitialProbe(key, version, out var context);
        if ((uint)indexL < (uint)keys.Length
            && keys[indexL].Equals(key))
        {
            index = indexL;
            return true;
        }

        index = table.FindOrInserted(
            keys,
            key,
            context,
            version,
            -1,
            InsertBehavior.None,
            out var exist,
            out _
        );

        return exist;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(TKey key, out bool exist)
    {
        var table = this.table.AsSpan();
        var next = this.count;
        var keys = this.keys.AsSpan(0, next);
        var version = this.version.Table;

        var index = table.InitialProbe(key, version, next, out var context);
        if (exist = ((uint)index < (uint)keys.Length
            && keys[index].Equals(key)))
            return index;
        else if (index == next)
            return Setup(key, 0);

        index = table.FindOrInserted(
            keys,
            key,
            context,
            version,
            next,
            InsertBehavior.Insert,
            out exist,
            out var collisionCount
        );

        if (exist)
            return index;
        else
            return Setup(key, collisionCount);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Setup(TKey setKey, int collisionCount)
        {
    #if DEBUG
            this.MaxCollisions = Math.Max(collisionCount, this.MaxCollisions);
    #endif
            var keys = this.keys;
            var keyIndex = this.count++;
            if ((uint)keyIndex >= (uint)keys.Length
                || collisionCount > Helper.CollisionTolerance)
                ExpandAdd(keyIndex, setKey, collisionCount);
            else
                keys[keyIndex] = setKey;
            return keyIndex;

            [MethodImpl(MethodImplOptions.NoInlining)]
            void ExpandAdd(int index, TKey setKey, int collisionCount)
            {
                var keys = this.keys;
                int tableSize = this.table.Length;

                var isRehashing = collisionCount > Helper.CollisionTolerance;
                int newKeysSize;
                int newTableSize;
                if(isRehashing 
                    && (uint)index < (uint)keys.Length)
                {
                    newKeysSize = keys.Length;
                    newTableSize = tableSize < keys.Length * 2
                        ? tableSize * 2
                        : tableSize;
                }else
                {
                    newKeysSize = 
                        keys.Length == 0 
                        ? Helper.KeysFloor
                        : keys.Length * 2;
                    newTableSize = tableSize;
                }
                isRehashing = this.ExpandCore(
                    newKeysSize,
                    newTableSize
                );
                if((uint)index < (uint)keys.Length
                    && object.ReferenceEquals(keys, this.keys))
                    keys[index] = setKey;
                else
                    this.keys[index] = setKey;
                if (isRehashing)
                    this.Rehash();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex_Test(TKey key, out bool exist)
    {
        throw new NotImplementedException();
//        var table = this.table.AsSpan();
//        var next = this.count;
//        var keys = new ReadOnlySpan<TKey>(this.keys, 0, next);
//        var version = this.version.Table;
//        var hash = key.GetHashCode();
//        (var index, var comparand) = table.InitialProbe(hash, version);
//        if (exist = (uint)index < (uint)keys.Length)
//        {
//            if (key.Equals(keys[index]))
//            {
//                return index;
//            }
//        }
//        else
//        {
//            return Setup(key, 0, MetaSlotResult.Progress(hash & (table.Length - 1), comparand, Meta.Data.CreateEntry(hash, version)));
//        }

//        var result = table.FindOrInsertPoint(keys, key, hash, version);
//        if (exist = result.Entry.IsDefault)
//            return result.Slot.KeyIndex;
//        else
//            return Setup(key, default, result);

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        int Setup(TKey setKey, int collisionCount, MetaSlotResult result)
//        {
//#if DEBUG
//            this.MaxCollisions = Math.Max(collisionCount, this.MaxCollisions);
//#endif
//            var keys = this.keys;
//            var keyIndex = this.count++;
//            if ((uint)keyIndex >= (uint)keys.Length
//                || collisionCount > Helper.CollisionTolerance)
//                ExpandAdd(keyIndex, setKey, collisionCount);
//            else
//            {
//                keys[keyIndex] = setKey;
//                //this.table.AsSpan().InstantInsert(result.SlotIndex, this.version.Table, new(keyIndex, result.Entry), result.Slot);
//            }
//            return keyIndex;

//            [MethodImpl(MethodImplOptions.NoInlining)]
//            void ExpandAdd(int index, TKey setKey, int collisionCount)
//            {
//                var keys = this.keys;
//                int tableSize = this.table.Length;

//                var isRehashing = collisionCount > Helper.CollisionTolerance;
//                int newKeysSize;
//                int newTableSize;
//                if (isRehashing
//                    && (uint)index < (uint)keys.Length)
//                {
//                    newKeysSize = keys.Length;
//                    newTableSize = tableSize < keys.Length * 2
//                        ? tableSize * 2
//                        : tableSize;
//                }
//                else
//                {
//                    newKeysSize =
//                        keys.Length == 0
//                        ? Helper.KeysFloor
//                        : keys.Length * 2;
//                    newTableSize = tableSize;
//                }
//                isRehashing = this.ExpandCore(
//                    newKeysSize,
//                    newTableSize
//                );
//                if ((uint)index < (uint)keys.Length
//                    && object.ReferenceEquals(keys, this.keys))
//                    keys[index] = setKey;
//                else
//                    this.keys[index] = setKey;
//                var dummyHeader = new TableHeader(this.version.Table);
//                if (isRehashing)
//                    this.table.AsSpan().Rehash(new ReadOnlySpan<TKey>(this.keys, 0, this.count), this.version.Table);
//            }
//        }
    }

    bool IReadOnlyDictionary<TKey, int>.TryGetValue(TKey key, out int value)
        => this.TryGetIndex(key, out value);
    bool IReadOnlyDictionary<TKey, int>.ContainsKey(TKey key)
        => this.TryGetIndex(key, out _);
    IEnumerable<TKey> IReadOnlyDictionary<TKey, int>.Keys
        => this.keys.Take(this.count).Where(key => this.TryGetIndex(key, out _));
    int IReadOnlyDictionary<TKey, int>.this[TKey key]
        => this.TryGetIndex(key, out var result) ? result : throw new KeyNotFoundException();
    IEnumerable<int> IReadOnlyDictionary<TKey, int>.Values
        => Enumerable.Range(0, this.count).Where(index => this.TryGetIndex(this.keys[index], out _));
    IEnumerator<KeyValuePair<TKey, int>> IEnumerable<KeyValuePair<TKey, int>>.GetEnumerator()
    {
        foreach (var item in ((IReadOnlyDictionary<TKey, int>)this).Values.Select(index => KeyValuePair.Create(this.keys[index], index)))
            yield return item;
    }

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<KeyValuePair<TKey,int>>)this).GetEnumerator();
    
    public void Dispose()
    {
        this.count = -1;
        if (this.table is not null)
        {
            HashTablePool.Return(this.table, this.version);
            this.table = null!;
        }

        this.version = default;
        this.keys = Array.Empty<TKey>();
    }
}
