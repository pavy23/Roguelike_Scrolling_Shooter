# Determinism Audit

Runs the Unity-free `RunManager` against repository `GameData` with stable
scripted inputs. The audit folds every publicly observable run and battle field,
all ordered entity collections, generated stage data, reward candidates and
choices, power-up state, statistics, and complete per-tick events into a stable
64-bit FNV-1a hash.

Run the full audit suite:

```powershell
dotnet run --project Tools/DeterminismAudit -- --suite
```

The suite runs five multi-stage, multi-seed, multi-reward-path scenarios twice
and requires exact trace hash equality. It also sweeps 256 seeds across a
synthetic `maxPerRun` boundary: one path excludes a capped reward while another
keeps it eligible, then verifies equal battle traces and equal next-stage reward
options after both paths converge. This proves that different eligible pool
sizes cannot shift battle RNG or a later stage's reward RNG stream.

A single rotating-choice scenario remains available:

```powershell
dotnet run --project Tools/DeterminismAudit -- 12345 3 30000
```

`stageCount` is the number of stages to complete and `tickCount` is the total
tick budget. The runner raises player HP only in its audit config so traversal
does not depend on balance survivability; player hits, HP changes, and events
remain part of the folded state.
