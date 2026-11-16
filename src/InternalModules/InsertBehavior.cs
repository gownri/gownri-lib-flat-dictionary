using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

[Flags]
internal enum InsertBehavior : byte
{
    None = 0,
    SetTombstone = 1 << 0,
    Insert = 1 << 1,

    IncludeTombstone = 1 << 6,
    LogicalLocked = 1 << 7,
}