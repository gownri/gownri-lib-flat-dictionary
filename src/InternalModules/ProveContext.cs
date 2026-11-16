using System;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal readonly struct ProveContext
{
    //private const int HashFlag = 1 << (Meta.Data.DistanceOffset + 7);
    internal readonly int Hash;
    internal ProveContext(int hash, bool isInitialProved)
    {
        //this.Hash = isInitialProved ? hash : Test(hash);
        this.Hash = hash;
    }
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]

    //public override readonly int GetHashCode() => this.Hash;//HashCode.Combine(this.Hash);
    //internal ProveContext(int hash, ushort version)
    //{
    //    var metaData = Meta.Data.CreateEntry(hash, version);
    //    this.Hash = Unsafe.As<Meta.Data, int>(ref metaData);
    //}
}

