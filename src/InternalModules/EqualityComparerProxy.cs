using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlatDictionary.InternalModules;

internal static class EqualityComparerProxy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EqualityComparerProxy<TItem, TComparer> Create<TItem, TComparer>(TItem value, TComparer comparer)
        where TComparer : notnull, IEqualityComparer<TItem>
        => new(value, comparer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EqualityComparerDefaultProxy<TItem> Create<TItem>(TItem value)
        => new(value);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct EqualityComparerProxy<TItem, TComparer>(TItem value, TComparer comparer) : IEquatable<TItem>
    where TComparer : notnull, IEqualityComparer<TItem>
{
    internal readonly TItem Value = value;
    internal readonly TComparer Comparer = comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TItem other)
        => this.Comparer.Equals(this.Value, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => this.Comparer.GetHashCode(this.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override bool Equals(object obj)
        => obj is TItem item && this.Equals(item);
}

internal readonly struct EqualityComparerDefaultProxy<TItem>(TItem value) : IEquatable<TItem>
{
    internal readonly TItem Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TItem other)
        => EqualityComparer<TItem>.Default.Equals(this.Value, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => EqualityComparer<TItem>.Default.GetHashCode(this.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override bool Equals(object obj)
        => obj is TItem item && this.Equals(item);
}
