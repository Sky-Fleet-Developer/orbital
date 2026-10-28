# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

Orbital: a Unity experiment in interplanetary travel mechanics, built on the two-body problem (Keplerian orbits, orbit transitions/entry calculation via Newton's method, path/node planning). See `README.md`.

- Unity **6000.2.5f1** (`ProjectSettings/ProjectVersion.txt`; older notes saying 2021.3.2f1 were stale), URP 17, C#, Windows dev environment. `activeInputHandler` is 1 (new Input System only): legacy `UnityEngine.Input` is not usable, new input code must use `com.unity.inputsystem`.
## Product direction

Goal: a prototype of an **idle/incremental space game** built on the orbital mechanics.

- The player flies a spacecraft to reach other bodies. Like KSP, but **no player time acceleration**, plus a **social layer**: players see each other's running craft and chat by text over a "radio".
- Achievements are tied to locations; more remote = more valuable.
- Long-term control is an immersive cockpit simulator. **Not now** — the prototype uses a greybox UI.
- The prototype must still include **interaction with a planet's surface** (landing/launch/surface state), not only orbits.
- Current phase: fine-tune the orbital mechanics and test what they can do (accuracy, stability, transitions between spheres of influence, long-run determinism). Multiplayer/network design comes later but keep simulation state serializable and deterministic where practical, since craft must be visible to other players and survive real-time (no warp) runs.

Testing today is manual (logs, `Debug.DrawLine`). Improving this is in scope: prefer pure-C# orbital math that can be covered by EditMode tests (`com.unity.test-framework`) over scene-only checks, and add numeric assertions (energy, angular momentum, round-trip anomaly conversions) alongside visual debug.

## Status

- Branch `master` is main; current work is on `online`. The experimental SQLite world storage was removed (no `DB/`, no `WorldSqlDataAdapter`); the world is a JSON tree (`TreeContainer`).
- Gameplay prototype with an authoritative server is planned and logged stage by stage in `Docs/GameplayPrototype.md` (requirements: `GameplayRequest.txt`). Read both before working on gameplay, server or map; start with the section "Состояние дел" in `Docs/GameplayPrototype.md` (stage table, work queue, open decisions). Keep the stage log in that doc current when you finish a piece of work. Working mode so far: a coordinator hands each stage to an executor agent, then accepts it independently (re-runs the tests, reads the diff); commits only when the user asks. Stage 2 detail: `Docs/Stage2-Plan.md`.
- The draft authoritative server lives **outside this repo**: `E:\Projects\orbital-server\` (`OrbitServer/`, `OrbitServer.Tests/`, `README.md`). It links engine-free sources from `Assets/Orbital/Core` and `Assets/Plugins/Ara3D` (path `..\..\orbital`, override with `-p:OrbitalRepo=`), so a change to those files can break the server build; run its tests too.

## Layout

Own code lives in `Assets/Orbital/`. Everything under `Assets/Plugins/` is third-party or vendored (Zenject, Odin Inspector, Ara3D, NavMeshComponents, UnityWinForms, Sqlite, LocalPool, Patterns, ScriptableObjectContainer) — do not edit or refactor it unless explicitly asked.

| Path | Purpose |
|---|---|
| `Orbital/Core/` | Simulation core (asmdef `Core`): `World`, `TreeContainer`, time/event loop, trajectory system, orbit navigation (`PathBuilder`, `OrbitTransitionUtility`), serialization |
| `Orbital/Core/TrajectorySystem/` | Celestial bodies, static/dynamic orbits, single-center and double-system branches, mass utilities |
| `Orbital/Core/Simulation/` | `IterativeSimulation`, `RuntimeOrbit`, trajectory containers |
| `Orbital/Core/Serialization/` | JSON serialization (`ISerializer`, `JsonPerformance`, vector/matrix converters, `SerializedData`) |
| `Orbital/Core/Model/` | `WorldModel`: runtime model built from the deserialized tree |
| `Orbital/Core/Flight/` | Engine-free flight core (blittable structs + static pure functions, double precision): engine/fuel, angular dynamics, RCS, gyrodyne, input mapping, surface contact, `ThrustSegmentRecorder`, batch API. No Unity API here; shared with the server via `Compile Include`. Designed to move to Jobs/Burst/GPU later |
| `Orbital/Technology/` | ECS layer (Leopotam `ecs`): components, systems (jet force, input, vehicle creation), `EcsAdapter` |
| `Orbital/View/` | Rendering of orbits/bodies/map (`MapView`, `*View`, `*ViewUpdater`, settings) |
| `Orbital/Player/` | `PlayerModel`, `PlayerMotor`, `Session` |
| `Orbital/WorldEditor/` | Editor-side state machine for building the world tree |
| `Orbital/Test/` | Ad-hoc test MonoBehaviours (Database, Forms, Path, Trajectory) |
| `Assets/LineRendering/` | Line/circle shaders used for orbit drawing |
| Scenes | `Assets/Main.unity`, `Assets/SimulationScene.unity` |

Assemblies: `Core` (`Orbital.Core.asmdef`) and `Orbital` (`Orbital.asmdef`, depends on Core). Core must not depend on the `Orbital` assembly. References are by GUID; when adding a dependency, edit the asmdef rather than relying on the generated `.csproj`.

## Architecture notes

- **DI:** Zenject. Services (`World`, `TimeService`, `EventLoopEmitterService`, `ComponentsRegistrationService`) are assembled on `sceneLoaded` by `Core/Bootstrapper.cs` (scene sub-container, `IBindMe`/`IInstaller`/`ISceneReady`/`IViewInitializer`; `Bootstrapper.InstallObject` wires runtime-created objects). There are no `GameSceneInstaller`/`PlayerInstaller` classes. Prefer `[Inject]` over singletons/`FindObjectOfType`.
- **World:** the `World` component owns a `TreeContainer` (celestial bodies with static orbits, stored as JSON text and loaded with `JsonPerformance`), builds a `WorldModel` from it and binds it to the scene via `WorldSceneBinder`. There is no database.
- **Sphere of influence:** the decided rule is the Laplace sphere, `r = a * (m / M)^(2/5)` (`a` = semi-major axis of the body's orbit, `m`, `M` = masses of body and parent; the root body is unbounded), used in Core, the server and the client. The single implementation is `TrajectoryUtility.GetSphereOfInfluenceRadius(a, bodyNu, parentNu)` (engine-free, shared with the server) with the wrapper `MassUtility.GetSphereOfInfluenceRadius(IStaticBody)`; the old `GetGravityRadius` is gone. The Core callers (`WorldModel`, `OrbitTransitionUtility`, `PathTest`) were migrated; the user confirmed in the Editor that the project compiles and the world runs.
- **Body data (decided, migration pending, stage 1.5):** a body's radius belongs to the model (collision depends on it), not to the view. `BodiesTable` (`Assets/_game/ViewSettings/BodiesTable.asset`, `BodyViewTable`) already also stores rotation and is to become the source of all body data except position, split into minimal facets. Today `IStaticBody.Radius` is still a stub (`=> 1`) and the real radius is only in `AppearanceFacet.radiusMeters`, so the `R*k` minimum of the sphere of influence only works on the server. Configs (flight, fuel types, craft specs, start points) are JSON shared between client and server.
- **Flight physics:** own double-precision physics (no PhysX) in `Core/Flight`; contact goes through a surface abstraction (`SurfaceKind`, only a sphere now, terrain later); keep the hot path free of classes/interfaces/allocations so it can move to Jobs/Burst/GPU.
- **Player and craft:** one player controls exactly one craft (currently inseparable, 1 account = 1 craft, the callsign is the account nickname). A crash above the landing speed limit destroys the craft and returns the player to the start point. Keep account and craft as separate entities in data models: radio-controlled drones may come later.
- **Planet rotation:** planets rotate (period, axis tilt); a craft on a surface is stored as latitude/longitude in the planet frame and inherits surface velocity on launch. Rotation angle must be a deterministic function of time, identical on server and client.
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
- Server tests: `dotnet test` in `E:\Projects\orbital-server\OrbitServer.Tests\` (includes real WebSocket tests on a free loopback port). Same Unity-managed-DLL requirement and `-p:UnityManaged=` override as below.
- Running them without the Editor: `dotnet test` in `Tools/OrbitMathTests/` compiles the same sources plus the tests against Unity's managed `UnityEngine.CoreModule.dll`. Unity batch mode (`-runTests`) does not work from this shell: no license access token. Only engine-free code can go into that harness; keep orbit math free of `Debug.*`, `Quaternion` and other native calls.
- Orbit conventions (tested): frame Forward = periapsis, Up = normal = position x velocity, motion Forward -> Right; `H` = position x velocity; mean anomaly `M = n*(t - Epoch) + MeanAnomalyAtEpoch`; semi-major axis is negative for hyperbolas; parabolic (e = 1) and radial (zero angular momentum) orbits are unsupported.
- The shell's file-writing path has been seen to turn the two characters backslash-n inside string constants into real line breaks. After writing C# with such literals, grep the file to check them.
- Git: commit only when asked. Commit messages follow short lowercase style seen in history (e.g. `newton method to entry calculation, fixes for nodes`).
- Windows host: use PowerShell/Git Bash-compatible commands and forward slashes in Bash.
