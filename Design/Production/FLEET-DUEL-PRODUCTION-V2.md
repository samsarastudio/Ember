# Emberwake fleet duel production baseline

Version 2 • 13 October 2026 • Supersedes v1 gameplay and camera implementation

## Active sources of truth

1. [Fleet duel concept](../FLEET-DUEL-V2.md) for rules and player flow.
2. [Photon networking decision](PHOTON-NETWORKING.md) for authority, latency and online validation.
3. [Fleet class board](../Art/fleet-classes-v2.png) for six ship silhouettes.
4. [Duel battle render](../Art/fleet-duel-battle-v2.png) for arena and card-hand composition.
5. [Original art direction](../ART-DIRECTION.md) for palette and material principles only where not superseded here.

The generated images establish visual intent; numeric rules and correction notes take precedence. All renders are AI targets, not engine captures. No Unity project or Photon integration has yet been created.

## Art changes from v1

Replace the player-following camera with a fixed view of both channels and all six permanent structures. Replace joystick/Gust with a four-card hand, next-card preview and Supply meter. The old hero cutter is now the Cutter class; it has no privileged protagonist status. Every class can be on either team. Ivory sail fabric is the neutral baseline, with teal or coral pennants, trim and health indicators identifying the local/opposing team.

Retain the low-poly walnut/ivory/teal palette, broad facets, quiet ocean and sparse basalt detail. Use original nautical iconography and command tray styling. Do not import Clash Royale UI artwork or purple Elixir graphics. Our regenerating resource is Supply, represented with amber segments and a crate/manifest motif.

Battle render corrections for implementation:

- Render says 7/10 Supply but its lit segment count is inconsistent. The real UI must derive both number and fill from the same authoritative/provisionally predicted balance.
- The tiny next-card image resembles a Cutter but shows cost 2. Real Cutter cost is 3; use card-definition data for every cost.
- Ironclad should follow the armored wide-hull silhouette in the class board; the battle render's dark sailing brig is an approximate placeholder.
- Permanent base locations and reef gaps must use mirrored gameplay coordinates, not the slightly irregular generated image layout.
- Team color comes from ownership, not fixed class sail color. Enemy versions of all six classes must be readable.
- Reduce water texture and wake noise relative to the AI image. Remove the large decorative game title from the live combat HUD if it reduces arena space.
- The repair ship's crane should become a short folded chunky shape; remove the class render's delicate hanging chain for mobile readability.

## Ship assets and Tripo handoff

Do not send the six-panel collage into image-to-model. Generate or prepare one isolated reference per ship with the board's shape and material language. Only the existing Cutter currently has a dedicated isolated reference. The class board is a human-facing shape guide.

| Asset | Visual definition | Proposed final LOD0 budget |
| --- | --- | --- |
| Skiff | Tiny narrow hull, one triangular sail, single forward cannon | 800–1,500 triangles |
| Cutter | Existing ivory/teal one-mast two-sail cutter | 2,500–4,000 |
| Ironclad | Wide walnut hull with thick charcoal armor belt and two short masts | 3,000–5,000 |
| Mortar | Squat barge, giant short upward barrel, tiny rear sail | 1,500–2,500 |
| Ram | Long narrow hull, single swept sail, bronze bow wedge | 1,200–2,000 |
| Repair | Broad service hull, wrench sail, crates and folded crane | 1,800–3,000 |
| Cannon outpost | Basalt island base, timber gun platform, chunky rotating gun | 1,000–2,000 |
| Harbor HQ | Broader basalt island, timber warehouse/command deck, flag and gun | 3,000–5,000 |

The six ship meshes require team-color masks and per-class collider footprints. Standard ship hull length starts around 2.5–3.5 arena units, with Skiffs smaller. Do not import the v1 six-unit Cutter scale unchanged into the compact duel arena. One simulation unit is an arena scale convention here rather than a requirement for nautical realism.

Keep materials to 1–2 per ship. Prefer a shared 1024 fleet atlas plus simple team-color parameter. Define circular or capsule planar colliders for navigation and hits; masts and sails do not affect collision. Bow is +Z. Visual bobbing, sail sway and recoil occur on a child transform and never move the simulation collider.

