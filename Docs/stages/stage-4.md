# Stage 4: flight map, descent instrument, temporary fuel boost

Status: first part done 2026-11-04 by the coordinator without an executor and confirmed by the user in the Editor the same day ("теперь всё работает отлично"), including 4.2. Not done from the plan of stage 4: player-added points and trajectories (4b, see below). The fuel boost is still on. Nothing is committed in either repo.

## 4a Flight map (M or C)
- `Craft/Map/FlightMapView.cs`: a new map of the flight, built only when the world has a body table. The old `MapView` stays for the scene world and the Editor; its camera stays off in flight (`CraftPresenter.OwnMap`).
- Precision: every position is a difference of doubles from the point the camera looks at, scaled so that this point is 100 units from the camera; meshes are rewritten every frame. Same precision from a craft on the ground to the whole system.
- Orbit of a body: sampled from the body's anomaly now, denser near it, the middle sample is the body. The line passes exactly through the body at any zoom.
- Lines: `Resources/Orbital/MapLine.shader`, quads widened in screen space, width in pixels. Markers, labels, window at the cursor, summary panel: `FlightMapOverlay` (IMGUI).
- Controls: wheel zoom, RMB turn, click a body to follow it, F back to the craft. Flight keys keep working on the map.
- Window at the cursor: craft, other crafts, bodies, Pe/Ap/SOI/impact markers, any point of the trajectory (time to it, body, height, speed).
- Settings: `View/Flight/MapViewSettings.cs`, a nested asset of `ViewSettingsContainer` (added by the menu tool in batch mode).

## Forecast (Core, engine-free)
- `Core/Flight/TrajectoryForecast.cs`: patched conics through every SOI change (at most 6 conics), `TrajectoryPlot` (samples in meters relative to the frame body, events), `TrajectoryForecaster` (one worker task, two plots trade places on the main thread, never touches the simulation).
- Frame rule: a conic in the frame body's subtree is the true path relative to that body over time. Any other conic keeps its own shape and is moved to continue the previous one: the true path would be a meaningless curl there. So around the root the picture is exact; **from the craft's frame, after leaving the body, the line is continuous but the later conics are shifted**. For an exact picture of a transfer click the parent body or the Sun.
- `SoiForecast.NextEvent` is now the one event search. **Behaviour change:** an entry into a child's sphere before reaching the ground now counts (`TryNext` used to say nothing for a craft on a falling conic).
- The HUD line `Next SOI` reads the forecast, so its search left the main thread.
- The forecast ignores thrust: during a burn it shows the conic of the current state, refreshed 10 times a second with the map open and twice without.

## 4.1 Descent instrument
- `Core/Flight/LandingAdvisor.cs`: point mass, numerically: the coast to the ground, then a full burn against the velocity relative to the rotating ground from a point of that coast; bisection for the burn that ends at 50 m. About 0.4 ms per call, called 5 times a second.
- HUD panel right of the nav ball, only when the coast ends on the ground: `BURN IN`, `BURN NOW`, `TOO LATE`, `NOT ENOUGH FUEL`; time and speed of impact, burn time and fuel, where a full burn started now stops, how far the nose is off the retrograde.
- **Not counted:** the time to turn the craft and the throttle lag. The plan assumes the nose is already on the retrograde and full throttle at once (X). The 50 m margin is small for this.
- Impact marker on the map.

## Temporary fuel boost
- `fuels.json`: new fuel `test-unlimited` (Isp 45000), `craftspec.json`: both crafts use it. `flightconfig.json`: RCS Isp 20000 (was 200).
- To remove: `"fuel": "standard"` in `craftspec.json`, `rcs.isp` 200; 4 tests hold the temporary numbers, search for `TEMPORARY boost` in `Stage2bTests.cs`, `Stage14Tests.cs`, `Stage15Tests.cs`.
- A craft already living on a server keeps the spec of its snapshot: online take a new callsign for the boost.

