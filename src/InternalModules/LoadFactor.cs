using System;
using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal enum LoadFactor : byte
{
    Half,// 0.5
    MemorySave,// 0.75
    Fill,// 1(n - 1)
}

internal static class LoadFactorExtensions
{
    private const int Safety = 1;
    internal static MetaTable.SqSize ComputeTableSizeFromCapacity(this LoadFactor loadFactor, int capacity)
        => loadFactor switch
        {
            LoadFactor.Half => new(capacity << 1),
            LoadFactor.MemorySave => new( (capacity - 1) + (Helper.RoundUpToPowerOfTwo(capacity) >>> 2) ),
            LoadFactor.Fill => new(capacity + Safety),
            _ => new(Throw()),

        };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ComputeKeysSize(this LoadFactor loadFactor, MetaTable.SqSize tableCapacity)
        => loadFactor switch
        {
            LoadFactor.Half => tableCapacity.Value >>> 1,// 0.5
            LoadFactor.MemorySave => (tableCapacity.Value >>> 1) | (tableCapacity.Value >>> 2),// 0.75
            LoadFactor.Fill => tableCapacity.Value - Safety,// 1(n - 1)
            _ => Throw(),
        };
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Throw()
        => throw new ArgumentException(nameof(LoadFactor));

}

//internal MetaTable.SqSize(int minimumSize)
//{
//    this.Value = Helper.GetTableSize(minimumSize);
//}