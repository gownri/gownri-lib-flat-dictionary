using FlatDictionary.InternalModules;
using FlatDictionary.InternalModules.Atomic;
using FlatDictionary.InternalModules.InLockHook.Interface;
using FlatDictionary.InternalModules.Interface;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlatDictionary.InternalModules.Atomic;

internal static class AtomicCompareProxy
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetHashCode<TKey>(TKey key, IEqualityComparer<TKey>? comparer)
        where TKey : notnull
        => !typeof(TKey).IsValueType
            ? comparer!.GetHashCode(key)
            : comparer is null
                ? key.GetHashCode()
                : comparer.GetHashCode(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equals<TKey>(TKey x, TKey y, IEqualityComparer<TKey>? comparer)
        => !typeof(TKey).IsValueType
            ? comparer!.Equals(x, y)
            : comparer is null
                ? EqualityComparer<TKey>.Default.Equals(x, y)
                : comparer.Equals(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WordSizeKeyDefaultComparerProxy<TKey> CreateInWordSize<TKey>(TKey key)
        where TKey : notnull
        => new(key);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static WordSizeKeyComparerProxy<TKey> CreateInWordSize<TKey>(TKey key, IEqualityComparer<TKey> comparer)
        where TKey : notnull
        => new(key, comparer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OutSizeStructDefaultComparerProxy<TKey, TValue> CreateOutWordSize<TKey, TValue>(
        TKey key,
        AtomicField<TKey, TValue> atomicField,
        TableVersion comparand
    )
        where TKey : notnull
        => new(key, atomicField, comparand);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OutSizeStructComparerProxy<TKey, TValue> CreateOutWordSize<TKey, TValue>(
        TKey key,
        IEqualityComparer<TKey> comparer,
        AtomicField<TKey, TValue> atomicField,
        TableVersion comparand
    )
        where TKey : notnull
        => new(key, atomicField, comparand, comparer);
}

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct WordSizeKeyDefaultComparerProxy<TKey>(TKey key) : IComparerProxy<TKey>
    where TKey : notnull
{
    private readonly TKey key = key;
    readonly TKey IComparerProxy<TKey>.Key
        => this.key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TKey other)
        => EqualityComparer<TKey>.Default.Equals(this.key, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => this.key.GetHashCode();
}

[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct WordSizeKeyComparerProxy<TKey>(TKey key, IEqualityComparer<TKey> comparer) : IComparerProxy<TKey>
    where TKey : notnull
{
    private readonly IEqualityComparer<TKey> comparer = comparer;
    private readonly TKey key = key;

    readonly TKey IComparerProxy<TKey>.Key
        => this.key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TKey other)
        => this.comparer.Equals(this.key, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => this.comparer.GetHashCode(this.key);
}

[StructLayout(LayoutKind.Auto)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct OutSizeStructDefaultComparerProxy<TKey, TValue>(
    TKey key, 
    AtomicField<TKey, TValue> atomicField,
    TableVersion comparand
) : IComparerProxy<TKey>
    where TKey : notnull
{
    private readonly TableVersion comparand = comparand;
    private readonly AtomicField<TKey, TValue> atomicField = atomicField;
    private readonly TKey key = key;

    readonly TKey IComparerProxy<TKey>.Key
        => this.key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TKey other)
        => this.atomicField.VolatileReadVersion().RawValue == this.comparand.RawValue 
        && EqualityComparer<TKey>.Default.Equals(this.key, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => this.key.GetHashCode();
}

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
[StructLayout(LayoutKind.Auto)]
internal readonly struct OutSizeStructComparerProxy<TKey, TValue>(
    TKey key,
    AtomicField<TKey, TValue> atomicField,
    TableVersion versionComparand,
    IEqualityComparer<TKey> comparer
) : IComparerProxy<TKey>
    where TKey : notnull
{
    private readonly TableVersion versionComparand = versionComparand;
    private readonly AtomicField<TKey, TValue> atomicField = atomicField;
    private readonly IEqualityComparer<TKey> comparer = comparer;
    private readonly TKey key = key;

    readonly TKey IComparerProxy<TKey>.Key
        => this.key;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(TKey other)
        => this.atomicField.VolatileReadVersion().RawValue == this.versionComparand.RawValue
        && this.comparer.Equals(this.key, other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
        => this.comparer.GetHashCode(this.key);
}