## 2026-07-10 - Avoid Redundant SpatialHashGrid Updates
**Learning:** Checking distance traveled (`DistanceSquaredTo`) before updating spatial grid nodes saves substantial time compared to doing unconditional updates in high-frequency loops (like `_PhysicsProcess`).
**Action:** Always verify if a state change warrants expensive data structure updates.
