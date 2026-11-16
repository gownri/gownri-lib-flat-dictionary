//using System;
//using System.Collections.Generic;
//using System.Diagnostics.SymbolStore;
//using System.IO;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Threading;

//namespace HashIndex.InternalModules;

//internal struct TableSentinel
//{

//    //const int PriorityLock = int.MinValue;
//    internal const int FreeValue = 0;
//    internal const int PriorityLockValue = int.MaxValue;


//    internal static readonly Meta Unlocked = new(FreeValue, Meta.Data.Sentinel);
//    internal static readonly long UnlockedPrimitive = Unsafe.SizeOf<Meta>() == sizeof(long) ? Unsafe.As<Meta, long>(ref Unlocked) : throw new NotSupportedException();
//    internal static readonly Meta PriorityLocked = new(PriorityLockValue, Meta.Data.Sentinel);
//    internal static readonly long PriorityLockedPrimitive = Unsafe.As<Meta, long>(ref PriorityLocked);

//    internal static readonly Meta SentinelOnlyTableSource;
//    internal static Span<Meta> OnlySentinelTable => MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in SentinelOnlyTableSource), 1);

//    internal Meta Original;


//    public static explicit operator TableSentinel(Meta meta) 
//        => Unsafe.As<Meta, TableSentinel>(ref meta);
//}

//internal static class MetaTableSentinelExtensions
//{
//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    internal static ref TableSentinel GetSentinel(this Span<Meta> table)
//        =>
//#if DEBUG
//        ref (uint)0 >= (uint)table.Length ? ref Throw() :
//#endif
//        ref Unsafe.As<Meta, TableSentinel>(ref MemoryMarshal.GetReference(table));
//    private static ref TableSentinel Throw() => throw new ArgumentOutOfRangeException();
    
//    internal static ushort ReadTableVersion(this scoped ref TableSentinel sentinel)
//        => (ushort)(sentinel.Original.DataReader.Version - 1);
//    /// <returns><see cref="bool"/> : can try again</returns>
//    internal static bool ChainTryPriorityEnter(this scoped ref TableSentinel sentinel, ushort version, scoped ref LockResult lockTaken)
//    {
//        var local = sentinel.Original;
//#if DEBUG
//        if (lockTaken != default)
//            throw new ArgumentException();
//#endif
//        if (!local.VersionEquals(version))
//            return false;
//        if (local.KeyIndex != default)
//            return true;

//        if(
//            Interlocked.CompareExchange(
//                ref sentinel.AsPrimitive(),
//                local.UpdateKeyIndex(unchecked((int)LockResult.PriorityLockValue)).PrimitivePackage,
//                local.PrimitivePackage
//            ) == local.PrimitivePackage
//        )
//        {
//            lockTaken = LockResult.Priority;
//            return false;
//        }
//        else
//        {
//            lockTaken = LockResult.Free;
//            return true;
//        }
            
//    }

    
//    /// <returns><see cref="bool"/> : can try again</returns>
//    internal static bool TrySyncLock(this scoped ref TableSentinel sentinel, ushort version, scoped ref bool lockTaken)
//    {
//        var local = sentinel.Original;
//#if DEBUG
//        if (lockTaken != default)
//            throw new ArgumentException();
//#else
//        lockTaken = default;
//#endif
//        var state = (MetaTableState)local.DataReader.Hash;
//        if (!local.VersionEquals(version))
//            return false;
//        if ((state & MetaTableState.SyncLock) != 0)
//            return true;

//        var comparand = local.PrimitivePackage;
//        local = local.Update(local.DataReader.SetHash((byte)MetaTableState.SyncLock));
//        var locked = local.PrimitivePackage;

//        lockTaken = Interlocked.CompareExchange(ref sentinel.AsPrimitive(), locked, comparand) == comparand;
//        return false;
//    }


//    internal static void Exit(this scoped ref TableSentinel sentinel, LockResult lockResult)
//    {
//        ref var location = ref sentinel.AsPrimitive();
//        Meta original;
//        long value, comparand;
//        do
//        {
//            original = sentinel.Original;
//            value = original
//                .UpdateKeyIndex(
//                    (int)((uint)original.KeyIndex ^ lockResult.Value)
//                )
//                .PrimitivePackage;
//            comparand = original.PrimitivePackage;
//        }
//        while (
//            Interlocked.CompareExchange(
//                ref location,
//                value,
//                comparand
//            ) != comparand
//        );
//    }
//    internal static void SyncUnlock(this scoped ref TableSentinel sentinel)
//    {
//        ref var location = ref sentinel.AsPrimitive();
//        Meta original;
//        long value, comparand;
//        do
//        {
//            original = sentinel.Original;
//            value = original
//                .Update(
//                    original.DataReader
//                    .SetHash(
//                        (byte)((MetaTableState)original.DataReader.Hash & ~MetaTableState.SyncLock)
//                    )
//                )
//                .PrimitivePackage;
//            comparand = original.PrimitivePackage;
//        }
//        while (
//            Interlocked.CompareExchange(
//                ref location,
//                value,
//                comparand
//            ) != comparand
//        );
//    }


//    private static ref long AsPrimitive(ref this Meta meta)
//        => ref Unsafe.As<Meta, long>(ref meta);
//    [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    private static ref long AsPrimitive(ref this TableSentinel sentinel)
//        => ref Unsafe.As<TableSentinel, long>(ref sentinel);

//    private static bool VersionEquals(this Meta sentinel, ushort version)
//        => (sentinel.DataReader.Version-1) == version;
//}



//[Flags]
//internal enum MetaTableState : byte
//{
//    None = 0,

//    Sentinel = 1 << 0,
//    Disposed = 1 << 1,
//    SyncLock = 1 << 2,
//    All = Sentinel | Disposed | SyncLock,

//    MetaSlotStates = MetaSlotState.All,
//}

//[Flags]
//internal enum MetaSlotState : byte
//{
//    MetaTableStates = MetaTableState.All,

//    Existed = 1 << 3,
//    Failed = 1 << 4,

//    Result = Existed | Failed,

//    All = Existed | Failed,
//}

