# IM ULTRAKILLING IT

Experimental single-player heuristic bot for ULTRAKILL and BepInEx 5.

## Controls

- F8: enable/disable the bot
- F9: pause/resume
- F10: restart the mission
- F7: toggle the debug overlay

Disabling the bot stops all movement, aiming, firing, and weapon switching by the plugin on the next frame. The game's own input components are never disabled.

## Current vertical slice

The bot finds the highest-scoring visible living enemy, aims through `CameraController`, moves through `NewMovement`, jumps over short obstructions, fires the current weapon, rotates populated weapon slots, detects stalls, displays live time/kills/style targets, and can conservatively restart a clearly failed P-rank attempt. Automatic restarts are off by default.

This prototype is not yet a general level-completion bot. It has no authored route to exits, traversal objectives, skulls, or switches, and it cannot yet guarantee a P-rank on any level.

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

Launch ULTRAKILL normally, open a level, and press F8. Confirm the overlay selects a visible enemy and the BepInEx console contains `IM ULTRAKILLING IT loaded`. Press F8 before menus, elevators, or taking manual control. After closing the game, inspect `BepInEx/LogOutput.log` for exceptions from `IMULTRAKILLINGIT`.

## Main blockers to a reliable first P-rank

- Per-level routes and objective/exit metadata
- Hazard-aware path planning and fall recovery
- Encounter-trigger completeness and unreachable-enemy routes
- Cooldown-aware alternate-fire, parry, projectile-boost, and combo policies
- Validation of rank thresholds and restart timing across every difficulty
