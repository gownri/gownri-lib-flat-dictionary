using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IndexFriendlyCollections;
namespace FlatDictionary.InternalModules;

internal readonly partial struct MetaTable
{
   
    internal const int HeaderOffset = 1;
    private const int headerPosition = 0;
    internal static MetaTable Empty { get; } = new MetaTable([default]).Init();

    internal readonly Meta[] Source;
    internal readonly SqSize Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var ret = this.Source.Length - HeaderOffset;
            return Unsafe.As<int, SqSize>(ref ret);
        }
    }

    internal readonly ref TableHeader Header
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var src = this.Source;
            var idx = headerPosition;
            // var idx = src.Length - 1;
            if ((uint)idx < (uint)src.Length)
                return ref Unsafe.As<Meta, TableHeader>(ref src[idx]);
            else
                return ref Throw();
            [MethodImpl(MethodImplOptions.NoInlining)]
            static ref TableHeader Throw()
                => throw new NullReferenceException();
        }

    }
    private MetaTable(Meta[] array)
        => this.Source = array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]    
    private readonly MetaTable Init()
    {
        this.Header = new(Meta.Data.MinVersion);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MetaTable Create(SqSize size, int multiply = 1)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert((size & -size) == size);
#endif
        return new MetaTable(new Meta[(size * multiply) + HeaderOffset]).Init();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MetaTable Rent(SqSize size, int multiply = 1)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert((size & -size) == size);
#endif
        return Pool.Rent(size * multiply);
    }

    internal readonly void Return()
        => Pool.Return(this);
    
    //internal readonly Span<Meta> AsSpan(out ushort version)
    //{
    //    var span = new Span<Meta>(this.Source);
    //    version = Unsafe.As<Meta, TableHeader>(
    //        ref MemoryMarshal.GetReference(span)
    //    ).SyncState.Version;
    //    return span[HeaderOffset..];
    //}


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<Meta> AsReadOnlySpan()
    {
        var source = this.Source;
        var idx = HeaderOffset;
        if ((uint)idx <= (uint)source.Length)
            return source.AsSpan(idx);
        else
            return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Span<Meta> AsSpan()
    {
        var source = this.Source;
        var idx = HeaderOffset;
        if ((uint)idx <= (uint)source.Length)
            return source.AsSpan(idx);
        else
            return default;
    }

    private static class Pool
    {
        private const int poolLessTableSize = 16;
        private const int poolLessOffset = 4;
        private static ReadOnlySpan<int> RentAltIndexes =>
        [
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
            20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 
            30, 31
        ];
        private static ReadOnlySpan<int> ReturnAltIndexes =>
        [
            31, 30, 
            29, 28, 27, 26, 25, 24, 23, 22, 21, 20,
            19, 18, 17, 16, 15, 14, 13, 12, 11, 10,
            9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
        ];

        private static readonly IndexAccessPool<Meta[]> pool = new(31, 15, 8, stackalloc[] { 1, 1, 1, 1, 1, 4});


        internal static MetaTable Rent(int minimumLog2Length)
        {
            var index = Helper.GetIndexOfPowerOfTwo(minimumLog2Length);
            Meta[] table;
            MetaTable metaTable;
            if (!pool.TryRent(index, out table!, RentAltIndexes[index..]))
                metaTable = new MetaTable(new Meta[minimumLog2Length + 1]).Init();
            else
                metaTable = new MetaTable(table);
            ref var header = ref metaTable.Header;
            var lockTaken = header.TryAcquire(TableHeader.LockType.Clearing);
            if (!lockTaken)
                lockTaken = header.AcquireWithSpinWait(TableHeader.LockType.Clearing);
            if (lockTaken.CurrentState.Version < Meta.Data.MaxVersion)
            {
                if (lockTaken.CurrentState.IsClean)
                    header.Release(lockTaken, TableHeader.Context.None);
                else
                    header.ReleaseAtomicUpdateVersion(lockTaken, (ushort)(lockTaken.CurrentState.Version + 1));
            }
            else
                metaTable = WaitAndClear(
                    ref header,
                    metaTable,
                    header.Update(lockTaken, TableHeader.LockType.Clearing)
                );

            return metaTable;

            [MethodImpl(MethodImplOptions.NoInlining)]
            static MetaTable WaitAndClear(ref TableHeader header, MetaTable table, TableHeader.LockTaken lockTaken)
            {
                try
                {
                    var spinWait = new System.Threading.SpinWait();
                    while (header.VolatileReadWriterCount() != 0)
                        spinWait.SpinOnce();
                    //Array.Clear(array, HeaderOffset, array.Length - HeaderOffset);
                    MemoryMarshal.AsBytes(table.AsSpan()).Clear();
                    header.ReleaseAtomicUpdateVersion(lockTaken, Meta.Data.MinVersion);
                }
                catch
                {
                    header.Release(lockTaken, TableHeader.Context.None);
                    table = MetaTable.Create(table.Size);
                }

                return table;
            }
        }

        internal static void Return(MetaTable metaTable)
        {
            var table = metaTable.Source;
            var index = Helper.GetIndexOfPowerOfTwo(metaTable.Size);
            pool.Return(index, table, ReturnAltIndexes[index..]);
        }
    }

    internal readonly struct SqSize
    {
        internal readonly int Value;

        internal SqSize(int minimumSize)
        {
            this.Value = Helper.GetTableSize(minimumSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SqSize(int value, bool dummy)
            => this.Value = value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(SqSize size)
            => size.Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SqSize operator <<(SqSize size, int shift)
            => new SqSize(size.Value << shift, true);

    }
}
