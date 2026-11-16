using System;
using System.Runtime.InteropServices;

namespace FlatDictionary.InternalModules;

internal readonly struct LockResult : IEquatable<LockResult>
{
    internal const uint FreeValue = 0;
    internal const uint PriorityLockValue = ~FreeValue;

    internal static LockResult Priority { get; } = new(PriorityLockValue);
    internal static LockResult Free { get; } = new(FreeValue);

    internal readonly uint Value;
    internal readonly int IntValue => unchecked((int)this.Value);
    internal readonly bool IsPriorityLock => this.Value == PriorityLockValue;
    internal readonly bool HasLock => this.Value != FreeValue;

    internal LockResult(uint value) 
        => this.Value = value;
    public readonly override bool Equals(object? obj)
    {
        return obj is LockResult result && this.Equals(result);
    }

    public readonly bool Equals(LockResult other)
    {
        return this.Value == other.Value;
    }

    public readonly override int GetHashCode()
    {
        return this.Value.GetHashCode();
    }

    public static bool operator ==(LockResult left, LockResult right) => left.Value == right.Value;
    public static bool operator !=(LockResult left, LockResult right) => left.Value != right.Value;

    public static bool operator true(LockResult value) => value.HasLock;
    public static bool operator false(LockResult value) => !value.HasLock;
}

