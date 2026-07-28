# Determinism Audit

Runs the Unity-free `RunManager` against repository `GameData` with a stable
scripted input sequence. After every executed tick it folds run/stage state,
player coordinates, bullet/enemy counts, total score, and event count into a
64-bit FNV-1a hash.

```powershell
dotnet run --project Tools/DeterminismAudit -- 12345 3 18000
dotnet run --project Tools/DeterminismAudit -- 12345 3 18000
```

The audit passes when the two `hash=` values match. `stageCount` is the highest
stage the runner may enter and `tickCount` is the total tick budget. The output
also reports early termination such as `RunOver`. The runner raises player HP
inside its audit-only config so routine checks exercise stage transitions;
player-hit events remain represented by the folded per-tick event count.
