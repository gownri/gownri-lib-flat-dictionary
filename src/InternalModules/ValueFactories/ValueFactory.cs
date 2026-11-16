using FlatDictionary.InternalModules.Interface;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlatDictionary.InternalModules.ValueFactories;

internal static class ValueFactory
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueWrap<TKey, TValue> Create<TKey, TValue>(TKey key, TValue value)
        => new(key, value);
}


[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct ValueWrap<TKey, TValue>(TKey key, TValue value) : IAddValueFactory<TKey, TValue>
{
    //readonly TKey IAddValueFactory<TKey, TValue>.Key 
    //{
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)] get; 
    //} = key;
    private readonly TValue value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly TValue IAddValueFactory<TKey, TValue>.Invoke(TKey key)
         => this.value;
}