# IM ULTRAKILLING IT

Experimental single-player heuristic bot for ULTRAKILL and BepInEx 5.

## Controls

- F1: enable/disable the bot
- F2: pause/resume while enabled; start/stop route recording while disabled
- F3: restart the mission
- F4: toggle the debug overlay

### Record a level route

1. Leave the bot disabled and press F2 at the level start.
2. Play normally, including jumps and shooting breakable glass.
3. Press F2 again to save, then restart the level.
4. Press F1 to replay it. Combat temporarily takes control and the route resumes afterward.

Disabling the bot stops all movement, aiming, firing, and weapon switching by the plugin on the next frame. The game's own input components are never disabled.

## Navigation milestone 1

Navigation is split into world scanning, semantic objectives, NavMesh route planning, waypoint movement, hazard checks, stuck detection, recovery, and debugging. On activation it inspects the current scene and current player state, waits for a safe landing when enabled mid-air, and selects reachable incomplete checkpoints, exits, or unlocked unvisited doors. Routes use complete Unity NavMesh paths; when no semantic objective is reachable, the bot chooses a reachable unexplored frontier. Combat suspends navigation, and navigation rescans and replans from the player's current position afterward.

Movement follows local path corners with smoothed camera turning, capsule clearance checks, conservative drop checks, and configurable speed. Repeated failures escalate through alternate recovery directions and eventually reject that objective for the current run. The overlay and optional world-space debug lines expose the current objective, route corner, movement state, grounded state, and recovery level.

The existing combat behavior remains intact: the bot scores visible enemies, aims through `CameraController`, moves through `NewMovement`, fires known weapons, rotates populated weapon slots, and can conservatively restart a clearly failed P-rank attempt. Automatic restarts remain off by default.

This milestone does not yet execute switches, keys, elevators, moving platforms, scripted interactions, deliberate drops, or generated jump/dash links. Those real game component types are discovered and counted, but require specialized traversal adapters in later milestones.

## Build

From the game directory:

```powershell
$env:DOTNET_CLI_HOME=(Resolve-Path '.\.tools').Path
$env:NUGET_PACKAGES=(Resolve-Path '.\.tools').Path+'\packages'
.\.tools\dotnet\dotnet.exe build .\IMULTRAKILLINGIT\IMULTRAKILLINGIT.csproj -c Release
.\.tools\dotnet\dotnet.exe run --project .\IMULTRAKILLINGIT\tests\DecisionLogicCheck.csproj -c Release
```

The release build copies `IMULTRAKILLINGIT.dll` to `BepInEx/plugins/IMULTRAKILLINGIT/`.

When the repository is cloned outside the game directory, pass the game path explicitly:

```powershell
.\.tools\dotnet\dotnet.exe build -c Release -p:UltrakillDirectory="C:\path\to\ULTRAKILL"
```

## Test in game

Launch ULTRAKILL normally, open a level, and press F1. Confirm the overlay selects a visible enemy and the BepInEx console contains `IM ULTRAKILLING IT loaded`. Press F1 before menus, elevators, or taking manual control. After closing the game, inspect `BepInEx/LogOutput.log` for exceptions from `IMULTRAKILLINGIT`.

## Later milestones

- Switch/key/arena dependency adapters and dynamic progression graph edges
- Validated jump/drop traversal edges and fall recovery
- Elevator and moving-platform executors
- Runtime traversal-edge reliability and risk-weighted route costs
- Encounter-trigger completeness and unreachable-enemy routes
- Cooldown-aware alternate-fire, parry, projectile-boost, and combo policies
- Validation of rank thresholds and restart timing across every difficulty
