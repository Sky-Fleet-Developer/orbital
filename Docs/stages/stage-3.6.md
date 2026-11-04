# Stage 3.6. Unity: two cameras, impostor bodies, points, the Sun's light

Status: code done 2026-11-04 by the coordinator without an executor (user's request). Checked by the user in the Editor 2026-11-04: "Всё смотрится отлично"; the user removed the old `Directional Light` from `Main`. Plan: `Docs/Stage3-Plan.md`, sections 3.4, 3.4a, 3.6.

## What was done
- `Craft/ScaledSpaceView.cs` (new): the sky of a camera. Each real body of `BodyFrameTable` has a sphere and a point; `CelestialView.Fill` gives direction, angular size, flux, mode. The view takes a camera, a layer and an eye, knows nothing of the flight: a second instance with a `RenderTexture` is a telescope. `BodyLook.Read` takes tint, emission, albedo from `AppearanceFacet`.
- `Craft/Resources/Orbital/ScaledBody.shader`, `ScaledPoint.shader` (new, in Resources so a player includes them).
- `CraftPresenter`: URP stack (Base `SkyCamera` on layer 30, the old camera becomes Overlay). The 3.4 sphere pool is replaced by one `NearPlanet` sphere shown by `NearPlanetMode`; Local far = max(`localMinFar` 200 km, `LocalFar`); `FlightSun` follows the real Sun. The ground patch takes the body's tint. Without a body table (`source = Scene`) the old single-camera path stays.
- `Core/Flight/CelestialView.FarToNearOrder` (engine-free, `Stage3p6Tests`, 4 tests).
- `AppearanceFacet.albedo` and its values through `orbital-server/Tools/migrate_bodies_albedo_3_6.py` (31 lines added, idempotent). Values are from memory, not checked against a source; exposure is logarithmic, so tens of percent do not show.
- `World.BodyData`; `Orbital.asmdef` got the URP runtime reference (the package is already in the project, no new package).
- Env hooks: `ORBITAL_SPAWN_ALT` (with `ORBITAL_SPAWN_BODY`, circular orbit), `ORBITAL_VIEW_BODY` (camera looks at a body), `ORBITAL_VIEW_FOV`. Automated runs set `ORBITAL_NET_MODE` explicitly (the scene's `netMode` is the user's choice, it changes).

## Decisions that differ from the plan
1. **No depth in Scaled, painter's order by real distance** (material `renderQueue`). At one scaled distance depth is wrong: Io in front of Jupiter would lose to Jupiter's bigger impostor, a Moon-Sun eclipse would z-fight. The plan had depth `near 1 .. far 2L`.
2. **Each disk is lit from its own direction to the Sun** by a small shader, not by the one directional light. With the eye's direction a far planet gets a wrong phase (Jupiter at quadrature would look half lit). The directional light stays for Local objects.
3. `minPixels` 3 and point size 4 px in the settings (plan: about 2), so a point and the thinnest disk are of a similar size. The Core default stays 2.
4. The near sphere uses the same shader and a 128x64 sphere mesh, so the hand-over at the threshold does not show. Other bodies are never in Local now.

A moon cannot cover the near planet wrongly: a body that covers the disk must be lower than the craft, and the near mode ends below the moons (Mars exit 4040 km, Phobos at 6000 km; Jupiter, Saturn, Uranus, Neptune are never near, their radius exceeds the far budget).

## Checks
- OMT 704/704, server 301/301, Unity EditMode 834/834, player build OK.
- Player runs, screenshots in `Docs/stages/screenshots/stage-3.6-*.png`, no errors in the logs:
  - `pad`: Earth pad, ground, points in the sky.
  - `e50000`: Earth from 50 000 km is a disk on its night side, a thin crescent toward the Sun.
  - `sun`: same orbit, the Sun is a disk of about 6 px (expected 6.4) right under the crescent.
  - `marsnarrow`: vertical FOV 0.02 deg: Mars is a disk with a gibbous phase, two faint points beside it (taken for Phobos and Deimos, not identified). The point/disk rule by camera pixels works.
  - `phobosmars`: Mars from the Phobos surface is a big disk (it was cut by far in 3.4).
  - `near479`, `far482`: on both sides of the Earth enter threshold (480 km): the same picture, the seam does not show.
- Online without a server (Connecting screen): renders, no exceptions.

## Not checked
- Only single screenshots: no flicker check in motion, no time-lapse, no real flight through the thresholds or an SOI change. Needs the Editor.
- Day side of the near planet together with the patch: the patch is URP Lit, the sphere is the new shader, their brightness may differ (the shots near the threshold fell on the night side). The Earth ground is now blue (tint).
- Online flight with a craft under the new view.
- Painter's order in a real transit: the order is covered by a test, not seen on screen.
- HDR (user's decision 2): not tried. `maxPointBrightness` above 1 does nothing visible without bloom, no Volume was added.

## Known limits
- The Sun's light does not weaken with distance for Local objects and disks. No eclipses or shadows from bodies (by plan).
- Brightness may jump at the point/disk switch: a disk is lit by tint, a point by log flux.
- The limb of a very big disk shows faint facets (128 segments).
- `IStarField` is not drawn: the background is a solid color until a generator exists.

## Editor check (user)
Run offline (`netMode` Offline or env `ORBITAL_NET_MODE=offline`):
1. Pad: dark sky with points; ground and poles look OK; compare day and night (time slider).
2. Climb with the time slider through 480-960 km on Earth: the planet does not jump or flicker; look at the patch at 12 km.
3. RMB camera rotate: bodies stay put in the sky, no shaking.
4. `ORBITAL_SPAWN_BODY=Phobos`: Mars visible; fly out of the SOI, Phobos stays visible as a disk.
5. Are the points bright enough? Tune `SkySettings` (see below): `minPointBrightness`, `fluxFloor`, `minDiskPixels`.

## Settings moved out of the spawner (user's request, 2026-11-04)
The user asked to take the view and lighting settings off `CraftSpawner` and put them in intuitive, extensible places.

**Where they live now:** `Assets/_game/ViewSettings/ViewSettingsContainer.asset`, nested assets next to the map's settings (the project's existing pattern). Classes in `Orbital/View/Flight/`:
| Asset | Topic |
|---|---|
| `FlightCameraSettings` | layer, field of view, clip planes, zoom limits, mouse |
| `SkySettings` | layer, background, scaled distance, disk/point threshold, exposure |
| `FlightLightingSettings` | sun color and intensity, day and night brightness of the bodies |
| `LocationViewSettings` | ground patch, pad and poles |

- `FlightViewSettings` gathers them from the container. A missing section gets default values and a warning; the player build fails on it (`BuildScript.ValidateViewSettings`).
- **New topic:** a ScriptableObject class in that folder, one property in `FlightViewSettings`, menu `Orbital/Add Missing Flight View Settings` (`FlightViewSettingsTool`, idempotent, finds sections by the properties).
- `CraftSpawner` holds one reference `viewSettings`. `Main.unity`: the `view:` block replaced by that one line, text edit; old values equal the class defaults, nothing was lost.
- `SunLightRig` (new) owns the light: the directional light and the shader globals `_OrbitalBodyDay`, `_OrbitalBodyNight` (hard-coded before: intensity 1.1 in the presenter, 0.015 and 1 in the shader). The direction comes from the simulation, the scene needs no light of its own.
- `ScaledSpaceView` takes `SkySettings` instead of a list of numbers and sets up its camera itself. Almost everything is read each frame, so it should be tunable in Play mode; tooltips name what is read only at the start.
- The class is not called `LightingSettings`: that name clashes with `UnityEngine.LightingSettings`.
- A test of the container could not go to `Assets/Orbital/Tests`: `Orbital.Tests` does not see the `Orbital` assembly and `OrbitMathTests` compiles that folder too. So the check is in the build validation.

**Checks:** OMT 704/704, server 301/301, EditMode 834/834, player builds. Wiring proof in a player: with `background` red, `nightBrightness` 0.25, `fieldOfView` 30 in the asset the picture changed in all three, the Earth disk doubled; asset restored. With the real values the view from 50 000 km looks the same as before the move.

**Not checked:** tuning during Play mode in the Editor (the player has no inspector); the map view (C) after the user removed the scene light; the Editor's display of the nested assets.

**Left on the spawner, the user decides:** network (`netMode`, `serverUrl`), craft (`vehiclePrefab`, `greyboxMaterialTemplate`, `craftId`, `callsign`, `maxLandingSpeed`, `groundSupport`), config TextAssets, spawn place, test tools. `maxLandingSpeed` repeats a server value. The scene's ambient light stays in the scene's Lighting window (not in `FlightLightingSettings`).
