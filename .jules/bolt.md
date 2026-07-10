## 2024-05-24 - Avoid DistanceTo for performance
**Learning:** Using `DistanceTo` which involves expensive square root calculations can be a performance bottleneck when called frequently (like in AI update loops for many enemies).
**Action:** Use `DistanceSquaredTo` and compare against squared distances (e.g. `dist < RANGE * RANGE`) when checking if an object is within a certain radius.
