//#define ALLOWBIGENDIAN

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using static FlatDictionary.InternalModules.TableHeader.LockTaken;

namespace FlatDictionary.InternalModules;

/// <summary>
/// this is mutable
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct TableHeader
{
    private const int StateOffset = 0;
    private const int WriterCountOffset = sizeof(int);

    [FieldOffset(0)]
    private long AsPrimitive;

    [FieldOffset(StateOffset)]
    private State syncState;
    internal readonly State SyncState
        => this.syncState;
    [FieldOffset(StateOffset)]
    private int syncStatePrimitive;

    [FieldOffset(WriterCountOffset)]
    private int writerCount;
    internal readonly int WriterCount
        => this.writerCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TableHeader(ushort version)
    {
        Unsafe.SkipInit(out this);
        this.writerCount = 0;
        this.syncState = new(version);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly State VolatileReadState()
        => InnerOnlyAccessibilityProve.CastFromIntToState(
            Volatile.Read(
                ref Unsafe.AsRef(in this.syncStatePrimitive)
            )
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly int VolatileReadWriterCount()
        => Volatile.Read(ref Unsafe.AsRef(in this.writerCount));
    internal LockTaken AcquireWithSpinWait(LockType lockType)
    {
        var spinWait = new SpinWait();
        LockTaken lockTaken;
        while (!(lockTaken = this.TryAcquire(lockType)))
            //if (!Thread.Yield())
                spinWait.SpinOnce();
        return lockTaken;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LockTaken AcquireWithDoubleWait(LockType lockType, object lockObj)
    {
        LockTaken taken;
        if (taken = this.TryAcquire(lockType))
            return taken;
        else
            return DoubleWait(ref this, lockType, lockObj);
        static LockTaken DoubleWait(scoped ref TableHeader header, LockType lockType, object lockObj)
        {
            var spinWait = new SpinWait();
            LockTaken lockTaken = default;
            while (!(lockTaken = header.TryAcquire(lockType)))
            {
                if (spinWait.NextSpinWillYield
                && header.syncState.Contains(LockType.HeavyProcess))
                {
                    HeavyLock(lockObj);
                    [MethodImpl(MethodImplOptions.NoInlining)]
                    static void HeavyLock(object lockObj)
                    {
                        lock (lockObj) { }
                    }
                }
                spinWait.SpinOnce();
            }
            return lockTaken;
        }
    }

    internal LockTaken TryAcquireUntilHeavyProcess(LockType lockType)
    {
        LockTaken lockTaken;
        var sp = new SpinWait();
        while ((lockTaken = this.TryAcquire(lockType)).HasLock is false)
        {
            if (this.SyncState.Contains(LockType.HeavyProcess))
                return default;
            sp.SpinOnce();
        }

        return lockTaken;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LockTaken AcquireInZeroWriter(LockType lockType, object? lockObj = null)
    {
        var lockTaken = lockObj is null 
            ? this.AcquireWithSpinWait(lockType)
            : this.AcquireWithDoubleWait(lockType, lockObj);
        if (this.VolatileReadWriterCount() == 0)
            return lockTaken;
        else
            return this.WaitWhileExistWriter(lockTaken, lockType, lockObj);
    }

    internal LockTaken WaitWhileExistWriter(LockTaken lockTaken, LockType lockType, object? lockObj = null)
    {
        var sp = new SpinWait();
        do
        {
            if (lockTaken)
                this.Release(lockTaken);
            sp.SpinOnce();
            //lockTaken = default;
            lockTaken = lockObj is null
                ? this.AcquireWithSpinWait(lockType)
                : this.AcquireWithDoubleWait(lockType, lockObj);
        } while (this.VolatileReadWriterCount() != 0);

        return lockTaken;
    }
    internal LockTaken TryAcquire(LockType lockType)
    {
        var comparand = this.syncState;
        var ret = comparand.UpdateLockTypeContext(addLockType: lockType);
        comparand = comparand.LockMasked();
        return comparand.AsPrimitive
            == Interlocked.CompareExchange(ref this.syncStatePrimitive, ret.AsPrimitive, comparand.AsPrimitive)
                ? InnerOnlyAccessibilityProve.CastLockTaken(ret)
                : InnerOnlyAccessibilityProve.CastLockTaken(comparand);

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int IncrementWriterCount(bool inPrivate = false)
        => inPrivate
            ? this.writerCount += 1
            : Interlocked.Increment(ref this.writerCount);

    internal void DecrementWriterCount()
        => Interlocked.Decrement(ref this.writerCount);
    internal LockTaken Update(
        LockTaken lockTaken,
        LockType addLockType = default,
        Context addContext = default,
        Context unsetContext = default
    )
    {
#if DEBUG
        if (!lockTaken || this.syncStatePrimitive != lockTaken.CurrentState.AsPrimitive)
            throw new InvalidOperationException($"{nameof(LockTaken)} is invalid value");
#endif
        var ret = InnerOnlyAccessibilityProve.CastLockTaken(
            lockTaken.CurrentState.UpdateLockTypeContext(addLockType, addContext, default, unsetContext)
        );
        Volatile.Write(
            ref this.syncStatePrimitive,
            ret.CurrentState.AsPrimitive
        );
        return ret;
    }

    internal void Release(LockTaken lockTaken, Context unsetContext)
        => Volatile.Write(
            ref this.syncStatePrimitive, 
            lockTaken.CurrentState.UpdateLockTypeContext(
                unsetLockType: lockTaken.CurrentState.LockType,
                unsetContext: unsetContext
            ).AsPrimitive
        );
    internal void Release(LockTaken lockTaken, Context unsetContext = default, Context addContext = default)
        => Volatile.Write(
            ref this.syncStatePrimitive,
            lockTaken.CurrentState.UpdateLockTypeContext(
                addContext: addContext,
                unsetLockType: lockTaken.CurrentState.LockType,
                unsetContext: unsetContext
            ).AsPrimitive
        );
    internal void ReleaseAtomicUpdateVersion(LockTaken lockTaken, ushort newVersion, Context addContext = Context.Clean)
        => Volatile.Write(
            ref this.syncStatePrimitive,
            lockTaken.CurrentState.UpdateLockTypeContext(
                addContext: addContext,
                unsetLockType: lockTaken.CurrentState.LockType
            ).UpdateVersion(newVersion).AsPrimitive
        );

    internal LockTaken.ByWriterCount ReleaseAtomicIncrementWriterCount(LockTaken lockTaken, Context unsetContext = Context.Clean)
    {
#if DEBUG
        if(!lockTaken)
            throw new InvalidOperationException("lockTaken has not lock");
#endif
        Interlocked.Add(
            ref this.AsPrimitive,
#if !ALLOWBIGENDIAN
            (long)(uint)lockTaken.CurrentState.UnlockAddition(unsetContext)
#else
            (long)lockTaken.CurrentState.UnlockAddition()
                    + new TableHeader()
                    {
                        writerCount = 1
                    }.AsPrimitive
#endif
        );

        return InnerOnlyAccessibilityProve.CreateWriterCountLockTaken(-1);
    }

    internal int Release(LockTaken.ByWriterCount toWriterCount)
        => Interlocked.Add(
            ref this.writerCount,
            toWriterCount.Value
        );

    public readonly override string ToString()
        => $"SyncState:[{this.syncState}], WriterCount:{this.writerCount}";

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct InnerOnlyAccessibilityProve
    {
        [FieldOffset(0)]
        private readonly int Value;
        [FieldOffset(0)]
        private readonly LockTaken State;

        [FieldOffset(0)]
        private readonly LockTaken.ByWriterCount WriterCount;

        [FieldOffset(0)]
        private readonly State StateCast;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InnerOnlyAccessibilityProve(int value)
        {
            Unsafe.SkipInit(out this);
            this.Value = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InnerOnlyAccessibilityProve(State state)
        {
            Unsafe.SkipInit(out this);
            this.Value = state.AsPrimitive;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LockTaken CastLockTaken(State state)
            => new InnerOnlyAccessibilityProve(state).State;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LockTaken.ByWriterCount CreateWriterCountLockTaken(int value)
            => new InnerOnlyAccessibilityProve(value).WriterCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static State CastFromIntToState(int value)
            => new InnerOnlyAccessibilityProve(value).StateCast;
    }

    [Flags]
    internal enum LockType : byte
    {
        Free = 0,

        AnySync = 1 << 0,
        HeavyProcess = (1 << 1) | AnySync,

        //table operations
        Registration = (1 << 3) | AnySync,
        CompactionGrow = (1 << 4) | HeavyProcess,
        Removing = (1 << 5) | AnySync,
        Clearing = (1 << 6) | AnySync,
        Readonly = (1 << 7) | AnySync,

    }

    [Flags]
    internal enum Context : byte
    {
        None = 0,
        Clean = 1 << 0,
        Leave = 1 << 1,
        Disposed = 1 << 2,
    }

    internal readonly struct LockTaken
    {
        internal readonly State CurrentState;

        internal readonly bool HasLock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.CurrentState.HasLock;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator true(LockTaken self)
            => self.CurrentState.HasLock;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator false(LockTaken self)
            => self.CurrentState.HasLock is false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !(LockTaken self)
            => self.CurrentState.HasLock is false;
        public readonly override string ToString()
            => $"{(this ? this.CurrentState : "default")}";

        internal readonly struct ByWriterCount
        {
            internal readonly int Value;

            internal readonly bool HasLock
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.Value != 0;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator true(ByWriterCount self)
              => self.Value != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator false(ByWriterCount self)
                => self.Value == 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !(ByWriterCount self)
                => self.Value == 0;

            public readonly override string ToString()
                => $"Value:{this.Value}";
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct State
    {
        internal const int LockTypeContextBlockOffset = sizeof(ushort);

        private const int ContextBlockBitOffset = LockTypeContextBlockOffset * 8;

        private const int contextOffset = LockTypeContextBlockOffset;
        private const int ContextBitOffset = contextOffset * 8;

        private const int LockTypeOffset = contextOffset + sizeof(byte);
        private const int LockTypeBitOffset = LockTypeOffset * 8;

        private const int LockTypeMask = (int)byte.MaxValue << LockTypeBitOffset;
        internal const int VersionOffset = 0;
        private const int VersionBitOffset = VersionOffset * 8;

        [FieldOffset(0)]
        internal readonly int AsPrimitive;

        [FieldOffset(0)]
        internal readonly ushort Version;

        [FieldOffset(LockTypeContextBlockOffset)]
        private readonly ushort lockTypeContextBlock;

        [FieldOffset(contextOffset)]
        internal readonly Context Context;
        [FieldOffset(LockTypeOffset)]
        internal readonly LockType LockType;

        internal readonly bool HasLock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.LockType != LockType.Free;
        }
        internal readonly bool IsClean
            => (this.Context & Context.Clean) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal State(ushort version, Context cleanContext = Context.Clean) 
            : this(version, LockType.Free, cleanContext)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private State(int value)
        {
            Unsafe.SkipInit(out this);
            this.AsPrimitive = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private State(ushort version, LockType lockType, Context context)
        {
            Unsafe.SkipInit(out this);
            this.Version = version;
            this.Context = context;
            this.LockType = lockType;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool Contains(LockType lockType)
            => (this.LockType & lockType) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool Contains(Context context) 
            => (this.Context & context) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int UnlockAddition(Context unsetContexts = Context.Clean)
            =>
#if !ALLOWBIGENDIAN
            -(this.AsPrimitive & (LockTypeMask | ((int)unsetContexts << ContextBitOffset)));
#else
            -this.AsPrimitive + new State(this.Version, default, this.Context & ~unsetContexts).AsPrimitive;
#endif
#if ALLOWBIGENDIAN
        internal readonly State WithClearFlag(bool clearFlag)
            => new (
                this.Version,
                this.LockType,
                clearFlag ? Context.Clean : Context.None
            );
#endif
        internal readonly int UpdateVersionAddition(ushort newVersion)
            =>
#if !ALLOWBIGENDIAN
            -((int)this.Version << VersionBitOffset) + (int)newVersion << VersionBitOffset;
#else
            -(new State(this.Version, default, default).AsPrimitive) + new State(newVersion, default, default).AsPrimitive;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly State UpdateVersion(ushort newVersion)
            => new(newVersion, this.LockType, this.Context);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly State UpdateLockTypeContext(
            LockType addLockType = default,
            Context addContext = default,
            LockType unsetLockType = default,
            Context unsetContext = default)
            => new(this.Version, (this.LockType | addLockType) & ~unsetLockType, (this.Context | addContext) & ~unsetContext);

        internal readonly State LockMasked()
            => new(this.Version, default, this.Context);

        public readonly override string ToString()
            => $"Version:{this.Version}, {nameof(TableHeader.LockType)}:{this.LockType}, {nameof(TableHeader.Context)}:{this.Context} ";
    }
}
