using FlatDictionary.InternalModules.Atomic;
using FlatDictionary.InternalModules.InLockHook.Interface;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FlatDictionary.InternalModules.InLockHook;

internal static class Hook
{
    [ThreadStatic]
    static ExceptionDispatchInfo? exception;
    static void AddException(Exception ex)
    {
        var field = exception;
        if (field is null)
            exception = ExceptionDispatchInfo.Capture(ex);
        else
        {
            exception = ExceptionDispatchInfo.Capture(
                new AggregateException(
                    field.SourceException,
                    ex,
                    new InvalidOperationException(nameof(IHookPolicy))
                )
            );
        }
    }

    static void ThrowIfExcepted()
    {
        var ex = exception;
        if (ex is not null)
        {
            exception = null;
            //ExceptionDispatchInfo.Throw(ex);
            ex.Throw();
        }
    }


    internal static bool IsEmpty<THook, TKey, TValue>()
        where TKey : notnull
        => typeof(THook) == typeof(EmptyHook<TKey, TValue>);
    internal readonly struct EmptyHook<TKey, TValue> : IInLockAllHook<TKey, TValue>
        where TKey : notnull
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        { }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        { }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Update(
            AtomicField<TKey, TValue>.UnsafeSnapshot snapshot,
            int removedIndex,
            int addedIndex)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRemoveHook<TKey, TValue>.Remove(TKey key, TValue value, int index)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockClearHook.Clear(int oldCount)
        { }
    }

    internal static EmptyHook<TKey, TValue> Empty<TKey, TValue>(AtomicField<TKey, TValue> typeInference = default!)
        where TKey : notnull
        => default;

    internal readonly struct AnyDummy<TKey, TValue>
        : IRegistrationPolicy,
          IRegistrationHookPolicy<TKey, TValue>,
          IInLockRegistrationHook<TKey, TValue>,
          IInLockReallocateHook<TKey, TValue>,
          IInLockRemoveHook<TKey, TValue>,
          IInLockClearHook
        where TKey : notnull
    {
        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
        }

        readonly bool IRegistrationPolicy.IsUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Update(
            AtomicField<TKey, TValue>.UnsafeSnapshot snapshot,
            int removedIndex,
            int addedIndex)
        {
#if DEBUG
            throw new InvalidOperationException();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRemoveHook<TKey, TValue>.Remove(TKey key, TValue value, int index)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IHookPolicy.ThrowIfExcept()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IInLockClearHook.Clear(int oldCount)
        { }
    }

    internal readonly struct UpdateDummy<TKey, TValue> 
        : IRegistrationPolicy,
          IRegistrationHookPolicy<TKey, TValue>,
          IInLockRegistrationHook<TKey, TValue>,
          IInLockReallocateHook<TKey, TValue>
        where TKey : notnull
    {
        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
        }

        readonly bool IRegistrationPolicy.IsUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        {
#if DEBUG
            throw new InvalidOperationException();
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockRegistrationHook<TKey, TValue>.Update(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot, int removedIndex, int addedIndex)
        { }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void IHookPolicy.ThrowIfExcept()
        { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AnyDummy<TKey, TValue> CreateDummy<TKey, TValue>(AtomicField<TKey, TValue> typeInference = default!)
        where TKey : notnull
        => default;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UpdateDummy<TKey, TValue> CreateUpdateDummy<TKey, TValue>(AtomicField<TKey, TValue> typeInference = default!)
        where TKey : notnull
        => default;

    internal readonly struct RegistrationWrap<THook, TKey, TValue>(THook hook) : IRegistrationHookPolicy<TKey, TValue>, IHookPolicy
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
        where TKey : notnull
    {
        private readonly THook hook = hook;

        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        readonly bool IRegistrationPolicy.IsUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        
        readonly void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        {
            try
            {
                this.hook.Add(key, value, index);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        
        readonly void IInLockRegistrationHook<TKey, TValue>.Update(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot, int removedIndex, int addedIndex)
        {
            try
            {
                this.hook.Update(snapshot, removedIndex, addedIndex);
#if DEBUG
                throw new InvalidOperationException();
#endif
            }
            catch(Exception ex)
            {
                AddException(ex);
            }
        }
        
        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        {
            try
            {
                this.hook.Reallocated(snapshot);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        readonly void IHookPolicy.ThrowIfExcept()
        {
            ThrowIfExcepted();
        }
    }


    internal readonly struct UpdateWrap<THook, TKey, TValue>(THook hook) : IRegistrationHookPolicy<TKey, TValue>, IHookPolicy
       where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
       where TKey : notnull
    {
        private readonly THook hook = hook;
        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        readonly bool IRegistrationPolicy.IsUpdate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
        }


        readonly void IInLockRegistrationHook<TKey, TValue>.Add(TKey key, TValue value, int index)
        {
            try
            {
                this.hook.Add(key, value, index);
#if DEBUG
                throw new InvalidOperationException();
#endif
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }


        readonly void IInLockRegistrationHook<TKey, TValue>.Update(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot, int removedIndex, int addedIndex)
        {
            try
            {
                this.hook.Update(snapshot, removedIndex, addedIndex);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        {
            try
            {
                this.hook.Reallocated(snapshot);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        readonly void IHookPolicy.ThrowIfExcept()
        {
            ThrowIfExcepted();
        }
    }

    internal readonly struct ClearWrap<T>(T hook) : IInLockClearHook, IHookPolicy
        where T : IInLockClearHook
    {
        private readonly T hook = hook;

        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        readonly void IInLockClearHook.Clear(int oldCount)
        {
            try
            {
                this.hook.Clear(oldCount);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }

        readonly void IHookPolicy.ThrowIfExcept()
        {
            ThrowIfExcepted();
        }
    }

    internal readonly struct RemoveWrap<THook, TKey, TValue>(THook hook) : IInLockRemoveHook<TKey, TValue>, IHookPolicy
       where THook : IInLockRemoveHook<TKey, TValue>
       where TKey : notnull
    {
        private readonly THook hook = hook;
        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }
        readonly void IInLockRemoveHook<TKey, TValue>.Remove(TKey key, TValue value, int index)
        {
            try
            {
                this.hook.Remove(key, value, index);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }
        readonly void IHookPolicy.ThrowIfExcept()
        {
            ThrowIfExcepted();
        }
    }
  
    internal readonly struct ReallocateWrap<THook, TKey, TValue>(THook hook) : IInLockReallocateHook<TKey, TValue>, IHookPolicy
       where THook : IInLockReallocateHook<TKey, TValue>
       where TKey : notnull
    {
        private readonly THook hook = hook;
        readonly bool IHookPolicy.IsDummy
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }
        readonly void IInLockReallocateHook<TKey, TValue>.Reallocated(AtomicField<TKey, TValue>.UnsafeSnapshot snapshot)
        {
            try
            {
                this.hook.Reallocated(snapshot);
            }
            catch (Exception ex)
            {
                AddException(ex);
            }
        }
        readonly void IHookPolicy.ThrowIfExcept()
        {
            ThrowIfExcepted();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RegistrationWrap<THook, TKey, TValue> CreateRegistration<THook, TKey, TValue>(
            THook hook,
            AtomicField<TKey, TValue> typeInference = default!)
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
        where TKey : notnull
        => new(hook);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UpdateWrap<THook, TKey, TValue> CreateUpdate<THook, TKey, TValue>(
        THook hook,
        AtomicField<TKey, TValue> typeInference = default!)
        where THook : IInLockRegistrationHook<TKey, TValue>, IInLockReallocateHook<TKey, TValue>
        where TKey : notnull
        => new(hook);

    internal static ClearWrap<T> CreateClear<T>(T hook)
        where T : IInLockClearHook
        => new(hook);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RemoveWrap<THook, TKey, TValue> CreateRemove<THook, TKey, TValue>(THook hook, AtomicField<TKey, TValue> typeInference = default!)
        where THook : IInLockRemoveHook<TKey, TValue>
        where TKey : notnull
        => new(hook);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReallocateWrap<THook, TKey, TValue> CreateReallocate<THook, TKey, TValue>(
        THook hook, 
        AtomicField<TKey, TValue> typeInference = default!)
        where THook : IInLockReallocateHook<TKey, TValue>
        where TKey : notnull
        => new(hook);
}
