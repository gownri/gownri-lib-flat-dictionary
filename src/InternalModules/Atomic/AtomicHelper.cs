using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;

internal static class AtomicHelper<T>
{
    internal static bool IsWordSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Unsafe.SizeOf<T>() == IntPtr.Size
                || Unsafe.SizeOf<T>() == sizeof(int)
                || Unsafe.SizeOf<T>() == sizeof(byte)
                || Unsafe.SizeOf<T>() == sizeof(short));
    }

    internal static bool IsAtomicStrict
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !typeof(T).IsValueType 
            || ((typeof(T).IsPrimitive || typeof(T).IsEnum)
                && Unsafe.SizeOf<T>() <= IntPtr.Size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T BulkLoad(scoped ref readonly T location)
    {

        if ((RuntimeHelpers.IsReferenceOrContainsReferences<T>() 
        || typeof(T).IsPrimitive
        || typeof(T).IsEnum)
            && Unsafe.SizeOf<T>() <= IntPtr.Size
        )
            return location;

        if (Unsafe.SizeOf<T>() == sizeof(long))
            return Unsafe.As <long, T>(
                ref Unsafe.AsRef(
                    Volatile.Read(
                        ref Unsafe.As <T, long>(
                            ref Unsafe.AsRef(in location)
                        )
                    )
                )
            );

        if (Unsafe.SizeOf<T>() == sizeof(int))
            return Unsafe.As <int, T>(
                ref Unsafe.AsRef(
                    Volatile.Read(
                        ref Unsafe.As <T, int>(
                            ref Unsafe.AsRef(in location)
                        )
                    )
                )
            );

        if (Unsafe.SizeOf<T>() == sizeof(short))
            return Unsafe.As <short, T>(
                ref Unsafe.AsRef(
                    Volatile.Read(
                        ref Unsafe.As <T, short>(
                            ref Unsafe.AsRef(in location)
                        )
                    )
                )
            );

        if (Unsafe.SizeOf<T>() == sizeof(byte))
            return Unsafe.As <byte, T>(
                ref Unsafe.AsRef(
                    Volatile.Read(
                        ref Unsafe.As <T, byte>(
                            ref Unsafe.AsRef(in location)
                        )
                    )
                )
            );

        return ReadOnStack(ref Unsafe.AsRef(in location)/* mitigate defensive copy*/);
        [MethodImpl(MethodImplOptions.NoInlining)]
        static T ReadOnStack(scoped ref T location)
            => location;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BulkWrite(scoped ref T location, in T value)
    {
        if ((RuntimeHelpers.IsReferenceOrContainsReferences<T>()
        || typeof(T).IsPrimitive
        || typeof(T).IsEnum)
            && Unsafe.SizeOf<T>() <= IntPtr.Size)
        {
            location = value;
            return;
        }

        if (Unsafe.SizeOf<T>() == sizeof(long))
        {
            Volatile.Write(
                ref Unsafe.As<T, long>(ref location),
                Unsafe.As<T, long>(
                    ref Unsafe.AsRef(in value)
                )
            );
            return;
        }

        if (Unsafe.SizeOf<T>() == sizeof(int))
        {
            Volatile.Write(
                ref Unsafe.As<T, int>(ref location),
                Unsafe.As<T, int>(
                    ref Unsafe.AsRef(in value)
                )
            );
            return;
        }

        if (Unsafe.SizeOf<T>() == sizeof(short))
        {
            Volatile.Write(
                ref Unsafe.As<T, short>(ref location),
                Unsafe.As<T, short>(
                    ref Unsafe.AsRef(in value)
                )
            );
            return;
        }

        if (Unsafe.SizeOf<T>() == sizeof(byte))
        {
            Volatile.Write(
                ref Unsafe.As<T, byte>(ref location),
                Unsafe.As<T, byte>(
                    ref Unsafe.AsRef(in value)
                )
            );
            return;
        }

        location = value;
        return;
    }

}
