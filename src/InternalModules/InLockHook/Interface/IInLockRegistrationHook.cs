using FlatDictionary.InternalModules.Atomic;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IInLockRegistrationHook<TKey, TValue>
    where TKey : notnull
{
    void Add(TKey key, TValue value, int index);
    void Update(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot, int removedIndex, int addedIndex);
}
