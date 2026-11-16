using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.InLockHook.Interface;

internal interface IInLockClearHook
{
    void Clear(int oldCount);
}
