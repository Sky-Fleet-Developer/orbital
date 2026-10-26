# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

Orbital: a Unity experiment in interplanetary travel mechanics, built on the two-body problem (Keplerian orbits, orbit transitions/entry calculation via Newton's method, path/node planning). See `README.md`.

- Unity **2021.3.2f1** (`ProjectSettings/ProjectVersion.txt`), C#, Windows dev environment.
## Product direction

Goal: a prototype of an **idle/incremental space game** built on the orbital mechanics.

- The player flies a spacecraft to reach other bodies. Like KSP, but **no player time acceleration**, plus a **social layer**: players see each other's running craft and chat by text over a "radio".
- Achievements are tied to locations; more remote = more valuable.
- Long-term control is an immersive cockpit simulator. **Not now** — the prototype uses a greybox UI.
- The prototype must still include **interaction with a planet's surface** (landing/launch/surface state), not only orbits.
- Current phase: fine-tune the orbital mechanics and test what they can do (accuracy, stability, transitions between spheres of influence, long-run determinism). Multiplayer/network design comes later but keep simulation state serializable and deterministic where practical, since craft must be visible to other players and survive real-time (no warp) runs.

Testing today is manual (logs, `Debug.DrawLine`). Improving this is in scope: prefer pure-C# orbital math that can be covered by EditMode tests (`com.unity.test-framework`) over scene-only checks, and add numeric assertions (energy, angular momentum, round-trip anomaly conversions) alongside visual debug.

## Status

- Branch `master` is main; current work is on `online` (SQLite-backed world storage in progress).

## Layout

Own code lives in `Assets/Orbital/`. Everything under `Assets/Plugins/` is third-party or vendored (Zenject, Odin Inspector, Ara3D, NavMeshComponents, UnityWinForms, Sqlite, LocalPool, Patterns, ScriptableObjectContainer) — do not edit or refactor it unless explicitly asked.

| Path | Purpose |
|---|---|
| `Orbital/Core/` | Simulation core (asmdef `Core`): `World`, `TreeContainer`, time/event loop, trajectory system, orbit navigation (`PathBuilder`, `OrbitTransitionUtility`), serialization |
| `Orbital/Core/TrajectorySystem/` | Celestial bodies, static/dynamic orbits, single-center and double-system branches, mass utilities |
| `Orbital/Core/Simulation/` | `IterativeSimulation`, `RuntimeOrbit`, trajectory containers |
| `Orbital/Core/Serialization/` | JSON converters, `WorldSqlDataAdapter` (world <-> SQLite tables) |
| `Orbital/Technology/` | ECS layer (Leopotam `ecs`): components, systems (jet force, input, vehicle creation), `EcsAdapter` |
| `Orbital/View/` | Rendering of orbits/bodies/map (`MapView`, `*View`, `*ViewUpdater`, settings) |
| `Orbital/Player/` | `PlayerModel`, `PlayerMotor`, `Session` |
| `Orbital/Installers/` | Zenject installers (`GameSceneInstaller`, `PlayerInstaller`) |
| `Orbital/WorldEditor/` | Editor-side state machine for building the world tree |
| `Orbital/Test/` | Ad-hoc test MonoBehaviours (Database, Forms, Path, Trajectory) |
| `DB/sqliteDb.db` | SQLite world database |
| `Assets/LineRendering/` | Line/circle shaders used for orbit drawing |
| Scenes | `Assets/Main.unity`, `Assets/SimulationScene.unity` |

Assemblies: `Core` (`Orbital.Core.asmdef`) and `Orbital` (`Orbital.asmdef`, depends on Core). Core must not depend on the `Orbital` assembly. References are by GUID; when adding a dependency, edit the asmdef rather than relying on the generated `.csproj`.

## Architecture notes

- **DI:** Zenject. Services (`World`, `TimeService`, `EventLoopEmitterService`, `ComponentsRegistrationService`) are bound in `GameSceneInstaller`. Prefer `[Inject]` over singletons/`FindObjectOfType`.
- **World:** built from SQLite via `WorldSqlDataAdapter.BuildWorldFromTables(worldName, databasePath)`; holds a `TreeContainer` of celestial bodies (static orbits) and dynamic bodies.
- **Update loop:** custom handler interfaces (`IUpdateHandler`, `IFixedUpdateHandler`, `ILateUpdateHandler`, `IUpdateByFrequencyHandler`, `ISimulationSpaceTriggerHandler`) dispatched by `EventLoopEmitterService`, registered through `HandlesRegister`. Implement these rather than adding per-object `Update()`.
- **Math:** `Ara3D` types vs `UnityEngine` vectors both appear; check which one a file uses before mixing, and keep orbital math in double precision where the existing code does.
- **ECS:** Leopotam `ecs` (classic, not Unity DOTS) for vehicles/engines.

## Conventions

- Match the surrounding code: namespaces mirror folders (`Orbital.Core.Simulation`), private fields `_camelCase`, `[SerializeField] private` for inspector fields, Odin attributes where already used.
- No new packages or plugins without asking.
- Don't hand-edit `.unity`, `.prefab`, `.asset` YAML or `.meta` files unless necessary; if you add/move/delete a script, keep its `.meta` file with it. Never regenerate GUIDs.
- Don't commit `Library/`, `Temp/`, `obj/`, `Logs/`, generated `.csproj`/`.sln` changes, or `UserSettings/` noise.
- Changes to `Packages/manifest.json`, `packages-lock.json`, `UserSettings/EditorUserSettings.asset` are currently uncommitted editor side effects; leave them alone unless asked.

## Working in this repo

- Compilation/tests need the Unity Editor (no standalone build script). Don't claim code "compiles" or "works" unless it was verified in the Editor, via the Unity CLI/MCP, or by the user; say what was not verified.
- Tests: `Orbital/Test/` are scene-driven helpers, not Test Framework tests. Real tests live in `Assets/Orbital/Tests/` (NUnit, asmdef `Orbital.Tests`, run from Unity's Test Runner, EditMode) and cover the pure orbit math.
- Running them without the Editor: `dotnet test` in `Tools/OrbitMathTests/` compiles the same sources plus the tests against Unity's managed `UnityEngine.CoreModule.dll`. Unity batch mode (`-runTests`) does not work from this shell: no license access token. Only engine-free code can go into that harness; keep orbit math free of `Debug.*`, `Quaternion` and other native calls.
- Orbit conventions (tested): frame Forward = periapsis, Up = normal = position x velocity, motion Forward -> Right; `H` = position x velocity; mean anomaly `M = n*(t - Epoch) + MeanAnomalyAtEpoch`; semi-major axis is negative for hyperbolas; parabolic (e = 1) and radial (zero angular momentum) orbits are unsupported.
- The shell's file-writing path has been seen to turn the two characters backslash-n inside string constants into real line breaks. After writing C# with such literals, grep the file to check them.
- Git: commit only when asked. Commit messages follow short lowercase style seen in history (e.g. `newton method to entry calculation, fixes for nodes`).
- Windows host: use PowerShell/Git Bash-compatible commands and forward slashes in Bash.
