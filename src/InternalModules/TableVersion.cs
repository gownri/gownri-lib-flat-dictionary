//#define UNION
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatDictionary.InternalModules;
#if UNION
[StructLayout(LayoutKind.Explicit)]
#endif
internal readonly struct TableVersion
{
    private const int hashTableMask = ushort.MaxValue;
    private const int overflowThreshold = ushort.MaxValue - 1;
    private const int generationOffset = sizeof(ushort) * 8;
    private const int generationUnit = 1 << generationOffset;
    private const uint maxGenerationCount = (uint)ushort.MaxValue << generationOffset;
#if UNION
    [FieldOffset(0)]
#endif
    internal readonly uint RawValue;
#if UNION

    [FieldOffset(0)]
    internal readonly ushort Table;
    [FieldOffset(sizeof(ushort))]
    internal readonly ushort Generation;
#else
    internal readonly ushort Table
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ushort)this.RawValue;
    }
    internal readonly ushort Generation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ushort)(this.RawValue >>> generationOffset);
    }
#endif
    internal readonly bool IsBoundaryGeneration 
        => this.RawValue >= maxGenerationCount;

    internal readonly bool IsValid => this.RawValue != 0;
    internal static TableVersion Create()
    {
        return new(0, 1);
    }
    internal static TableVersion Create(ushort tableVersion)
        => new(0, tableVersion);
    private TableVersion(ushort generation, ushort table)
#if !UNION
    : this((uint)generation << generationOffset | table) { }
#else
    {
        Unsafe.SkipInit(out this);
        this.Generation = generation;
        this.Table = table;
    }
#endif

    private TableVersion(uint value)
    {
        Unsafe.SkipInit(out this);
        this.RawValue = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly TableVersion VolatileRead()
        => new(System.Threading.Volatile.Read(ref Unsafe.AsRef(in this.RawValue)));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly uint VolatileReadRawValue()
        => System.Threading.Volatile.Read(ref Unsafe.AsRef(in this.RawValue));
    internal readonly TableVersion IncrementTableVersion(out bool isOverflow)
    {
        const int increment = 1;
        const int overflowedIncrement = 3;
        var value = this.RawValue;
        var isOverflowL = (value & hashTableMask) == overflowThreshold;
        isOverflow = isOverflowL;
        return new (
            (uint)(isOverflow 
                ? value + overflowedIncrement
                : value + increment)
        );
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly (TableVersion newVersion, bool isOverflowed) IncrementTableVersion()
    {
        var incremented = this.IncrementTableVersion(out var isOverflow);
        return (incremented, isOverflow);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly TableVersion ResetGeneration()
        => new(0, this.Table);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly TableVersion SetTableVersion(ushort newTableVersion)
        => new((ushort)(this.Generation+1), newTableVersion);


    public readonly override string ToString()
        => $$""" "Gen":{{this.Generation,5}}, "Tab":{{this.Table,5}}""";
}
