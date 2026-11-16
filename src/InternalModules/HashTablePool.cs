using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlatDictionary.InternalModules;

internal static class HashTablePool
{

    private const int TableSizeFloor = 15;
    private const int GuidDestination = 1;
    private static readonly bool isSupported =
        (Unsafe.SizeOf<Meta>() * TableSizeFloor) > Unsafe.SizeOf<BufferData>() + Unsafe.SizeOf<Meta>();
    private static readonly Guid guid = Guid.NewGuid();
    private static readonly BufferData defaultData = default;
    internal static Meta[] Rent(int minimumLength, out TableVersion tableVersion)
    {
        minimumLength |= TableSizeFloor;
        
        var array = ArrayPool<Meta>.Shared.Rent(minimumLength);
        if (isSupported)
        {
            if ((uint)GuidDestination >= (uint)array.Length)
            {
                tableVersion = TableVersion.Create();
                Array.Clear(array, 0, array.Length);
                return array; 
            }
            ref var destination = ref Unsafe.As<Meta, byte>(ref array[GuidDestination]);
            var bufferData = Unsafe.ReadUnaligned<BufferData>(ref destination);
            Unsafe.WriteUnaligned(ref destination, defaultData);
            if (bufferData == guid)
            {
                tableVersion = bufferData.version
                    .IncrementTableVersion(out var overflowed);
                    //.ResetGeneration();
                if(overflowed)
                    Array.Clear(array, 0, array.Length);
            } 
            else
            {
                tableVersion = TableVersion.Create(); 
                Array.Clear(array, 0, array.Length);
            }
        }
        else
        {
            tableVersion = TableVersion.Create();
            Array.Clear(array, 0, array.Length);
        }

        return array;
    }

    internal static void Return(Meta[] table, TableVersion version)
    {
        if ((uint)GuidDestination >= (uint)table.Length || TableSizeFloor > table.Length)
            return;

        ref var destination = ref Unsafe.As<Meta, byte>(ref table[GuidDestination]);
        Unsafe.WriteUnaligned(ref destination, new BufferData(guid, version));
        ArrayPool<Meta>.Shared.Return(table);
    }

    private readonly struct BufferData
    {
        public readonly Guid guid;
        public readonly TableVersion version;

        public BufferData(Guid guid, TableVersion version)
        {
            this.guid = guid;
            this.version = version;
        }
        public static bool operator ==(in BufferData left, in BufferData right)
            => left.guid == right.guid;
        public static bool operator !=(in BufferData left, in BufferData right)
            => left.guid != right.guid;
        public static bool operator ==(in BufferData left, in Guid guid)
            => left.guid == guid;
        public static bool operator !=(in BufferData left, in Guid guid)
            => left.guid != guid;
        public readonly override bool Equals(object obj)
            => obj is BufferData my && this == my;
        public readonly override int GetHashCode()
            => this.guid.GetHashCode();
    }

    private const int bufferableLog2Count = 32;
    private const int mainThreadBufferSize = 16;
    
    private readonly static (Meta[]?[] buffers, int current, int mask)[] shared = InitBuffers(mainThreadBufferSize);

    [ThreadStatic]
    private static (Meta[]?[] buffers, int current, int mask)[] TSBuffers = shared; //main thread only special handling
    private static (Meta[]?[] buffers, int current, int mask)[] InitBuffers(int size = 2)
    {
        var ret = new (Meta[]?[], int, int)[bufferableLog2Count]; 
        for (var i = 0; i < ret.Length; ++i)
        {
            ret[i] = (new Meta[size][], 0, size - 1);
        }
        return ret;
    }
    public static Meta[] ConcurrentRent(int minimumLog2Length, out TableVersion version)
    {
        var index = Helper.GetIndexOfPowerOfTwo(minimumLog2Length);
        var buffereds = TSBuffers;
        if (buffereds is null)
            buffereds = TSBuffers = InitBuffers();

        ref var scope = ref buffereds[index];
        var (tables, current, mask) = scope;
        Span<Meta> table;
        if ((uint)current < (uint)tables.Length)
        {
            ref var tableRef = ref tables[current];
            table = tableRef;
            tableRef = null;
            scope.current--;
        }
        else
        {
            current &= mask;
            ref var tableRef = ref tables[current];
            table = tableRef;
            tableRef = null;
            scope.current = current - 1;
        }


        throw new NotImplementedException();
    }

}