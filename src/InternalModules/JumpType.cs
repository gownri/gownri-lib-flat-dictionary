using System.Runtime.CompilerServices;

namespace FlatDictionary.InternalModules;

internal readonly struct JumpType
{
    internal const int One = 1;
    internal const int Short = 3;
    internal const int Medium = 5;
    internal const int Long = 7;
    /// <summary>
    /// jumpLength = 1
    /// </summary>

    internal static JumpType None
        => default;

    internal readonly uint ForMetaDataDistanceAddOperationValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JumpType(Meta.Data data)
    => this.ForMetaDataDistanceAddOperationValue
        = ((data.RawData & (~data.RawData + 1) & 0b0110) | 1) << Meta.Data.DistanceOffset;
    public readonly override string ToString()
        => $"Jump:{(int)this,3}";

    /// <returns>{1, 3, 5}</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(JumpType type)
        => (int)(type.ForMetaDataDistanceAddOperationValue >>> Meta.Data.DistanceOffset) | 1;
}
