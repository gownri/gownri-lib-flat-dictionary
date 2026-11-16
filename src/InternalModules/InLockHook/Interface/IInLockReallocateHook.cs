using FlatDictionary.InternalModules.Atomic;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IInLockReallocateHook<TKey, TValue>
    where TKey : notnull
{
    void Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot);
}
