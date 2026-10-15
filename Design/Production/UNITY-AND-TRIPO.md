# Unity and Tripo production plan

> Superseded architecture and scope. Use [Fleet Duel V2 production](FLEET-DUEL-PRODUCTION-V2.md) and [Photon networking](PHOTON-NETWORKING.md). The asset cleanup/import guidance below remains a reference; the old single-player schedule and controls do not apply.

## Unity baseline

Use Unity 6.3 LTS with the Universal Render Pipeline as the proposed stable project baseline. Pin a tested patch version when creating the project rather than guessing a patch number here. Unity lists 6.3 LTS support through December 2027; see [Unity release support](https://unity.com/releases/unity-6/support). Confirm target-store SDK requirements again when preparing release builds.

Use C#, the Input System for touch and editor keyboard input, and uGUI for this compact HUD. Begin with one scene for Boot, one Dock and one Voyage; shared services survive scene transitions deliberately. Avoid a complex dependency framework for the first slice. Choose package versions compatible with the pinned Editor through Package Manager.

The workspace currently contains design assets, not a Unity project. This phase creates no package installation, playable scenes, store integration or build verification.

## Runtime organization

| Area | Responsibility |
| --- | --- |
| ShipMotor | Planar acceleration, steering, terrain avoidance and Gust; visual bobbing isolated on child |
| BroadsideWeapon | Independent left/right cooldown, side-arc targeting, line-of-sight test, cannon sockets |
| ProjectilePool / Damageable | Reuse projectiles; hit ownership; single damage application and despawn |
| EnemyBrain | Approach, turn-to-broadside, telegraph, fire and recovery states; rammer alternative |
| EncounterDirector | Seeded encounter recipes, spawn safety, enemy caps and captain timing |
| VoyageState | Start, combat, choice, success, sunk and withdrawal; pauses and transitions |
| UpgradeSystem | Valid offers and bounded modifiers, reset between voyages |
| RewardService / SaveService | Commit reward once, versioned saves, migration and recovery |
| CameraRig / HudPresenter | Safe composition, touch controls, warning presentation and cooldown display |
| AdsAdapter / PurchaseAdapter / AnalyticsAdapter | Swappable interfaces; mock in prototype, real services only after core loop works |

Use ScriptableObjects for ShipDefinition, WeaponDefinition, EnemyDefinition, UpgradeDefinition, EncounterRecipe and EconomyConfig. Avoid copying tuned values into multiple prefabs. Keep runtime mutable HP and cooldowns out of shared ScriptableObjects. Save file contains version, owned cosmetics, permanent upgrade levels, mission unlocks and committed reward IDs. Write atomically with a backup. Local saves are sufficient for initial offline progress; paid entitlements need platform verification/restoration once purchases exist.

Ship movement uses a kinematic Rigidbody constrained to the XZ plane, with fixed-step movement and simple compound hull colliders. Ray/sphere sweeps guard against fast projectiles passing through targets. Sails have no colliders. Do not use full ocean buoyancy. Use collision layers for ships, rocks, projectiles and pickups. Avoid friendly fire for initial playtests. AI can use local steering and authored navigable lanes instead of ocean NavMesh complexity.

## Folder and prefab conventions

Future Unity content lives under Assets/_Game with Art/Ships, Art/Environment, Materials, VFX, Audio, Prefabs, Scenes, Scripts, Data and UI. Keep these Design documents outside Assets so huge reference images do not enter builds.

Ship prefab root has ShipMotor, Damageable and normalized world scale. Children: VisualRoot, HullCollider, CannonSockets/Port_01..03, CannonSockets/Starboard_01..03, WakeSocket and HealthAnchor. Use +Z as bow, +Y up, +X starboard. Set the ship root pivot to the waterline center, distinct from any exported bottom-center pivot. Validate axes after export rather than assuming DCC coordinates survive import unchanged.

## Initial performance budgets

Budgets are design targets to profile, not measured results. Name actual test devices before signing off performance: one older 4-GB Android phone and one representative midrange phone, with GPU and OS recorded in build notes.

| Budget | Initial target |
| --- | --- |
| Frame rate | Stable 30 fps on lower-tier test phone; 60 fps option on stronger devices |
| Total visible geometry | Under 150,000 triangles in busiest MVP encounter |
| Rendering batches | Under 120 typical frame, validate in player build |
| Active ships | At most 8 enemies plus player |
| Active cannonballs | At most 64 |
| Materials per ship | 1–2, shared wherever possible |
| Ship texture | One 1024 atlas initially; 512 fallback |
| Total texture memory | Target below 128 MB before platform compression review |
| Runtime memory | Initial target below 500 MB resident on selected test phone |

Measure CPU/GPU frame time in a development player, then confirm release build behavior. Check thermal stability for a 15-minute session, particle overdraw, GC allocations, pause/resume and low-memory recovery. Treat screen-space water/foam cost as a primary risk. Optimize from measured bottlenecks rather than triangle counts alone.

## Asset inventory

Triangle counts below are final Unity mesh budgets, not a promise of direct Tripo output. Texture counts can be reduced using shared atlases.

| ID | Asset / count | Method | LOD0 target | Priority |
| --- | --- | --- | --- | --- |
| SHP_Player_Cutter_01 | Hero cutter / 1 | Tripo from isolated reference, manual cleanup | 4,000–6,000 | First |
| SHP_Enemy_Raider_01 | Raider / 1 | Tripo from prompt or new isolated reference | 2,500–4,000 | Slice |
| SHP_Enemy_Brig_01 | Brig / 1 | Tripo; use board only for style | 5,000–8,000 | Slice |
| SHP_Enemy_Rammer_01 | Reinforced raider / 1 | Modify raider bow | 3,000–4,500 | Slice |
| SHP_Boss_Captain_01 | Captain brig / 1 | Modify brig, pennant and prow | 6,000–9,000 | Slice |
| ENV_Basalt_01..03 | Three rock cluster shapes | Tripo separately, remove generated base | 600–1,500 each | Slice |
| PRP_Crate_01 | Cargo crate / 1 | Simple authored mesh preferred | Under 250 | Slice |
| PRP_Barrel_01 | Floating barrel / 1 | Simple authored mesh preferred | Under 350 | Slice |
| PRP_Chest_01 | Reward chest / 1 | Tripo or authored mesh | 500–1,000 | Later |
| VFX_Combat_Set | Wake, flash, hit, warning, sink, pickup | Unity particles, meshes and shader | Profile overdraw | Slice |
| UI_Core_Set | Icons, buttons and HUD | Authored UI/vector assets | No Tripo | Slice |
| ENV_Dock_01 | Small dock vignette / 1 | Modular authored pieces | Under 5,000 | Soft launch |

## Tripo workflow

Generate the hero first using Art/player-cutter-v1.png; do not upload the entire mood-board collage as an image-to-model input. Collages can generate combined scenes and inconsistent geometry. Generate individual enemy references before image-to-model production, or use the provided text prompts as initial studies. Only the hero currently has a dedicated isolated reference.

1. Keep the render, prompt, generation ID and raw output together in an asset working folder. Record the account/plan and usage rights applicable when generated before commercial release.
2. Generate one candidate, inspect from every side, then refine only if needed. Do not spend credits on a fleet before the first import is accepted.
3. Preserve the original GLB. Export an FBX with textures for the standard Unity pipeline; GLB import requires a compatible importer and is not assumed here. Tripo documents conversion and retopology controls in its [conversion documentation](https://docs.tripo3d.ai/export/conversion.html); exact options depend on the product surface and model used.
4. Inspect in Blender or another DCC: remove unseen junk, fix holes, normals and self-intersections; separate or rebuild sails; normalize dimensions and axes; reduce topology and rebake if necessary. Generated textures may contain baked lighting that should be removed or simplified.
5. Produce final triangles within budget, a clean UV atlas and 1–2 materials. Remeshing alone does not guarantee useful topology. Make a 40–50% triangle LOD1 only if profiling/camera variation justifies it.
6. Import FBX to Unity. Rebuild materials for URP, check sRGB and normal-map import settings, and assign platform texture compression after visual comparison. Test backfaces on sails and verify readable colors under the actual scene light.
7. Create simple hull collision and cannon sockets manually. Set pivot at waterline, +Z forward. Rigging is unnecessary for hull movement; simple sail/pennant animation can be authored later.
8. Compare an actual phone-scale render with ART-DIRECTION.md. Verify no missing stern, fused cannon bores, floating rail fragments, excessively thin sails or unintended extra masts. Reject anything that is only convincing from the original image angle.

Generated ships are starting meshes. Budget time for cleanup; do not equate a successful Tripo generation with a game-ready asset.

## Milestone validation

Graybox: verify steering, both side arcs, cooldown independence, rock line-of-sight, projectile ownership, spawn safety and impossible-to-dodge patterns. Use purposeful gameplay checks rather than tests that merely repeat constants.

Slice: play a full voyage, die/retry, pause/resume, background/foreground, switch scenes and relaunch. Confirm one reward commit per voyage, upgrades reset appropriately, and permanent progress survives. Capture gameplay in the same framing as the art target.

Monetized build: test mocked success, failure, cancellation and duplicate callbacks; verify earned reward exactly once; restore purchases; support no-fill/offline flows. Separate development and production identifiers. Confirm store listing, age rating, privacy declarations and SDK consent flows against the actual chosen SDKs before submission. No store publishing or paid acquisition is part of this design phase.

## Next implementation task

Create the pinned Unity URP project and implement a primitive-geometry combat scene with player steering, one broadside enemy, island collision, camera and win/loss. Establish feel before importing the first cleaned cutter. The first art milestone is an actual Unity screenshot that matches the written visual baseline at phone size.
