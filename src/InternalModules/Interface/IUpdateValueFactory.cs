using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules.Interface;

internal interface IUpdateValueFactory<TKey, TValue>
{
    TValue UpdateValueFactory(TKey key, TValue oldValue);
}