Tripo prompt template: "One complete isolated [class definition above], stylized low-poly mobile naval game asset, matte walnut hull, ivory sail, simple replaceable team pennant, broad faceted surfaces, complete underside, no ocean, wake, environment, base, labels, crew or fine ropes. Distinct [class silhouette feature]. Keep functional parts clean and separable where possible." Append the class-specific size and cannon requirements. For outposts/HQ, explicitly include the island foundation as part of the object; the no-base instruction applies only to ships.

Use the cleanup/import workflow in the v1 production document for raw GLB preservation, FBX export, topology cleanup, UVs and URP materials. New silhouette and scale rules here override the old player/enemy hierarchy.

## Unity architecture

Keep simulation logic independent of VFX, card dragging and UI so the same rules run in offline practice and on the dedicated server. Use Photon Fusion for synchronization following the networking decision. The authoritative simulation controls costs, card queue, spawn delay, target selection, movement, damage and match phase.

| System | Responsibility |
| --- | --- |
| CardCatalog / DeckDefinition | Costs, classes, order behavior and deck validation |
| DeploymentService | Request identity, placement validity, atomic spend/cycle/spawn |
| SupplyLedger / HandQueue | Resource phase rate and private ordered card state |
| ArenaNavigation | Channel graph, static obstacles, local avoidance and objective routing |
| UnitBrain | Target priorities, range, movement and class-specific behavior |
| CombatSystem | Projectiles, splash, healing, status durations and batched tick damage |
| ObjectiveSystem | Outposts, HQ activation, destruction and territory unlocks |
| MatchDirector | Clock, multiplier changes, simultaneous results and final resolution |
| MatchGateway | Queue, region, auth, server assignment and reconnect |
| ResultsService | Exactly-once result/grants and progression updates |
| Presentation | Camera, card UI, animation, particles, sound and local anticipation |

Use an authored lane graph and planar steering initially. A ship advances through the nearest legal channel, can engage nearby targets, then resumes toward an objective. Avoid rigid single-file paths and full free-ocean navigation. Visual hull rotation must not force the gameplay AI into long circles simply to align broadside cannons. A mobile strategy abstraction is more useful than preserving the v1 steering simulation.

Keep private hand/Supply replication separate from public units. Do not put secrets into shared replicated state. Use backend-approved deck and balance versions at match start. Log command IDs and authoritative outcomes for diagnostics. Replays can record the command stream and state snapshots; do not promise cross-platform deterministic PhysX replay.

## Performance and content envelope

Start at 30 authoritative ticks/s and 60 rendered frames/s, then profile. Test up to 60 active ships plus temporary effects. Use a deliberately generous safety ceiling such as 96 ships total; if reached, reject new ship deployment with no cost/queue change and a clear capacity message. This is an emergency guard, not intended normal balance. Stress testing must show the expected 3×-Supply match remains comfortably below it; otherwise add unit lifetimes or revise costs intentionally.

Target approximately 250,000 visible triangles or less in a heavy duel, limited transparent overdraw and around 150 batches or fewer as initial mobile goals. These are unmeasured targets. Count all spawned Skiffs individually. Pool projectiles, units and effects. Tiny distant ships may need simplified LODs, particularly swarms. Validate the busiest match at 360-pixel-wide readability, on a low-tier Android device and under thermal load.

## Build gates

**Offline duel:** all eight cards, both channels, three structures per side, card cycling, Supply phases, victory/defeat/draw, deterministic rule tests for timer ordering and duplicate commands. Use primitive meshes initially.

**Online spike:** two physical devices, Photon connection, dedicated authority, hidden-state separation, low-latency local input feedback, reconnect and adverse-network tests. Record actual latency before describing gameplay as low-latency.

**Art slice:** import Cutter and one contrasting class, then prove camera readability, collision scale, team tint and rendering budget. Only then generate the rest of the fleet.

**Product loop:** tutorial, Harbor, fleet editing with twelve available cards, matchmaking, results, Play Again, progression and match history. Verify reward persistence after app termination and duplicate server callbacks.

**Soft launch:** service monitoring, load tests, queue/region strategy, device QA, privacy/store requirements for chosen services, controlled economy and retention tests. Cosmetics/passes are introduced after the gameplay loop is measured.

## Priority for the next implementation turn

Validate the chosen Unity/Fusion versions, then build the two-lane bot duel and a minimal two-client deployment spike alongside it. The first milestone is a playable, measurable deployment-and-counter loop. Producing more promotional art does not substitute for that proof.
