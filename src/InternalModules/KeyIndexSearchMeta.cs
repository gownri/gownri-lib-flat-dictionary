using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text;

namespace FlatDictionary.InternalModules;

//internal readonly struct KeyIndexSearchMeta : IEquatable<KeyIndexSearchMeta>
//{
//    public readonly Meta Inner;

//    private KeyIndexSearchMeta(int index, int version)
//    {
//        var offset = (uint)version << Meta.VersionOffset;
//        this.Inner = new(index, Unsafe.As<uint, Meta.Data>(ref offset));
//    }
//    public readonly bool Equals(KeyIndexSearchMeta other) 
//        => this.Inner.KeyIndex == other.Inner.KeyIndex 
//        && this.Inner.DataReader.Version == other.Inner.DataReader.Version;
//    public static implicit operator KeyIndexSearchMeta((int index, int version) tuple)
//        => new(tuple.index, tuple.version);
//}
