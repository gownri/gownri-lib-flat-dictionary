//#define UNION
//#define META_UNION
#define PRIMITIVE
using FlatDictionary.InternalModules.Atomic;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace FlatDictionary.InternalModules;
#if !PRIMITIVE
    #if !UNION && !META_UNION
    internal static class MetaExtensions
    {
        extension(Meta meta)
        {

            internal long PrimitivePackage
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Unsafe.As<Meta, long>(ref meta);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential, Size = sizeof(long))]
    #else
    [StructLayout(LayoutKind.Explicit, Size = sizeof(long))]
    #endif
#endif
internal readonly struct Meta
{
    internal const int KeyIndexFieldOffset = sizeof(int) * 8;
    internal const int DataFieldOffset = 0;

    private const long KeyIndexMask = (long)uint.MaxValue << KeyIndexFieldOffset;
    internal const int UnreachableFlag = int.MinValue;
    private const long UnreachableFlagToPrimitive = unchecked((long)(uint)UnreachableFlag << KeyIndexFieldOffset);
    private const long NotKeyIndexComparer = unchecked((long)(uint)int.MaxValue << KeyIndexFieldOffset);

    // share with meta.data
    private const uint EntanglementFlag = 0b1;
    private const uint SetEntanglementMask = ~EntanglementFlag;

    private const long EntanglementFlagToPrimitive = (long)EntanglementFlag << DataFieldOffset;
    private const long SetEntanglementMaskToPrimitive = (long)SetEntanglementMask << DataFieldOffset;
    private const long UnsetEntanglementToPrimitive = (long)EntanglementFlag << DataFieldOffset;

    private const int VersionOffset = sizeof(byte) * 8 + sizeof(byte) * 8;
    private const int VersionMask = (int)ushort.MaxValue << VersionOffset;
    // end share

    private const long KeyIndexAndVersionMask = KeyIndexMask | ((long)VersionMask << DataFieldOffset);
    private static Meta UndefinedEntanglement { get; } = new((long)Meta.Data.UndefinedEntanglement.RawData << DataFieldOffset);

    //use for atomic comparand and read/write value of Interlocked Methods
#if UNION || META_UNION
    [FieldOffset(0)]
    internal readonly long PrimitivePackage;

    //[FieldOffset(KeyIndexFieldOffset)]
    internal readonly int KeyIndex;

    //union
    [FieldOffset(DataFieldOffset)]
    internal readonly uint RawData;
    [FieldOffset(sizeof(int))]
    internal readonly Data DataReader;
#else
#if PRIMITIVE
    private readonly long primitive;
    internal readonly long PrimitivePackage
        => this.primitive;
#else
    private readonly int keyIndex;
    private readonly Meta.Data dataReader;
#endif
    internal readonly int KeyIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => (int)(this.primitive >>> KeyIndexFieldOffset);
#else
        get => this.keyIndex;
#endif
    }
    internal readonly Data DataReader
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => Meta.Data.FromMeta(this);
#else
        get => this.dataReader;
#endif
    }
    internal readonly uint RawData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => (uint)(this.primitive >>> DataFieldOffset);
#else
        get => this.dataReader.RawData;
#endif
    }
#endif

    internal readonly bool IsDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.PrimitivePackage == default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta VolatileRead()
#if PRIMITIVE
        => new(Volatile.Read(ref Unsafe.AsRef(in this.primitive)));
#else
        => new(Volatile.Read(ref Unsafe.As<Meta, long>(ref Unsafe.AsRef(in this))));
#endif
        //=> this;
    //=> AtomicHelper<Meta>.BulkLoad(in this);
    internal readonly bool HasEntanglement
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => (this.primitive & EntanglementFlagToPrimitive) == default
        && this.primitive != default;
#else
        get => this.dataReader.HasEntanglementFlag 
        && (this.dataReader.RawData != default || (uint)this.keyIndex == UndefinedSlotEntanglementKeyIndex);
#endif
    }
    internal readonly bool HasUnreachable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => (this.primitive & UnreachableFlagToPrimitive) != default;
#else
        get => (this.keyIndex & UnreachableFlag) != 0;
#endif
    }

    internal readonly bool HasNotKeyIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if PRIMITIVE
        get => (ulong)this.primitive >= (ulong)NotKeyIndexComparer;
