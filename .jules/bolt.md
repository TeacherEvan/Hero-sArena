## $(date +%Y-%m-%d) - Zero Allocation Random Selection using Span
**Learning:** Selecting random items from an array traditionally uses a `List<T>` and `RemoveAt()`, which causes GC allocation on `new List` and $O(N)$ overhead from `RemoveAt()`.
**Action:** Use `stackalloc` to create a `Span<T>`, copy array elements with `AsSpan().CopyTo()`, and use a "swap-and-pop" technique (swapping the selected item with the last item and decrementing the size) for $O(1)$ item removal and zero allocations on the heap.
