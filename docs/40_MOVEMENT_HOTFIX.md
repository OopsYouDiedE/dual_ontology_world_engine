# Hotfix 40 — Locomotion & Exploration Integrity

Base: `ae7893e` · Engine: Godot 4.7.2-stable mono · .NET 8

## Reproduction (real machine)

1. Build the C# assembly once: `dotnet build DuelView.sln` or open the project with `--build-solutions`.
2. Run the real host: `godot --path . res://tools/verification/MovementRegression.tscn`
3. Before this patch, manual input produced `speed_mps=12.04`,
   `animation=walk@1.55`, and ~`+54 game seconds` per wall-clock second.
   Path following used a 0.12 m waypoint radius while the body moved ~0.20 m
   per physics frame, so it orbited the last waypoint and never submitted the
   pending interaction.

## Root causes

- `time_scale` (default `player_speed=60`) leaked into manual walking through
  `sqrt(time_scale)` clamped to 5. Walking speed became `1.9 × 5 ≈ 12 m/s`,
  while the walk clip was capped at `1.55×`. This is the observed skating.
- Animation was driven by intended velocity (`velocity.length()` including
  gravity), not by measured horizontal displacement. A body pressed against a
  wall kept walking on the spot.
- Path arrival tolerance was smaller than a single frame of travel, and the
  arrival/interaction submission depended on `_manual_arrived` surviving a
  frame. Overshoot therefore deadlocked exploration.
- `observation()` assumed `settings.rooms` existed. A failed room load made the
  field GUI spam `Invalid access to property 'rooms'` every frame.
- 286 generated prop scripts were byte-identical (38,896 lines).

## Changes

- `scenes/components/characters/ActorMotor3D.gd` is the single manual
  locomotion authority: camera-relative normalized input, real-time walk speed,
  measured-displacement animation, waypoint lookahead, detour on stall, explicit
  arrival/blocked callbacks.
- `CampaignRoomHost.gd` delegates manual movement to the motor. Fast-forward
  only advances idle simulation. Real-time physical clock advances at 1× for a
  human-controlled detective. Paths created by clicks and by actions are handed
  to the motor. Camera Q/E rotation added. Observation is failure-safe.
- `StoryBody3D.gd` maps `walk` speed scale to a 1.8 m/s reference, clamped
  0.60–1.35. `step_towards` also reports measured displacement.
- `assets/data/presentation/campaign_rooms.dv`: `walking_speed 2.4 → 1.9`,
  `player_speed 60 → 1`. Waiting fast-forward remains available via `set_speed`.
- 286 per-instance scripts collapse into
  `scenes/components/architecture/GeneratedVisualComponent.gd`; every `.tscn`
  keeps its exported instance configuration.

## Acceptance / verification

`tools/verification/MovementRegression.tscn` (headless):

```json
{
  "verdict": "PASS",
  "locomotion": {"distance": true, "speed": true, "clock": true, "animation": true},
  "empty_settings_guard": true,
  "exploration_cross_room": true
}
```

Measured: 1.87 m in 1.0 s, `speed=1.90`, `game_delta=0.98 s`,
`max_anim=1.06`, room transition `R05 → B-P` in 2.32 s, 0 script errors.
Day-1 AI harness continues to advance rooms with 0 script errors.

## Apply

```bash
git apply changes.patch
dotnet build DuelView.sln
godot --headless --path . --editor --build-solutions --quit
godot --headless --path . res://tools/verification/MovementRegression.tscn
```
