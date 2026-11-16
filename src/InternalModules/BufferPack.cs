//using HashIndexInternalModules;
//using System;
//using System.Buffers;
//using System.Runtime.CompilerServices;

//namespace HashIndex.InternalModules;

//internal class BufferPack
//{
//    private readonly Meta[] array;
//    private readonly int size;
//    internal HashTableVersion version;
//    private bool isRented = false;
//    private bool isDisposed = false;
//    public BufferPack(int size)
//    {
//        this.version = HashTableVersion.Create();
//        this.size = Helper.RoundUpToTableSize(size);
//        this.array = ArrayPool<Meta>.Shared.Rent(size);
//    }

//    internal Span<Meta> Rent()
//    {
//        this.isRented = true;
//        return new Span<Meta>(this.array, 0, this.size);
//    }

//    public void Return(UnitHashIndex unit)
//    {
//        var returnBucket = unit.BucketSource;
//        if (Unsafe.AreSame(ref this.array[0], ref unit.BucketSource[0]) )
//        {
//            this.version = unit.Version;
//            this.isRented = false;
//        }
//        else
//        {
//            throw new InvalidOperationException("BufferPack is not the owner of the buffer");
//        }
//        if (this.isDisposed)
//            ArrayPool<Meta>.Shared.Return(this.array);
//    }

//    public void Dispose()
//    {
//        if (!this.isRented)
//        {
//            ArrayPool<Meta>.Shared.Return(this.array);
//        }
//        this.isDisposed = true;
//    }
//}
