using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IInLockRemoveHook<TKey, TValue>
{
    void Remove(TKey key, TValue value, int index);
}
