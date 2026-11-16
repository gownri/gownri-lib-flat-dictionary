//#define UNION

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;
#if !UNION
internal static class AtomicLengthAndVersionExtensions
{
    extension(in AtomicLengthAndVersion lav)
    {
        internal ref long AsPrimitive
            => ref Unsafe.As<AtomicLengthAndVersion, long>(ref Unsafe.AsRef(lav));
    }

}
#else

[StructLayout(LayoutKind.Explicit)]
#endif
internal struct AtomicLengthAndVersion : IEquatable<AtomicLengthAndVersion>
{
#if UNION
    [FieldOffset(0)]
    internal long AsPrimitive;

    [FieldOffset(0)]
    internal int RegisteredKeysLength;

    [FieldOffset(sizeof(int))]
    internal TableVersion Version;
#else
    internal int RegisteredKeysLength;
    internal TableVersion Version;
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly AtomicLengthAndVersion VolatileRead()
        => new(Volatile.Read(ref Unsafe.AsRef(in this.AsPrimitive)));
        //=> this;
        //=> AtomicHelper<AtomicLengthAndVersion>.BulkLoad(in this);


    public readonly override bool Equals(object? obj)
    {
        return obj is AtomicLengthAndVersion version && this.Equals(version);
    }

    public readonly bool Equals(AtomicLengthAndVersion other)
    {
        return this.AsPrimitive == other.AsPrimitive;
    }

    public readonly override int GetHashCode()
    {
        return HashCode.Combine(this.AsPrimitive);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal AtomicLengthAndVersion(int count, TableVersion version)
    {
        Unsafe.SkipInit(out this);
        this.RegisteredKeysLength = count;
        this.Version = version;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AtomicLengthAndVersion(long value)
    {
        Unsafe.SkipInit(out this);
        this.AsPrimitive = value;
    }

    public static bool operator ==(AtomicLengthAndVersion left, AtomicLengthAndVersion right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AtomicLengthAndVersion left, AtomicLengthAndVersion right)
    {
        return !(left == right);
    }

    public override readonly string ToString()
        => $""" "Len": {this.RegisteredKeysLength}, "version": {this.Version.ToString()} """;
}
