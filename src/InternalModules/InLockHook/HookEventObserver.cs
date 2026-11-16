using FlatDictionary.InternalModules.Atomic;
using FlatDictionary.InternalModules.InLockHook.Interface;

using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook;

public class HookEventObserver<TKey, TValue> : IInLockAllHook<TKey, TValue>
    where TKey : notnull
{
    public event Action<TKey, TValue>? OnAdd;
    public event Action<TKey, TValue>? OnRemove;
    public event Action<TKey, (TValue oldValue, TValue newValue)>? OnUpdate;
    public event Action<int>? OnClear;
    public event Action? OnReallocate;

    void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        => this.OnAdd?.Invoke(key, value);
    void IInLockRegistrationHook<TKey, TValue>.Update(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot, int removedIndex, int addedIndex)
        => this.OnUpdate?.Invoke(
            snapshot.GetKey(addedIndex),
            (
                snapshot.GetValue(removedIndex),
                snapshot.GetValue(addedIndex)
            )
        );
    void IInLockRemoveHook<TKey, TValue>.Remove(TKey key, TValue value, int index)
        => this.OnRemove?.Invoke(key, value);

    void IInLockClearHook.Clear(int oldCount)
        => this.OnClear?.Invoke(oldCount);

    void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        => this.OnReallocate?.Invoke();
}
