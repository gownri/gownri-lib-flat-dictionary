using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;

namespace FlatDictionary.InternalModules.Atomic;

[StructLayout(LayoutKind.Explicit)]
internal struct AtomicHeader
{
    private const int countOffset = 0;
    private const int versionOffset = sizeof(int);

    private const int lockFlag = int.MinValue;

    [FieldOffset(0)]
    private long AsPrimitive;

    [FieldOffset(0)]
    private Local AsLocal;

    [FieldOffset(countOffset)]
    private int count;

    [FieldOffset(versionOffset)]
    private TableVersion version;


    internal AtomicHeader(ushort version)
    {
        Unsafe.SkipInit(out this);
        this.count = 0;
        this.version = TableVersion.Create(version);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AtomicHeader(long value)
    {
        Unsafe.SkipInit(out this);
        this.AsPrimitive = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AtomicHeader(int count, TableVersion version)
    {
        Unsafe.SkipInit(out this);
        this.count = count;
        this.version = version;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly AtomicHeader.Local VolatileRead()
        => new AtomicHeader(Volatile.Read(ref Unsafe.AsRef(in this.AsPrimitive))).AsLocal;



    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct Local
    {
        [FieldOffset(0)]
        private readonly long AsPrimitive;

        [FieldOffset(countOffset)]
        private readonly int count;

        internal readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.count & ~lockFlag;
        }

        [FieldOffset(versionOffset)]
        internal readonly TableVersion version;
    }
}
