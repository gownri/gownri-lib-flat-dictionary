using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlatDictionary.InternalModules.Interface;

internal interface IComparerProxy<TKey> : IEquatable<TKey>
    where TKey : notnull
{
    internal TKey Key 
    { 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }
}
