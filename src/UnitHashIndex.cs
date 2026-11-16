using HashIndex.InternalModules;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace HashIndexInternalModules
{
    public ref struct UnitHashIndex
    {
        internal readonly Span<Meta> bucket;
        internal readonly Span<Meta> BucketSource => this.bucket;
        internal readonly HashTableVersion Version => this.version;
        private int count;
        private HashTableVersion version;

        public readonly int BucketSize => this.bucket.Length;
        public readonly int Count => this.count;


        public static int SizeOfMeta => Unsafe.SizeOf<Meta>();
        public static int ComputeBucketByteSize(int bucketCapacity)
            => Helper.RoundUpToTableSize(bucketCapacity)*Unsafe.SizeOf<Meta>();
        public UnitHashIndex(Span<byte> bufferBucket, bool isCleanBuffer)
        {
            var bucket = MemoryMarshal.Cast<byte, Meta>(bufferBucket);
            if(!isCleanBuffer)
                bucket.Clear();
            var lsb = bucket.Length & (-bucket.Length);
            this.bucket = lsb != bucket.Length 
                ? Slice(bucket, lsb) 
                : bucket;
            this.version = HashTableVersion.Create();
            this.count = 0;

            static Span<Meta> Slice(Span<Meta> span, int lsb)
            {
                uint max = 0;
                for (max = (uint)lsb; max < span.Length; max <<= 1) ;
                return span.Slice(0, (int)(max >> 1));
            }
        }

        //public UnitHashIndex(BufferPack bufferPack) : this(bufferPack.Rent(), bufferPack.version)
        //{ }
        internal UnitHashIndex(Span<Meta> bucket, HashTableVersion version)
        {
            this.bucket = bucket;
            this.version = version;
            this.count = 0;
            this.Clear();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public Entry GetEntries<TKey>(TKey key)
        //    where TKey : notnull
        //{
        //    var unit = this;
        //    var hashIndex = key.GetHashCode();
        //    var entry = Meta.Data.CreateEntry(hashIndex, unit.Version.Bucket);
        //    var entryRef = unit.bucket.GetBucket(hashIndex, out hashIndex);
        //    if (entryRef.RawData <= entry.RawData)
        //        return new(default, hashIndex, entry);

        //    _ = unit.bucket.EntryOrLess(
        //        (
        //            hashIndex,
        //            entry,
        //            entry.GetJumpType()
        //        ),
        //        out hashIndex,
        //        out entry
        //    );

        //    return new(unit.bucket, hashIndex, entry);
        //}

        //[Obsolete]
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]

        //public void Insert(InsertHint hint, Index insertIndexItem)
        //{
        //    throw new NotImplementedException();
        //    //            if (!hint.IsValid)
        //    //#if DEBUG
        //    //                throw new ArgumentNullException(nameof(hint));
        //    //#else
        //    //                return;
        //    //#endif
        //    //            this.count++;
        //    //            ref var inserted = ref this.bucket.Insert(
        //    //                this.version,
        //    //                hint.Index,
        //    //                hint.metaData
        //    //            );
        //    //            inserted = new (insertIndexItem.Value, hint.metaData);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this.count = 0;
            this.version = this.version.IncrementTable(out var isOverflow);
            if (isOverflow)
                this.bucket.Clear();
        }
        [Obsolete]
        public void Refresh<TKey>(ReadOnlySpan<TKey> keys)
            where TKey : notnull, IEquatable<TKey>
        {
            throw new NotImplementedException();
            //this.Clear();
            //var version = this.version;
            //var bucketVersion = version.Bucket;
            //var bucket = this.bucket;
            //var count = this.count;
            //var hash = 0;
            //ref var inserted = ref Unsafe.NullRef<Meta>();
            //for (var i = 0; i < keys.Length; ++i)
            //{ 
            //    hash = keys[i].GetHashCode();
            //    inserted = ref this.bucket.Insert(
            //        version, 
            //        bucket.GetBucketIndex(hash), 
            //        Meta.Data.CreateEntry(hash, bucketVersion)
            //    );
            //    inserted = inserted.UpdateKeyIndex(i);
            //}
        }
    }

}
