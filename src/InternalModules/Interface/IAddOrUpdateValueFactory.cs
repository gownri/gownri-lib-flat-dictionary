using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.Interface;

internal interface IAddOrUpdateValueFactory<TKey, TValue>
{
    TKey Key { get; }
    TValue AddValueFactory();
    TValue UpdateValueFactory(TValue oldValue);
}
