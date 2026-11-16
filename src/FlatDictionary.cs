using FlatDictionary.InternalModules.Atomic;
using FlatDictionary.InternalModules.InLockHook;
using FlatDictionary.InternalModules.InLockHook.Interface;
using FlatDictionary.InternalModules.Interface;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Gownri.IndexFriendly.Collections;

public class FlatDictionary<TKey, TValue>
    where TKey : notnull
{
    private AtomicField<TKey, TValue> atomicField;
    public ReadOnlySpan<TValue> Values
        => this.atomicField.Values;
    public ReadOnlySpan<TKey> Keys
        => this.atomicField.Keys;
    public int Count
        => this.atomicField.Count;

    private HookEventObserver<TKey, TValue>? hookObserver;
    public HookEventObserver<TKey, TValue> HookObserver
    {
        get => this.hookObserver ??= new();
    }


    internal void DEBUG_Check(IEnumerable<TKey>? valid = default)
    {
        this.atomicField.Debug_CollisionCount();
#if DEBUG
        if (this.atomicField.Debug_Count().count == this.atomicField.Count)
            return;
        var v = this.atomicField.VolatileReadVersion();
        
        System.Diagnostics.Debug.Fail($"{this.atomicField.Debug_Versions(v.Table).Count()
            }, {DateTime.Now.ToString("hh:mm:ss:fff")
            }, re: {(valid is null ? default : this.atomicField.Debug_Removed(valid))
            }"
        );

#endif

    }
    public FlatDictionary() : this(0, null) { }
    public FlatDictionary(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        this.atomicField = new(capacity, comparer, loadFactor: FlatDictionary.InternalModules.LoadFactor.Fill);
    }
    public int GetIndex<TArg>(TKey key, TValue value, TArg arg)
    {
        return AtomicField<TKey, TValue>.GetIndex(ref this.atomicField, key, new Factory(value), this.hookObserver);
    }
    public int GetIndex(TKey key, TValue value)
    {
        return AtomicField<TKey, TValue>.GetIndex(ref this.atomicField, key, new Factory(value), this.hookObserver);
    }
    public TValue GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TValue> valueFactory, TArg arg)
    {
        return AtomicField<TKey, TValue>.GetOrAdd(
            ref this.atomicField,
            key,
            new Factory<TArg>(valueFactory, arg),
            this.hookObserver
        );
    }


    public TValue GetOrAdd(TKey key, TValue value)
    {
        return AtomicField<TKey, TValue>.GetOrAdd(
            ref this.atomicField,
            key,
            new Factory(value),
            //value,
            this.hookObserver
        );
    }

    //public TValue GetOrAddDummy(TKey key, TValue value)
    //{
    //    //if (!AtomicField<TKey, TValue>.TryGetValue(ref this.atomicField, key, out var outValue))
    //    //    //    return this.GetOrAdd(key, value);
    //    //    return default!;

    //    //return outValue;

    //    return AtomicField<TKey, TValue>.GetOrAddDummy(ref this.atomicField, key, new Factory(value), this.hookObserver);
    //    //return AtomicField<TKey, TValue>.GetOrAdd(ref this.atomicField, key, value);
    //}

    public bool TryGetValue(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue value)
    {
        return AtomicField<TKey, TValue>.TryGetValue(in this.atomicField, key, out value);
    }


    public bool TryRemove(TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] scoped out TValue value)
    {

        return AtomicField<TKey, TValue>.TryRemove(
            ref this.atomicField,
            key,
            out value,
            this.hookObserver
        );
    }

    public bool TryRemove(KeyValuePair<TKey, TValue> item)
    {
        return AtomicField<TKey, TValue>.TryRemove(ref this.atomicField, item, this.hookObserver);
    }

    public void Clear()
    {
        AtomicField<TKey, TValue>.Clear(ref this.atomicField, this.hookObserver);
    }
    readonly struct Factory<TArg>(Func<TKey, TArg, TValue> valueFactory, TArg arg) : IAddValueFactory<TKey, TValue>
    {
        private readonly Func<TKey, TArg, TValue> valueFactory = valueFactory;
        private readonly TArg arg = arg;

        public readonly TValue Invoke(TKey key)
            => this.valueFactory(key, this.arg);
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly struct Factory(TValue value) : IAddValueFactory<TKey, TValue>
    {
        private readonly TValue value = value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TValue Invoke(TKey key)
            => this.value;
    }
}