## Checks
- OMT 733/733 (23 new in `Stage4Tests.cs`), server 308/308, Unity EditMode 834/834, player builds.
- `Instrument_FlownByTheKernel_*`: descents on the Moon, Mars, Earth flown through `FlightKernel` by the instrument: stops in the air, below 600 m. The attitude is forced in that test, so it does not check the pilot's turn. It found a real fault: the burn started up to 10 km too high because the coast samples were seconds apart; refined between samples.
- Player offline, screenshots `screenshots/stage-4-*.png`: low Earth orbit, window at the cursor, Phobos with a burn (two conics, leaving for Mars), the same around Mars, the Sun's view, falling on the Moon in both views.
- Player online against a local server (separate port, callsigns `Smoke4*`, `orbital-client.json` backed up and restored): map, forecast and boost with `rej 0`, `corr -`; one craft left Phobos for Mars (`@ mars` in OTHERS). The smoke found a fault: a map opened before the server gave the craft stayed on the Sun for good, with the camera inside it; fixed.

## Not checked
- Mouse in a real session: zoom, turn, click to follow, F. The window was checked with a forced cursor (`ORBITAL_MAP_MOUSE`), on the craft's marker only.
- An entry into a body's sphere on the map (Moon, Deimos) in the player: only in tests.
- Markers of other crafts: on the online screenshot they lie on top of each other at that zoom, so not seen apart.
- A real landing by the instrument with hand control.
- Frame rate with all lines on: no slowdown noticed on screenshots, not measured.

## 4b Not done (plan of stage 4)
Adding a point or a trajectory by parameters (orbit + snapshot time + text, saved for the player). It belongs with the exchange of trajectories in stage 5; to do when that starts or when asked.

## 4.2 Folding panels, the mirror of the flight view (2026-11-04, confirmed by the user in the Editor; no batch build, EditMode or server run after it)
- `Craft/HudPanels.cs`: a + / - button on every panel of the HUD and the map's panel, the state in PlayerPrefs. A folded descent panel keeps its big line. A click or the cursor over a panel does not reach the map. F1 folds the help. Hook `ORBITAL_HUD_COLLAPSE=flight,help,...` (this run only).
- The user's controls: airplane picture, nose forward: W nose down, E nose right, D roll right. The kernel, the keys and the nav ball already had this; **the flight view and the map were a mirror image**: `CraftPresenter.ToVector` copied right-handed numbers into left-handed Unity, so E and D went the other way on the screen. Diagnosis from the code; it was not seen in the player before the change.
- Fix in the view only, keys, kernel, server, nav ball untouched: `ToVector` / `ToQuaternion` flip Z, `FromVector` added, sun direction through `ToVector`, ground patch winding reversed, camera behind the craft moved to local +Z and turned 180 degrees, `MarkerZ` moved to the same body side. So planets now turn the true way on the screen. Prefabs with a marked side would be mirrored too.
- `Stage4Tests.Keys_OnTheScreen_ArePilotsKeys_LikeAnAirplane` writes this convention down in numbers; it checks the kernel against the formula of the view, not real pixels.
- To check after building: hooks `ORBITAL_AUTOKEYS=R,E`, `ORBITAL_VIEW_HORIZON=1`, ground patch and pad near the surface, lit sides of bodies, V camera, map.

## The lead to the Moon (`Stage4Tests.MoonLead_TheTable`)
400 km circular orbit in the Moon's plane, prograde burn, forecast of entering the Moon's sphere. Moon ahead of the craft along the motion at the burn: minimal burn to the Moon's distance 88..144 deg (middle about 116, the Hohmann figure 115), +100 m/s 125..149, +500 m/s 121..142, +2000 m/s 104..124. Moon's period 27.45 d. The plane of `earth-orbit` (equator) is 22 deg from the Moon's orbit at t = 0.

## Smoke hooks
`ORBITAL_MAP=1`, `ORBITAL_MAP_FOCUS=<body id>`, `ORBITAL_MAP_DISTANCE=<m>`, `ORBITAL_MAP_MOUSE=x,y`, `ORBITAL_SPAWN_HEIGHT=<m>` (with `ORBITAL_SPAWN_BODY`).

## Manual check (user)
1. `earth-orbit`: M, wheel, RMB, click the Moon, F. Burn with the map open (X, Z): the line follows; look for `Enters Moon` and the ring `Moon then`.
2. `phobos-base`: lift off, M: green conic, then orange around Mars. Click Mars for the true picture, fly to Deimos with the time slider.
3. Landing: from orbit brake until the panel appears. Turn to the retrograde beforehand with SAS, X at `BURN NOW`, finish by throttle under the limit of 5 m/s.
4. Online with a new callsign: the same, watch `rej` and `corr` on the Net line.
