using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FlatDictionary.InternalModules.Utils;

internal static class SpanExtensions
{
    extension<T>(ReadOnlySpan<T> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> TrySlice(int start)
        {
            if ((uint)start <= (uint)span.Length)
                return span.Slice(start);
            else
                return default;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> TrySlice(int start, int length)
        {
            if (IntPtr.Size == sizeof(long))
            {
                if ((ulong)(uint)start + (ulong)(uint)length > (ulong)(uint)span.Length)
                    return default;
                else
                    return span.Slice(start, length);
            }
            else
            {
                if ((uint)start > (uint)span.Length)
                    return default;
                span = span.Slice(start);
                if ((uint)length > (uint)span.Length)
                    return default;

                return span.Slice(0, length);
            }
        }
    }
}
