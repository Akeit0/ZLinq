﻿namespace ZLinq
{
    partial class ValueEnumerableExtensions
    {
        public static ValueEnumerable<TakeLast<TEnumerator, TSource>, TSource> TakeLast<TEnumerator, TSource>(this ValueEnumerable<TEnumerator, TSource> source, Int32 count)
            where TEnumerator : struct, IValueEnumerator<TSource>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
            => new(new(source.Enumerator, count));

    }
}

namespace ZLinq.Linq
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
#if NET9_0_OR_GREATER
    public ref
#else
    public
#endif
    struct TakeLast<TEnumerator, TSource>(TEnumerator source, Int32 count)
        : IValueEnumerator<TSource>
        where TEnumerator : struct, IValueEnumerator<TSource>
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        TEnumerator source = source;
        readonly int takeCount = Math.Max(0, count);
        RefBox<ValueQueue<TSource>>? q;

        public bool TryGetNonEnumeratedCount(out int count)
        {
            if (source.TryGetNonEnumeratedCount(out count))
            {
                count = Math.Min(count, takeCount);
                return true;
            }

            count = 0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TSource> span)
        {
            if (source.TryGetSpan(out span))
            {
                if (span.Length > takeCount)
                {
                    span = span[^takeCount..];
                }
                return true;
            }
            span = default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TSource> destination, Index offset)
        {
            if (source.TryGetNonEnumeratedCount(out var count))
            {
                var actualTakeCount = Math.Min(count, takeCount);
                if (actualTakeCount <= 0)
                {
                    return false;
                }

                var takeLastStartIndex = count - actualTakeCount;
                var offsetInTakeLast = offset.GetOffset(actualTakeCount);

                if (offsetInTakeLast < 0 || offsetInTakeLast >= actualTakeCount)
                {
                    return false;
                }

                var sourceOffset = takeLastStartIndex + offsetInTakeLast;
                var remainingElements = actualTakeCount - offsetInTakeLast;
                var elementsToCopy = Math.Min(remainingElements, destination.Length);

                if (elementsToCopy <= 0)
                {
                    return false;
                }

                return source.TryCopyTo(destination.Slice(0, elementsToCopy), sourceOffset);
            }

            return false;
        }

        public bool TryGetNext(out TSource current)
        {
            if (takeCount == 0)
            {
                Unsafe.SkipInit(out current);
                return false;
            }

            if (q == null)
            {
                q = new(new(4));
            }

        DEQUEUE:
            if (q.GetValueRef().Count != 0)
            {
                current = q.GetValueRef().Dequeue();
                return true;
            }

            while (source.TryGetNext(out current))
            {
                if (q.GetValueRef().Count == takeCount)
                {
                    q.GetValueRef().Dequeue();
                }
                q.GetValueRef().Enqueue(current);
            }

            if (q.GetValueRef().Count != 0) goto DEQUEUE;

            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose()
        {
            q?.Dispose();
            source.Dispose();
        }
    }
}
