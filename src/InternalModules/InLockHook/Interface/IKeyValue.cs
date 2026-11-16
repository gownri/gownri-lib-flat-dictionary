using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IKeyValue<TKey, TValue>
    where TKey : notnull
{
    TKey Key { get; }
    TValue Value { get; }
}


internal interface IKey<TKey>
    where TKey : notnull
{
    TKey Key { get; }
}