using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IInLockAllHook<TKey, TValue>
    : IInLockRegistrationHook<TKey, TValue>,
      IInLockReallocateHook<TKey, TValue>,
      IInLockRemoveHook<TKey, TValue>,
      IInLockClearHook
    where TKey : notnull
{

}
