# AGENTS.md

Instructions for an executor agent (deepcode or another CLI agent) working in this repository. Project rules, layout and conventions are in `CLAUDE.md`: read it first, they apply to you as well. Talk Russian in reports.

## Roles

- **Coordinator** (Claude): sets tasks, approves designs, writes acceptance tests, accepts the result, keeps the docs (`Docs/GameplayPrototype.md`, `Docs/stages/`).
- **Executor** (you): designs and implements one task at a time from a brief `Docs/tasks/<id>.md`.

One task = one executor session; the coordinator continues it with `--resume`, so keep the context and answer follow-ups with deltas.

## Workflow

1. **Design.** Read the brief and the doc sections it names (grep headings, read big files by line range, never whole). Do not write production code yet. Reply with a design note (at most about one page):
   - exact public API: types, members, signatures, units, frames (acceptance tests will be compiled against it);
   - algorithms and formulas, with the reason for each non-obvious choice;
   - invariants and edge cases (zero, inside, NaN/overflow, float conversion, hysteresis);
   - files to create or change;
   - open questions and options, with your recommendation.
2. **Design review.** The coordinator answers "approved" or with remarks. Answer remarks with **the delta only**: what changed in the note and why, not the whole note again. Repeat until approved.
3. **Acceptance tests.** The coordinator writes the acceptance tests (file named in the brief) against the approved API. **Do not edit acceptance test files.** If a test looks wrong, stop and report: which test, why, what you propose.
4. **Implementation.** Implement the approved design, run the tests as you go. You may add your own tests in a separate file named in the brief. If the approved design turns out not to work, stop and send a design delta for approval; never change the design silently.
5. **Report** (when all tests are green), short:
   - files created/changed;
   - result lines of every required test run (pipe through `tail`);
   - deviations from the approved design (should be none, see step 4);
   - assumptions you made on your own;
   - what is not covered by tests;
   - **risks and warnings** (precision, performance, determinism client/server, future stages);
   - draft of the "what was done" section for the stage log (a few bullets).

## Rules

- Touch only the files the brief allows. Never edit `Assets/Plugins/`, scenes, `.asset`/`.prefab`/`.meta` files, docs, or the server unless the brief says so. Do not create `.meta` files (Unity does).
- No commits, no branches, no stashes, no `git checkout`/`reset`.
- No new packages.
- Engine-free code (`Assets/Orbital/Core/Flight` and other files linked into `Tools/OrbitMathTests` or the server): no `UnityEngine`, no `Debug.*`, no allocations in hot functions, double precision, blittable structs + static pure functions (see `SoiFrames.cs`, `NavBall.cs`).
- Unity batch commands (`Tools/unity-build.sh`) only when the brief asks; they fail while the Editor has the project open, then report it instead of retrying.

## Test commands

- Math/core: `dotnet test` in `Tools/OrbitMathTests` (a new test file needs a `<Compile Include=...>` line in `OrbitMathTests.csproj`; `Core/Flight/*.cs` is included by mask).
- Server: `dotnet test` in `E:\Projects\orbital-server\OrbitServer.Tests` — required whenever `Assets/Orbital/Core/Flight`, `Core/Navigation`, `Core/TrajectorySystem` or `Plugins/Ara3D` change (the server links them).
- Unity (only if the brief asks): `Tools/unity-build.sh compile` / `test EditMode` (Git Bash).
