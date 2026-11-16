using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

public interface ILockedOvertimeInitialization<TKey, TArg>
{
    void Execute(TKey addedKey, int indexOfAddedKey, TArg arg);
}
