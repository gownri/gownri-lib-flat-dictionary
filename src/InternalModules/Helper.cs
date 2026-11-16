using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlatDictionary.InternalModules;

internal static class Helper
{
    internal const int KeysFloor = 4;
    internal const int TableFloor = 15;
    internal const int SupportedArraySize = 0x7FFF_FFC0;
    internal const uint CollisionTolerance = 100;
    internal const int InvalidIndex = -1;
    internal const int ProveOverValue = int.MaxValue;
    internal const int NotFoundValue = -1;

    //citations https://graphics.stanford.edu/~seander/bithacks.html

    internal static int RoundUpToPowerOfTwo(int size)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert(size > 0);
#endif
#if NET6_0_OR_GREATER
        return unchecked((int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)size));
#else
        size--;
        size |= size >>> 1;
        size |= size >>> 2;
        size |= size >>> 4;
        size |= size >>> 8;
        size |= size >>> 16;
        return ++size;
#endif
    }


    private static ReadOnlySpan<byte> DeBruijnIndexOfBitPosition32 =>
    [
        0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
        31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9,
    ];
    private static ReadOnlySpan<byte> OutUnder32ChunkDivShiftersDeBruijn =>
    [
        0, 0, 23, 0, 24, 9, 19, 0, 25, 17, 15, 10, 20, 12, 0, 3,
        26, 22, 8, 18, 16, 14, 11, 2, 21, 7, 13, 1, 6, 0, 5, 4
    ];
    private const uint DeBruijnConst = 0x077CB531;

    
    internal static int GetIndexOfPowerOfTwo(int powerOfTwoValue)
        =>
#if DEBUG
    (powerOfTwoValue & (-powerOfTwoValue)) != powerOfTwoValue
        ? throw new ArgumentException("argument value must log2")
        :
#endif
        Unsafe.AddByteOffset(
            ref MemoryMarshal.GetReference(DeBruijnIndexOfBitPosition32),
            (IntPtr)(((uint)powerOfTwoValue * DeBruijnConst) >>> 27)
        );
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetChunkDivShifterOfPowerOfTwo(int powerOfTwo)
    =>
#if DEBUG
    (powerOfTwo & (-powerOfTwo)) != powerOfTwo
        ? throw new ArgumentException("argument value must log2")
        :
#endif
#if NET6_0_OR_GREATER
        Math.Max(0, unchecked((int)System.Numerics.BitOperations.Log2((uint)powerOfTwo)) - 5);
#else
        Unsafe.AddByteOffset(
            ref MemoryMarshal.GetReference(OutUnder32ChunkDivShiftersDeBruijn),
            (IntPtr)(uint)(((uint)powerOfTwo * DeBruijnConst) >>> 27)
        );
#endif

    internal static int GetTableSize(int minimumSize)
        => RoundUpToPowerOfTwo(minimumSize | TableFloor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IndexToBitPosition(int primaryLength, int alternativeLength, int value)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert((alternativeLength & -alternativeLength) == alternativeLength);
#endif
        var len = (primaryLength & -primaryLength) == primaryLength
            ? primaryLength
            : alternativeLength;
        return 1 << (value >>> GetChunkDivShifterOfPowerOfTwo(len));

    }

    internal static int GetChunkSize(int maxLength)
        => maxLength <= 32 
            ? 1
            : maxLength >>> 5;
    internal static int GetChunkSize(int primaryLength, int alternativeLength)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert((alternativeLength & -alternativeLength) == alternativeLength);
#endif
        var len = (primaryLength & -primaryLength) == primaryLength
            ? primaryLength
            : alternativeLength;
        return len >>> 5;
    }
}
