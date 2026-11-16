using FlatDictionary.InternalModules.Atomic;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IHookPolicy
{
    internal bool IsDummy { get; }

    internal void ThrowIfExcept();
}

internal interface IRegistrationPolicy : IHookPolicy
{
    internal bool IsUpdate { get; }
}