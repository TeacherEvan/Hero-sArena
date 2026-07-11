## $(date +%Y-%m-%d) - Zero Allocation Random Selection using Span
**Learning:** Selecting random items from an array traditionally uses a `List<T>` and `RemoveAt()`, which causes GC allocation on `new List` and $O(N)$ overhead from `RemoveAt()`.
**Action:** Use `stackalloc` to create a `Span<T>`, copy array elements with `AsSpan().CopyTo()`, and use a "swap-and-pop" technique (swapping the selected item with the last item and decrementing the size) for $O(1)$ item removal and zero allocations on the heap.

## 2024-05-24 - Avoid DistanceTo for performance
**Learning:** Using `DistanceTo` which involves expensive square root calculations can be a performance bottleneck when called frequently (like in AI update loops for many enemies).
**Action:** Use `DistanceSquaredTo` and compare against squared distances (e.g. `dist < RANGE * RANGE`) when checking if an object is within a certain radius.

## 2026-07-10 - Avoid Redundant SpatialHashGrid Updates
**Learning:** Checking distance traveled (`DistanceSquaredTo`) before updating spatial grid nodes saves substantial time compared to doing unconditional updates in high-frequency loops (like `_PhysicsProcess`).
**Action:** Always verify if a state change warrants expensive data structure updates.

## 2024-07-10 - Time-Sliced Instantiation in Godot
**Learning:** Godot's `Instantiate` method can be expensive when called in a tight loop for hundreds or thousands of nodes, causing significant frame time spikes (e.g., ~30ms for 2000 nodes).
**Action:** Use time-slicing (spreading the instantiation across multiple frames in `_PhysicsProcess` or `_Process`) or object pooling to maintain smooth frame rates.

## 2024-03-24 - SpatialHashGrid Allocation Bottleneck
**Learning:** The spatial hash grid implementation eagerly removes and re-inserts entities into cell lists on every frame, even when they haven't crossed cell boundaries. In a game with many moving objects, this causes excessive O(N) list operations and heap allocations, creating a severe bottleneck during high-action scenes. The previous grid logic also had a dormant bug: bounding boxes computed during `Remove` didn't exactly match `Insert` bounds.
**Action:** When working with spatial partitions or grid systems, always cache the exact bounding cells and implement a fast-path for the `Update` loop to skip operations if the entity is within the exact same grid bounds as the previous frame.
