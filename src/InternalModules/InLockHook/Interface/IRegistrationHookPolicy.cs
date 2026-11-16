using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IRegistrationHookPolicy<TKey, TValue> 
    : IRegistrationPolicy,
      IInLockRegistrationHook<TKey, TValue>, 
      IInLockReallocateHook<TKey, TValue>
    where TKey : notnull;