#else
        get => (uint)this.keyIndex > (uint)int.MaxValue;
#endif
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Meta(int keyIndex, Data data)
    {
        //#if DEBUG
        //        System.Diagnostics.Debug.Assert((uint)keyIndex < (uint)int.MaxValue);
        //#endif
#if !UNION
#if PRIMITIVE
        this.primitive = ((long)(uint)keyIndex << KeyIndexFieldOffset) | ((long)data.RawData << DataFieldOffset);
#else
        this.keyIndex = keyIndex;
        this.dataReader = data;
#endif
#else
        Unsafe.SkipInit(out this);
        this.KeyIndex = keyIndex;
        this.RawData = data.RawData;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(uint keyIndexRaw, Data data)
    {
#if !UNION
#if PRIMITIVE
        this.primitive = ((long)(uint)keyIndexRaw << KeyIndexFieldOffset) | ((long)data.RawData << DataFieldOffset);
#else
        this.keyIndex = unchecked((int)keyIndexRaw);
        this.dataReader = data;
#endif
#else
        Unsafe.SkipInit(out this);
        this.KeyIndex = keyIndex;
        this.RawData = data.RawData;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(long fromPackage)
    {
#if !UNION && !META_UNION
#if PRIMITIVE
        this.primitive = fromPackage;
#else
        //this = Unsafe.As<long, Meta>(ref fromPackage);
        Unsafe.As<Meta, long>(ref this) = fromPackage;
#endif
#else
        Unsafe.SkipInit(out this);
        this.PrimitivePackage = fromPackage;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta Update(Data data)
#if PRIMITIVE
        => new((this.primitive & KeyIndexMask) | (long)data.RawData);
#else
        => new((uint)this.keyIndex, data);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta UpdateKeyIndex(int keyIndex)
#if PRIMITIVE
        => new(keyIndex, this.DataReader);
#else
        => new(keyIndex, this.dataReader);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta SetUnreachableFlag()
#if PRIMITIVE
        => new(this.primitive | UnreachableFlagToPrimitive);
#else
        => new((uint)(this.keyIndex | UnreachableFlag), this.dataReader);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta SetEntanglement()
#if PRIMITIVE
        => this.primitive == default
            ? UndefinedEntanglement
            : new(this.primitive & (SetEntanglementMaskToPrimitive));
#else
        => new(
            (uint)this.keyIndex,
            this.RawData == default
                ? Meta.Data.UndefinedEntanglement
                : this.dataReader.SetEntanglementFlag()
        );
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta UnsetEntanglement()
#if PRIMITIVE
        => new(this.primitive | UnsetEntanglementToPrimitive);
#else
        => new((uint)this.keyIndex, this.dataReader.UnsetEntanglement());
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta IgnoreEntanglement()
#if PRIMITIVE
        => new(this.primitive | UnsetEntanglementToPrimitive);
#else
        => new((uint)this.keyIndex, this.dataReader.UnsetEntanglement());
#endif
    public readonly override string ToString()
        => $$"""
        "keyIndex": {{this.KeyIndex & ~Meta.UnreachableFlag}}, "Data": {{{this.DataReader.ToString()}}}
        """;

    internal static Meta CreateSentinel(int keyIndex, Meta.Data data)
        => new((uint)keyIndex, data);
    internal readonly bool Equals(int keyIndex, ushort version)
#if PRIMITIVE
        => ((this.primitive & KeyIndexAndVersionMask) | UnsetEntanglementToPrimitive)
            == (((long)(uint)KeyIndex << KeyIndexFieldOffset) | (((long)version << VersionOffset) << DataFieldOffset) | UnsetEntanglementToPrimitive);
#else
        => this.keyIndex == keyIndex && this.dataReader.Version == version && this.dataReader.HasEntanglement is false;
#endif
#region EqualityOverrides

    public readonly override int GetHashCode()
        => this.PrimitivePackage.GetHashCode();
    public readonly override bool Equals(object? obj)
    {
        return obj is Meta meta && this.Equals(meta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Meta other)
    {
        return this.PrimitivePackage == other.PrimitivePackage;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Meta left, Meta right)
        => left.PrimitivePackage == right.PrimitivePackage;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Meta left, Meta right)
        => left.PrimitivePackage != right.PrimitivePackage;
#endregion

    [StructLayout(LayoutKind.Explicit, Size = sizeof(int))]
    internal readonly struct Data
    {
        internal static Data UndefinedEntanglement { get; }
            = new Data(
                version: 0,
                distance: byte.MaxValue,
                hash: byte.MaxValue
            ).SetEntanglementFlag();
        internal static Data Sentinel { get; } = new(
            version: ushort.MaxValue,
            distance: byte.MaxValue,
            hash: byte.MaxValue
        );
        internal static Data Initial { get; } = CreateEntry(0, 1);
        internal const int DistanceOffset = sizeof(byte) * 8;
        internal const int HashMask = 0xFF ^ 0b1;

        internal const int MaxCountableDistance = byte.MaxValue;

        private const uint DistanceMask = (uint)byte.MaxValue << DistanceOffset;
        private const uint HashRemoveMask = ~(uint)byte.MaxValue;
        private const uint DistanceAndHashMask = (uint)ushort.MaxValue;

        internal const ushort MinVersion = 1;
        internal const ushort MaxVersion = ushort.MaxValue - 1;

        [FieldOffset(0)]
        internal readonly uint RawData;
#if UNION

        [FieldOffset(0)]
        internal readonly byte Hash;

        [FieldOffset(sizeof(byte))]
        internal readonly byte DynamicHash;

        [FieldOffset(sizeof(byte) * 2)]
        internal readonly ushort Version;
#else
        internal readonly byte Hash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(this.RawData & HashMask);
        }
        internal readonly byte DynamicHash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(this.RawData >>> DistanceOffset);
        }
        internal readonly ushort Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ushort)(this.RawData >>> VersionOffset);
        }

