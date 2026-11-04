# Stages 3.6b, 3.4b, 3.7: settings, SOI hint, test start points

Status: code done 2026-11-04 by the coordinator without an executor. Waits for the user's check in the Editor (the manual flight scenario at the end). Stage 3 closes after it.

## 3.6b Network and craft settings off the spawner
- `Orbital/Craft/Settings/NetworkSettings.cs` (mode, serverUrl, callsign; the enum `CraftNetMode` moved here) and `CraftSettings.cs` (craftId, vehiclePrefab, greyboxMaterialTemplate, groundSupport). Assets: `Assets/_game/Settings/NetworkSettings.asset`, `CraftSettings.asset`, values copied from the scene.
- They are standalone assets, not in `ViewSettingsContainer`: they are not view settings. `CraftSpawner` holds two references; spawn place, shared configs, test tools stay on it.
- `Main.unity`: the old lines replaced by two references, text edit. The online/offline switch no longer dirties the scene.
- `maxLandingSpeed`: top level of `flightconfig.json`, read by `GameConfig` and by `ServerConfig.Load` (an explicit server setting wins). Not in the `FlightConfig` struct: that struct is stored in craft snapshots.
- `BuildScript` fails the build without the two assets or without the greybox material; checked: it did fail before the assets existed.
- HUD online shows the account's callsign, not the asset's.

## 3.4b SOI hint in the HUD (temporary)
- `Core/Flight/SoiForecast.TryNext`: the next change of body while coasting, from the current state, with the `SoiRule` thresholds, on `StaticOrbit`/`OrbitCrossing`. No answer for a nearly radial trajectory or a craft falling into its body.
- HUD line `Next SOI: Mars in 439 s (coasting)`, searched about once a second, in both modes.
- **Differs from the plan:** the plan had the server send `nextTransition` in the state and the client compute only offline. One client path instead: no protocol change for a hint that will be replaced, and it stays fresh after a burn the server has not got yet. The server's own `NextTransition` is not used by the client.

## 3.7 Test start points
- `startpoints.json`: `earth-orbit` (kind `space`, 400 km, craft `test-long-range`), `moon-equator`, `phobos-base`, `deimos-base`; the default stays `earth-equator`.
- `craftspec.json`: `test-long-range` (5 t dry, 100 t fuel, 2 MN): about 13.4 km/s, TWR 1.9 on Earth.
- Start point format: `altitudeMeters`, `inclinationDeg`, `phaseDeg`, `craftId` (a craft bound to a point, so no craft choice in the protocol). Server: a spawn on a `space` point uses the same `SpawnBuilder.CircularOrbit` as the client. A `space` point without an altitude is refused with `start_point_no_orbit` (was `start_point_kind_not_supported`; 2 old tests changed).
- `CraftPresenter`: `StartPad[]`, the nearest pad of the current body is drawn; one set of objects moves between pads. Orbit spawns no longer put a pad under the first position.
- Wrecks on the current body: `GhostPresenter.PresentWrecks` already filtered by body, no change.
- Choosing a start point: `startPointId` on the spawner or env `ORBITAL_START_POINT`. No selection in the HUD.

## Bug found by the online smoke and fixed (server, from stage 3.3 or earlier)
A long gyro-only burn from Phobos, then a coast across the SOI border: `rej 1 (attitude_change_impossible)` and `corr hard (resync)`.
- Cause: `Propagator.ApplyTransition` replaced the attitude record with `AttitudeAt`. Under SAS that is the "holding" view (rate 0, old orientation). A saturated gyro means the craft really turns, so `I*omega`, about 6000 N*m*s, vanished from the record and the next client report looked impossible.
- The old e2e tests did not see it: they fly with RCS on, so the gyro does not saturate. Control: the same flight from low Earth orbit (no body change) had no rejection.
- Fix: a transition keeps the record as it is; the inertial axes do not change with the body. Regression tests `GyroOnly_SaturatedBurn_ThenSoiExit_NoRejection`, `GyroOnly_SaturatedBurn_NoSoiChange_NoRejection` in `Stage3NetTests.cs`.
- Noticed, not fixed: the HUD counter `corrections hard` does not count a resync (`CraftNetLink` shows `hard (resync)`, `Link.Hard` stays 0), so the Net line is the one to read.

## Checks
- OMT 710/710, server 308/308, Unity EditMode 834/834, player builds.
- Player offline: `earth-orbit` (400 km, 100 t fuel), `phobos-base` with a burn (hint 439 s, matches 5.9 km at 13.5 m/s), `deimos-base` started without errors (its screenshot not looked at).
- Player online against a local server (separate port, callsigns `Smoke37*`, `orbital-client.json` backed up and restored): `phobos-base` and `earth-orbit` spawn; 12 s burn from Phobos and SOI exit `phobos -> mars` with the fixed server: no rejection or resync in the log. Screenshots `screenshots/stage-3.7-*.png`.

## Not checked
- The full scenario Phobos -> Mars SOI -> Deimos, and any entry into a body's SOI in a real flight: online only the exit was flown. The entry is covered by the forecast test against the rule and by older server tests.
- The flight to the Moon with the test craft; landing on any new point.
- Lift-off of `test-long-range` from Earth: only computed. It starts only in orbit now.
- Offline respawn or online spawn after a crash on another body than the first start.

## Manual check (user)
Settings: `Assets/_game/Settings/` (mode Offline for 1-2). `startPointId` on `CraftSpawner`.
1. `phobos-base`: lift off, hint counts down, `@ Mars`, come back or fly on to Deimos with the time slider. `deimos-base` is a pad to land on.
2. `earth-orbit`: burn prograde, the hint appears when the orbit reaches the Moon's sphere.
3. Online with a new callsign (an account with a live craft keeps it): the same, watch the Net line for `rej` and `corr`.
