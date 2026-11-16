using FlatDictionary.InternalModules.Atomic;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

internal readonly struct ObserveContext<TKey, TValue>
    where TKey : notnull
{
    private readonly AtomicField<TKey, TValue> field;
    internal readonly int RemovedIndex;
    internal readonly int AddedIndex;

}


internal enum ObserveBehavior : byte
{
    Add,
    Remove,
    Update,
    Clear,
    Compaction,
}