#endif
        internal readonly bool HasEntanglementFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (this.RawData & EntanglementFlag) == 0;
        }
        internal readonly bool IsDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.RawData == default; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(uint hash, ushort version)
        {
            //            uint mash = (hash >>> 8) ^ hash;
            //            mash ^= mash >>> 16;
            //#if UNION
            //            Unsafe.SkipInit(out this);
            //            this.Version = version;
            //            this.Distance = 0;
            //            this.Hash = (byte)mash;
            //#else
            //            mash &= 0xFF;
            //            this.RawData = ((uint)version << VersionOffset) | mash;
            //#endif

#if UNION
            var mash = (ushort)(hash | (hash >>> 16));
            Unsafe.SkipInit(out this);
            this.Version = version;
            this.Distance = mash >>> 8;
            this.Hash = (byte)mash;
#else
            this.RawData = ((uint)version << VersionOffset) 
            | ((hash | (hash >>> 16)) & DistanceAndHashMask)
            | EntanglementFlag;
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(uint data)
        {
#if UNION
            Unsafe.SkipInit(out this);
#endif
            this.RawData = data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(ushort version, byte distance, byte hash)
#if UNION
        {
            Unsafe.SkipInit(out this);
            this.Version = version;
            this.Distance = distance;
            this.Hash = hash;
        }
#else
        : this(((uint)version << VersionOffset) | ((uint)distance << DistanceOffset) | (uint)hash) { }
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data AddJump(JumpType jt)
            => new(unchecked(this.RawData + jt.ForMetaDataDistanceAddOperationValue));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly JumpType GetJumpType()
            => new(this);
        //(JumpType)(  ( unchecked((short)this.RawData & -(short)this.RawData) & 0b0110 ) + 1); //{1, 3, 5}
        //(JumpType)((this.RawData & 0b0110) + 1);// {1, 3, 5, 7}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data SetVersion(ushort version)
#if UNION
            => new(version, this.Distance, this.Hash);
#else
            => new(((uint)version << VersionOffset) | (this.RawData & DistanceAndHashMask));
#endif
            //new((this.RawData & HashRemoveMask) | hash);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data SetEntanglementFlag()
            //=> new(this.Version, this.Distance, this.Hash);
            => new(this.RawData & SetEntanglementMask);
        internal readonly Data UnsetEntanglement()
            => new(this.RawData | EntanglementFlag);
        internal readonly Data Hypothesis()
            => new(this.RawData | EntanglementFlag);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Meta.Data SubJump(JumpType jt)
            => new(unchecked(this.RawData - jt.ForMetaDataDistanceAddOperationValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data RestoreOverflowed(ushort originalVersion)
            => this.SetVersion(originalVersion);

        public readonly override string ToString()
            => $"""
            "hash": {this.RawData & 0xFF,3}, "v": {this.Version,3}, "d": {this.DynamicHash,3}, "j": {(int)this.GetJumpType(),2}
            """;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Data CreateEntry(int hash, ushort version)
            => new((uint)hash, version);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Data FromMeta(Meta meta)
            => new((uint)(meta.primitive >>> DataFieldOffset));
    }
}


// backup
/*
//#define UNION
//#define META_UNION
using HashIndex.InternalModules.Atomic;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace HashIndex.InternalModules;
#if !UNION && !META_UNION
internal static class MetaExtensions
{
    extension(in Meta meta)
    {

        internal long PrimitivePackage
        {
            get => Unsafe.As<Meta, long>(ref Unsafe.AsRef(in meta));
        }
    }
}
#else
[StructLayout(LayoutKind.Explicit, Size = sizeof(long))]
#endif
internal readonly struct Meta
{
    internal const int KeyIndexFieldOffset = 0;
    internal const int DataFieldOffset = sizeof(int);

    internal const int UnreachableFlag = int.MinValue;
    internal const int TombstoneKeyIndex = int.MaxValue;
    internal const int EntanglementKeyIndex = UnreachableFlag | TombstoneKeyIndex;


    //use for atomic comparand and read/write value of Interlocked Methods
#if UNION || META_UNION
    [FieldOffset(0)]
    internal readonly long PrimitivePackage;

    [FieldOffset(KeyIndexFieldOffset)]
    internal readonly int KeyIndex;

    //union
    [FieldOffset(DataFieldOffset)]
    internal readonly uint RawData;
    [FieldOffset(sizeof(int))]
    internal readonly Data DataReader;
#else
    internal readonly int KeyIndex { get; }
    internal readonly Data DataReader { get; }
    internal readonly uint RawData
        => this.DataReader.RawData;
    //internal readonly Data DataReader
    //{
    //    get => Data.FromUInt(this.RawData);
    //}

#endif

    internal readonly bool IsDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.PrimitivePackage == default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta VolatileRead()
        => new(Volatile.Read(ref Unsafe.As<Meta, long>(ref Unsafe.AsRef(in this))));
        //=> this;
    //=> AtomicHelper<Meta>.BulkLoad(in this);
    internal readonly bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !this.DataReader.IsDefault;
    }
    internal readonly bool HasTombstone 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.KeyIndex == TombstoneKeyIndex;
    }

    internal readonly bool HasEntanglement
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.KeyIndex == EntanglementKeyIndex;
    }
    internal readonly bool HasUnreachable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (this.KeyIndex & UnreachableFlag) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Meta(int keyIndex, Data data)
    {
#if !UNION
        this.KeyIndex = keyIndex;
        this.DataReader = data;
#else
        Unsafe.SkipInit(out this);
        this.KeyIndex = keyIndex;
        this.RawData = data.RawData;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(int keyIndex, uint raw)
    {
        Unsafe.SkipInit(out this);
        this.KeyIndex = keyIndex;
#if !UNION && !META_UNION
        this.DataReader = Unsafe.As<uint, Data>(ref raw);
#else
        this.RawData = raw;
#endif

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Meta(long fromPackage)
    {
        Unsafe.SkipInit(out this);
#if !UNION && !META_UNION
        //this = Unsafe.As<long, Meta>(ref fromPackage);
        Unsafe.As<Meta, long>(ref this) = fromPackage;
#else
        this.PrimitivePackage = fromPackage;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta Update(Data data)
        => new(this.KeyIndex, data);
  
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta UpdateKeyIndex(int keyIndex)
        => new(keyIndex, this.DataReader);
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //internal readonly Meta SetAttribute(MetaAttributeTag attribute)
    //    => new (this.KeyIndex | (int)attribute, this.RawData);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta SetTombstone()
        => new(TombstoneKeyIndex, this.DataReader);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta SetUnreachableFlag()
        => new(this.KeyIndex | UnreachableFlag, this.DataReader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Meta SetEntanglement()
        => new(EntanglementKeyIndex, this.DataReader);

    public readonly override string ToString()
        => $$"""{ "keyIndex": {{this.KeyIndex & ~Meta.UnreachableFlag}}, "Data": {{this.DataReader.ToString()}} }""";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Meta CreateWithEntanglement(Data data)
        => new(EntanglementKeyIndex, data);

    internal readonly bool Equals(int keyIndex, ushort version)
        => this.KeyIndex == keyIndex && this.DataReader.Version == version;


#region EqualityOverrides

    public readonly override int GetHashCode()
        => this.PrimitivePackage.GetHashCode();
    public readonly override bool Equals(object? obj)
    {
        return obj is Meta meta && this.Equals(meta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Meta other)
    {
        return this.PrimitivePackage == other.PrimitivePackage;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Meta left, Meta right)
        => left.PrimitivePackage == right.PrimitivePackage;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Meta left, Meta right)
        => left.PrimitivePackage != right.PrimitivePackage;
#endregion

    [StructLayout(LayoutKind.Explicit, Size = sizeof(int))]
    internal readonly struct Data
    {
        internal static Data Sentinel { get; } = new(
            version: ushort.MaxValue,
            distance: byte.MaxValue,
            hash: byte.MaxValue
        );
        internal static Data Initial { get; } = CreateEntry(0, 1);
        internal const int DistanceOffset = sizeof(byte) * 8;
        internal const int VersionOffset = sizeof(byte) * 8 + sizeof(byte) * 8;
        internal const int HashMask = 0xFF;

        private const uint DistanceRemoveMask = ~((uint)byte.MaxValue << DistanceOffset);
        private const uint HashRemoveMask = ~(uint)byte.MaxValue;

        internal const int MaxCountableDistance = byte.MaxValue;

        internal const ushort MinVersion = 1;
        internal const ushort MaxVersion = ushort.MaxValue - 1;

        [FieldOffset(0)]
        internal readonly uint RawData;
#if UNION

        [FieldOffset(0)]
        internal readonly byte Hash;

        [FieldOffset(sizeof(byte))]
        internal readonly byte Distance;

        [FieldOffset(sizeof(byte) * 2)]
        internal readonly ushort Version;
#else
        internal readonly byte Hash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(this.RawData & HashMask);
        }
        internal readonly byte Distance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte)(this.RawData >> DistanceOffset & 0xFF);
        }
        internal readonly ushort Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ushort)(this.RawData >> VersionOffset);
        }

#endif
        internal readonly bool IsDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.RawData == 0; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(uint hash, ushort version)
        {
            uint mash = (hash >>> 8) ^ hash;
            mash ^= mash >>> 16;
#if UNION
            Unsafe.SkipInit(out this);
            this.Version = version;
            this.Distance = 0;
            this.Hash = (byte)mash;
#else
            mash &= 0xFF;
            this.RawData = ((uint)version << VersionOffset) | mash;
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(uint data)
        {
#if UNION
            Unsafe.SkipInit(out this);
#endif
            this.RawData = data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Data(ushort version, byte distance, byte hash)
#if UNION
        {
            Unsafe.SkipInit(out this);
            this.Version = version;
            this.Distance = distance;
            this.Hash = hash;
        }
#else
        : this(((uint)version << VersionOffset) | ((uint)distance << DistanceOffset) | (uint)hash) { }
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data AddJump(JumpType jt)
            => new(unchecked(this.RawData + jt.ForMetaDataDistanceAddOperationValue));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly JumpType GetJumpType()
            => new(this);
        //(JumpType)(  ( unchecked((short)this.RawData & -(short)this.RawData) & 0b0110 ) + 1); //{1, 3, 5}
        //(JumpType)((this.RawData & 0b0110) + 1);// {1, 3, 5, 7}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data SetHash(byte hash)
            => new(this.Version, this.Distance, hash);
            //new((this.RawData & HashRemoveMask) | hash);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Data CreateEntry(int hash, ushort version)
            => new((uint)hash, version);

#if !UNION
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Data FromUInt(uint data)
            => new(data);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly uint ToUInt()
            => this.RawData;
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Meta.Data SubJump(JumpType jt)
            => new(unchecked(this.RawData - jt.ForMetaDataDistanceAddOperationValue));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly Data RestoreOverflowed(ushort originalVersion)
            => new(
                version: originalVersion,
                distance: this.Distance,
                hash: this.Hash
            );
        public readonly override string ToString()
            => $$"""{"hash": {{this.RawData & 0xFF,3}}, "v": {{this.Version,3}}, "d": {{this.Distance,3}}, "j": {{(int)this.GetJumpType(),2}} }""";

    }
}

*/