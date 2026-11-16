using System.Runtime.CompilerServices;


namespace FlatDictionary.InternalModules.Atomic;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct RegistrationResult(int value)
{
    const int based = 1;
    private readonly int value = value + based;

    internal readonly int Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.value - based;
    }
    internal readonly bool IsFailed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.value == default;
    }
    internal readonly bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.value != default;
    }
    internal static RegistrationResult Failed
        => default;
}


