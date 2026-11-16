using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.Interface;

internal interface IAddValueFactory<TKey, TValue>
{
    TValue Invoke(TKey key);
}
