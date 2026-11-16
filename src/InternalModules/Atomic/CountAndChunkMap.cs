//#define FORCE_SCAN_REMOVED_MAP
//#define UNION
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;
#if !UNION
internal static class CountAndChunkMapExtensions
{
    extension(in CountAndChunkMap cac)
    {
        internal ref long AsPrimitive
            => ref Unsafe.As<CountAndChunkMap, long>(ref Unsafe.AsRef(in cac));
    }
}

#else
[StructLayout(LayoutKind.Explicit)]
#endif

internal struct CountAndChunkMap
{
#if UNION
    [FieldOffset(0)]
    internal long AsPrimitive;
    [FieldOffset(0)]
    internal int Count;
    [FieldOffset(sizeof(int))]
    internal int RemoveChunkMap;
#else
    internal int Count;
    internal int RemoveChunkMap;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly CountAndChunkMap VolatileRead()
        => new(Volatile.Read(ref this.AsPrimitive));
        //=> this;
        //=> AtomicHelper<CountAndChunkMap>.BulkLoad(in this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CountAndChunkMap(long value)
    {
        Unsafe.SkipInit(out this);
        this.AsPrimitive = value;
    }

    public readonly override string ToString()
        => $"count: {this.Count}, map: {this.RemoveChunkMap.ToString("B")}";
}


