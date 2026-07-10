## 2024-07-10 - Time-Sliced Instantiation in Godot
**Learning:** Godot's `Instantiate` method can be expensive when called in a tight loop for hundreds or thousands of nodes, causing significant frame time spikes (e.g., ~30ms for 2000 nodes).
**Action:** Use time-slicing (spreading the instantiation across multiple frames in `_PhysicsProcess` or `_Process`) or object pooling to maintain smooth frame rates.
