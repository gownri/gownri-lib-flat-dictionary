using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

//internal static class TableAsSpanExtensions
//{
//    const string m = "this method is not supported";
//    internal static Span<Meta> AsSpan(this Meta[] table)
//        => MemoryExtensions.AsSpan(table, table.Length & 1/*out sentinel*/);
//    //overrides to do not support
//    [Obsolete(m)]
//    internal static Span<Meta> AsSpan(this Meta[] table, int length)
//        => throw new NotSupportedException();
//    [Obsolete(m)]
//    internal static Span<Meta> AsSpan(this Meta[] table, Index index)
//        => throw new NotSupportedException();
//    [Obsolete(m)]
//    internal static Span<Meta> AsSpan(this Meta[] table, Range range)
//        => throw new NotSupportedException();
//    [Obsolete(m)]
//    internal static Span<Meta> AsSpan(this Meta[] table, int start, int length)
//        => throw new NotSupportedException();
//}
