using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FlatDictionary.InternalModules.Atomic;

internal static class InterlockedMeta
{
    [StructLayout(LayoutKind.Explicit)]
    private readonly struct Cast
    {
        [FieldOffset(0)]
        private readonly long primitive;
        [FieldOffset(0)]
        internal readonly Meta AsMeta;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Cast(long primitive)
        {
            Unsafe.SkipInit(out this);
            this.primitive = primitive;
        }
    }
    internal static Meta CompareRestore(scoped ref Meta location, Meta original)
#if DEBUG
    {
        System.Diagnostics.Debug.Assert(original.HasEntanglement is false);
        System.Diagnostics.Debug.WriteLineIf(location != original.SetEntanglement(), $"restore miss: {location} != {original}");
        var cmp = Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                original.PrimitivePackage,
                original.SetEntanglement().PrimitivePackage
        );
        System.Diagnostics.Debug.Assert(cmp == original.SetEntanglement().PrimitivePackage);
        return new Cast(
            cmp
        ).AsMeta;
    }
#else
        =>
    #if NET8_0_OR_GREATER
        Unsafe.BitCast<long, Meta>(
            Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                original.PrimitivePackage,
                original.SetEntanglement().PrimitivePackage
            )
        );
    #else
        Unsafe.As<long, Meta>(
            ref Unsafe.AsRef(
                Interlocked.CompareExchange(
                    ref Unsafe.As<Meta, long>(ref location),
                    original.PrimitivePackage,
                    original.SetEntanglement().PrimitivePackage
                )
            )
        );
    #endif
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Meta CompareExchange(scoped ref Meta location, Meta value, Meta comparand)
#if DEBUG
    {
        System.Diagnostics.Debug.Assert(value.HasEntanglement is false);
        return new Cast(
            Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                value.PrimitivePackage,
                comparand.PrimitivePackage
            )
        ).AsMeta;
    }
#else
        =>
    #if NET8_0_OR_GREATER
        Unsafe.BitCast<long, Meta>(
            Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                value.PrimitivePackage,
                comparand.PrimitivePackage
            )
        );
    #else
        Unsafe.As<long, Meta>(
            ref Unsafe.AsRef(
                Interlocked.CompareExchange(
                    ref Unsafe.As<Meta, long>(ref location),
                    value.PrimitivePackage,
                    comparand.PrimitivePackage
                )
            )
        );
    #endif
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Meta CompareSetEntanglement(scoped ref Meta location, Meta comparand)
#if DEBUG
    {
        //System.Diagnostics.Debug.WriteLineIf(comparand.HasEntanglement, $"{nameof(CompareSetEntanglement)}: {nameof(comparand)} has entanglement");
        System.Diagnostics.Debug.Assert(comparand.HasEntanglement is false);

        return new Cast(
            Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                comparand.SetEntanglement().PrimitivePackage,
                comparand.PrimitivePackage
            )
        ).AsMeta;
    }
#else
        =>
    #if NET8_0_OR_GREATER
        Unsafe.BitCast<long, Meta>(
            Interlocked.CompareExchange(
                ref Unsafe.As<Meta, long>(ref location),
                comparand.SetEntanglement().PrimitivePackage,
                comparand.PrimitivePackage
            )
        );
    #else
        Unsafe.As<long, Meta>(
            ref Unsafe.AsRef(
                Interlocked.CompareExchange(
                    ref Unsafe.As<Meta, long>(ref location),
                    comparand.SetEntanglement().PrimitivePackage,
                    comparand.PrimitivePackage
                )
            )
        );
    #endif  
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Meta CompareSetUnreachable(scoped ref Meta location, Meta comparand)
#if DEBUG
    {
        //System.Diagnostics.Debug.WriteLineIf(comparand.HasUnreachable, $"{nameof(CompareSetEntanglement)}: {nameof(comparand)} has entanglement");
        System.Diagnostics.Debug.Assert(comparand.HasUnreachable is false);

        return
#if NET8_0_OR_GREATER
                Unsafe.BitCast<long, Meta>(
                    Interlocked.CompareExchange(
                        ref Unsafe.As<Meta, long>(ref location),
                        comparand.SetUnreachableFlag().PrimitivePackage,
                        comparand.PrimitivePackage
                    )
                );
#else
                Unsafe.As<long, Meta>(
                    ref Unsafe.AsRef(
                        Interlocked.CompareExchange(
                            ref Unsafe.As<Meta, long>(ref location),
                            comparand.SetUnreachableFlag().PrimitivePackage,
                            comparand.PrimitivePackage
                        )
                    )
                );
#endif
    }
#else
        => 
    #if NET8_0_OR_GREATER
            Unsafe.BitCast<long, Meta>(
                Interlocked.CompareExchange(
                    ref Unsafe.As<Meta, long>(ref location),
                    comparand.SetUnreachableFlag().PrimitivePackage,
                    comparand.PrimitivePackage
                )
            );
    #else
            Unsafe.As<long, Meta>(
                ref Unsafe.AsRef(
                    Interlocked.CompareExchange(
                        ref Unsafe.As<Meta, long>(ref location),
                        comparand.SetUnreachableFlag().PrimitivePackage,
                        comparand.PrimitivePackage
                    )
                )
            );
    #endif
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Write(scoped ref Meta location, Meta value)
#if DEBUG
    {
        var local = location.VolatileRead();
        if (local.HasEntanglement is false
        || local.RawData > value.RawData)
            System.Diagnostics.Debug.Fail(nameof(Write));

        Volatile.Write(ref Unsafe.As<Meta, long>(ref location), value.PrimitivePackage);
    }
#else
        => Volatile.Write(ref Unsafe.As<Meta, long>(ref location), value.PrimitivePackage);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteUnreachable(scoped ref Meta location, Meta value)
#if DEBUG
    {
        var local = location.VolatileRead();
        if (local.HasEntanglement is false
        || local.RawData > value.RawData
        || value.HasUnreachable 
        || value.HasEntanglement)
            System.Diagnostics.Debug.Fail(nameof(Write));

        Volatile.Write(ref Unsafe.As<Meta, long>(ref location), value.SetUnreachableFlag().PrimitivePackage);
    }
#else
        => Volatile.Write(ref Unsafe.As<Meta, long>(ref location), value.SetUnreachableFlag().PrimitivePackage);
#endif

    internal static void ReleaseLockPerEntry(
            MetaTable metaTable,
            MetaSlotResult firstResult,
            MetaSlotResult secondResult)
    {
        TryRelease(metaTable, firstResult);
        TryRelease(metaTable, secondResult);
        static void TryRelease(MetaTable table, MetaSlotResult result)
        {
            var idx = result.SlotIndex + MetaTable.HeaderOffset;
            if ((uint)idx < (uint)table.Source.Length)
                InterlockedMeta.CompareRestore(
                    ref table.Source[idx],
                    result.Slot
                );
        }
    }
}
