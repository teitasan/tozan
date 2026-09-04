# Unity Platformer Sample — Provenance

Vendored copy under `Assets/ThirdParty/UnityPlatformer/`.

## Upstream

| Field | Value |
|-------|-------|
| Package | `com.unity.charactercontroller` |
| Version (project lock) | **1.4.2** (`Packages/manifest.json`) |
| Sample | Platformer (ECS + Unity Physics kinematic character) |
| Public repository | [Unity-Technologies/CharacterControllerSamples](https://github.com/Unity-Technologies/CharacterControllerSamples) |
| License | Unity Companion License — see `LICENSE.md` in this folder |

## Local revision

This tree is a **project-local vendor snapshot**, not a git submodule. No upstream commit hash is recorded in-repo. Tozan changes are limited to:

- `Scripts/Tozan/` — PlayMode test harness
- `Scripts/Character/States/ClimbingState.cs` — optional geometry-mode detection path
- `Scripts/Character/States/LedgeStandingUpState.cs` — collision-safe mantle
- `Scripts/Character/States/LedgeGrabState.cs` — mantle target wiring
- `Scripts/Character/PlatformerCharacterProcessor.cs` — optional `TozanPlatformerGeometryConfig` passthrough
- `Scripts/Character/PlatformerCharacterSystems.cs` — geometry config lookup

Official sample scenes/prefabs keep tag-based climbing where authored. NaturalRockSandbox uses `TozanPlatformerGeometryAuthoring` (`GeometryOnly`) on the Platformer character prefab.

NaturalRockSandbox display mesh: **Erika** (`Assets/Characters/Erika/ErikaCharacterMesh.prefab`), wired by `TozanErikaPlatformerSetup`. The generated wrapper keeps `Animator` at the prefab root for the ECS hybrid link and lifts the imported rig so its renderer feet align to the `MeshRoot` ground. Official `CharacterMesh.prefab` (ProtoCharacter) remains in-tree as rollback reference. The `Freehang Climb` source clip is copied locally with the DPS `EnableController` event removed; no DPS Player or Locomotion runtime is used by the ECS player.

## Related Tozan runtime (not upstream)

| Path | Role |
|------|------|
| `Assets/Scripts/Tozan/TozanPlatformerGeometryConfig.cs` | Geometry vs tag detection mode |
| `Assets/Scripts/Tozan/TozanSurfaceProbe.cs` | Markerless surface classification |
| `Assets/Scripts/Tozan/TozanMantleUtility.cs` | Mantle preflight / sweep |
| `Assets/Scripts/Tozan/TozanPlatformerGeometryAuthoring.cs` | ECS bake for NaturalRock player |
