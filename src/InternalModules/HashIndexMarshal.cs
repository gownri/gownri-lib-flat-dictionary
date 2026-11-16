using IndexFriendlyCollections;
using System;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

internal static class HashIndexMarshal
{

    public static TKey[] SwapKeyArray<TKey>(HashIndex<TKey> hashIndex, TKey[] array, int tableSize)
        where TKey : notnull, IEquatable<TKey>
    {
        array ??= Array.Empty<TKey>();
        ref var keysRef = ref hashIndex.KeysRef;
        var len = array.Length;
        var ret = keysRef;
        keysRef = array;
        hashIndex.Expand(len, tableSize, true);
        return ret;
    }

    public static TKey[] SwapKeyArray<TKey>(HashIndex<TKey> hashIndex, TKey[] array)
        where TKey : notnull, IEquatable<TKey>
        => SwapKeyArray(hashIndex, array, (array?.Length ?? 0) * 3);
    public static TKey[] DetachKeysArray<TKey>(HashIndex<TKey> hashIndex)
        where TKey : notnull, IEquatable<TKey>
    {
        ref var keys = ref hashIndex.KeysRef;
        var ret = keys;
        keys = Array.Empty<TKey>();
        hashIndex.Clear();
        return ret;
    }
}
