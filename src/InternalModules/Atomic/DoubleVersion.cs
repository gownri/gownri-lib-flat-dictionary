using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules.Atomic;

internal readonly struct DoubleVersion(ushort keys, ushort table)
{
    public readonly ushort KeyArray = keys;
    public readonly ushort HashTable = table;
}
