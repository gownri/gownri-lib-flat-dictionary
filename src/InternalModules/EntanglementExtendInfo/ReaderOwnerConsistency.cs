using System;
using System.Threading;
using System.Runtime.CompilerServices;
namespace FlatDictionary.InternalModules.EntanglementExtendInfo;

internal readonly struct ReaderOwnerConsistency : IEquatable<ReaderOwnerConsistency>
{
    private const int OwnerFlag = 1;

    private const int ReaderCountBlockShifter = 1;
    private const int ReaderInclement = 1 << ReaderCountBlockShifter;

    private readonly int value;

    internal readonly int ReaderCount
        => (this.value >>> ReaderInclement);
    internal readonly bool HasOwner
        => (this.value & OwnerFlag) != default;
    internal readonly bool HasSomeOne
        => this.value != default;
    internal readonly ReaderOwnerConsistency VolatileRead()
        => new(Volatile.Read(ref Unsafe.AsRef(this.value)));

    private ReaderOwnerConsistency(int value)
        => this.value = value;

    private static ReaderOwnerConsistency CompareExchange(
        scoped ref ReaderOwnerConsistency location,
        ReaderOwnerConsistency value,
        ReaderOwnerConsistency comparand)
    {
        return new(
            Interlocked.CompareExchange(
                ref Unsafe.As<ReaderOwnerConsistency, int>(ref location),
                value.value, 
                comparand.value
            )
        );
    }
    internal static void OwnerEnter(scoped ref ReaderOwnerConsistency location)
    {
#if DEBUG
        System.Diagnostics.Debug.Assert((
            Interlocked.Exchange(
                ref Unsafe.As<ReaderOwnerConsistency, int>(ref location),
                location.VolatileRead().value | OwnerFlag
            ) & OwnerFlag)
            != default
        );
#else
        Volatile.Write(ref Unsafe.As<ReaderOwnerConsistency, int>(ref location), OwnerFlag);
#endif
    }
    internal static bool TryFastOwnerExit(scoped ref ReaderOwnerConsistency location)
    {
        return OwnerFlag == Interlocked.CompareExchange(ref Unsafe.As<ReaderOwnerConsistency, int>(ref location), default, OwnerFlag);
    }
    internal static bool OwnerExit(scoped ref ReaderOwnerConsistency location)
    {
        var comparand = location.VolatileRead().value | OwnerFlag;
        while (true)
        {
            if (comparand == (comparand = Interlocked.CompareExchange(
                ref Unsafe.As<ReaderOwnerConsistency, int>(ref location),
                comparand & ~OwnerFlag,
                comparand)))
                return true;
            else if ((comparand & OwnerFlag) == default)
                return false;
        }
    }

    internal static bool TryReaderEnter(scoped ref ReaderOwnerConsistency location)
    {
        var comparand = location.VolatileRead().value | OwnerFlag;
        while (true)
        {
            if (comparand == (comparand = Interlocked.CompareExchange(
                                            ref Unsafe.As<ReaderOwnerConsistency, int>(ref location),
                                            comparand + ReaderInclement,
                                            comparand
                                        )
                )
            )
                return true;
            else if ((comparand & OwnerFlag) == default)
                return false;
        }
    }

    internal static int ReaderExit(scoped ref ReaderOwnerConsistency location)
    {
        var ret = Interlocked.Add(ref Unsafe.As<ReaderOwnerConsistency, int>(ref location), -ReaderInclement)
            >>> ReaderCountBlockShifter;
#if DEBUG
        System.Diagnostics.Debug.Assert(ReaderCountBlockShifter != int.MaxValue);
#endif
        return ret;
    }

    public override bool Equals(object? obj)
    {
        return obj is ReaderOwnerConsistency consistency && this.Equals(consistency);
    }

    public bool Equals(ReaderOwnerConsistency other)
    {
        return this.value == other.value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.value);
    }

    public static bool operator ==(ReaderOwnerConsistency lhs, ReaderOwnerConsistency rhs)
       => lhs.value == rhs.value;
    public static bool operator !=(ReaderOwnerConsistency lhs, ReaderOwnerConsistency rhs)
        => lhs.value != rhs.value;
}
