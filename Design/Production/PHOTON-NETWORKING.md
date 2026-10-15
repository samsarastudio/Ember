# Photon networking for Emberwake

Decision proposal • 13 October 2026 • SDK setup updated 14 October 2026

## Implementation status — 14 October 2026

Fusion 2.1.3 Stable build 2390 has been imported into the existing Unity 6000.2.6f2 project. The Windows connection probe compiles with this editor; a Unity upgrade was not required for this setup check. The supplied Photon App ID is configured with US East as the development default. The isolated probe supports US East (`us`), US West (`usw`) and Canada East/Montreal (`cae`). Run `Tools/Test-FusionConnection.ps1 -Region us` after building with `BuildFusionProbe.Windows`.

The in-game Battle flow selects the transport advertised by the backend. The Raspberry Pi deployment uses the shared .NET simulation over secure WebSockets; an x64 deployment can select Fusion. `Tools/Play-FusionDuel.ps1` launches a headless Windows player in `GameMode.Server` and two playable instances. Clients can also host an explicitly labeled private test duel. Neither is a production server-subtarget build; a dedicated match process runs where it is launched, while the selected region controls Photon services. Social's old account-backed queue remains on WebSockets. The ranked API now allocates headless matches with account admission tickets, trusted deck/catalog assignment, authoritative result settlement and a 30-second reconnect grace period. A single-machine Windows hosting package is available; multi-region orchestration and production failover remain future work. No paid hosting was provisioned. See Docs/MANUAL-HOSTING.md.

### Implemented combat replication

`FusionBattleWorld` owns one `MatchSimulation` on StateAuthority. The existing 30 Hz rules remain shared with training and backend tests. Public state uses networked bounded arrays for units, active projectiles and zones. Attack, projectile impact, repair, ram and damage events use a 256-entry circular network buffer, including shots which spawn and hit within one simulation tick. Clients smooth ships/projectiles and build cosmetic effects from event IDs; they never run a second damage simulation. Both players receive the same world; cameras and friendly/enemy colors change by team.

This follows the separation of simulation data and visual projectiles described in [Photon's projectile data buffer](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/network-data-buffer). A moving shot remains authoritative until impact; a rendered overlap cannot cause damage. The event buffer is finite, so an extended stall can miss old cosmetic events while the latest HP/world state still converges.

Small reliable messages carry deployments, surrender, validated starter decks and owner-only hand/Supply state. Player identity comes from the sender's PlayerRef, not the payload. The pilot avoids woven RPCs after an observed Fusion 2.1.3 Windows Mono `CheckInvokeRpc` access exception; no SDK binaries were patched. Duplicate/stale command sequences and changed-card slots are rejected. Ranked matches pin the backend catalog and account-owned decks when allocated. Private developer tests still use the default catalog/starter cards and grant no rewards. The backend reads outcomes from its own match processes and settles each account once through the database ledger.

## Recommendation

Use Photon Fusion 2.x for the Unity client and an authoritative dedicated match server for competitive play. Evaluate the current stable 2.1 release during the initial compatibility spike and pin the exact tested SDK patch with Unity 6.3 LTS. Photon announced Fusion 2.1 stable with reconnect and server/host improvements; this recommendation does not assume the separate Fusion 3 Core documentation is the Unity SDK we will install. [Fusion 2.1 release](https://blog.photonengine.com/fusion-2-1-stable-release/).

This is a practical fit for the current design: Unity ships move autonomously, players send occasional deployment commands, and the server must enforce costs, card order, hits, clocks and results. Region selection and immediate local feedback should make deployment feel responsive. Photon is not a guarantee of a particular latency and does not create good gameplay by itself.

## Product and topology comparison

| Option | Benefit for this project | Main trade-off | Decision |
| --- | --- | --- | --- |
| Fusion dedicated server | Central authority, familiar Unity simulation, keep hidden hand and Supply private | Separate game-server hosting and operations | Preferred production direction |
| Fusion client host | Useful quick two-device prototype with a similar server/client programming model | Host advantage, host trust and mobile disconnects | Development testing only |
| Fusion Shared | Distributed authority can reduce some mobile simulation cost | Players own gameplay state unless additional controls exist | Not our competitive default |
| Quantum 3 | Deterministic input-driven simulation and rollback suit many autonomous units | Deterministic architecture, rollback CPU cost and information exposure trade-offs | Strong alternative if the project embraces that architecture early |

Photon describes these authority and hosting differences in its [Fusion topology guide](https://doc.photonengine.com/fusion/v2/fusion-choose). Dedicated mode requires a separately operated headless game process; a Photon Cloud subscription alone is not hosting for our Unity match simulation. No hosting plan or paid subscription has been purchased.

Quantum merits serious consideration for a command-based strategy game, but it is not a drop-in speed upgrade. Its clients simulate the world from confirmed and predicted inputs. Full local knowledge creates a documented hidden-information issue, relevant to an opponent's unrevealed deck, hand and resource count. Its public-cloud result reporting also needs backend validation; deterministic results are not automatically a trusted economy service. [Quantum frames](https://doc.photonengine.com/quantum/v3/manual/frames), [Quantum security and information trade-offs](https://doc.photonengine.com/quantum/v3/manual/cheat-protection).

The choice is therefore about development model and authority, not claiming Fusion always has less latency than Quantum. If server operating cost or very large unit counts become dominant, reconsider Quantum before producing the entire combat codebase.

## Low-latency design

1. Match players in a common low-ping region. Measure candidate regions on startup and let matchmaking select a suitable shared region. Do not select a different best region independently for two friends and expect them to find the same room.
2. Draw card drag, placement preview, haptic feedback and pending-deployment marker locally immediately. The UI must never wait for the network merely to highlight a card.
3. Send a compact deployment request containing match ID, monotonically increasing command ID, card slot/card ID, quantized target position and client tick. Bind player identity to the authenticated connection rather than accepting an arbitrary player ID in the payload.
4. Server validates the request and accepts it on a simulation tick. Supply spending, hand rotation and spawn scheduling happen together. Duplicate commands return the same outcome; invalid commands consume nothing.
5. Begin the local deployment anticipation immediately, but reconcile it against server acceptance. A rejected request removes the pending marker and restores provisional UI. Do not show irreversible hits or rewards before confirmation.
6. Simulate movement, targeting and damage centrally. Render ships smoothly between authoritative updates. The client can predict the resource display, but the server's balance decides whether a card can be played.
7. Do not transmit cosmetic foam, wake particles, cannon smoke or every splash as independent network objects. Replicate gameplay state and compact shot/spawn events, then generate visual effects locally.

Photon regions are separate matchmaking populations. More regions can lower ping but also spread a small player base across more queues. Start with regions supported by the intended test population; decide expansion from measured demand. [Photon region behavior](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/regions).

## Starting rates and latency targets

Start with a 30 Hz authoritative simulation and 60 fps client rendering, with a 30 fps visual quality fallback on weaker phones. Test 60 Hz simulation if input-to-accept time or combat collision quality justifies its CPU/network cost. Configure tick and send rates through the chosen SDK's supported settings; do not assume they must be identical.

Ship views use a small adaptive interpolation buffer. A two-tick buffer at 30 Hz is about 67 ms of visual delay; it must be included in measurements rather than hidden by reporting only ping. This is a candidate setting to tune, not a universal optimum.

Pilot goals, not promised results:

| Metric | Initial goal / measurement |
| --- | --- |
| Local card/drag feedback | Next rendered frame; typically about 17 ms at 60 fps before device/input overhead |
| RTT to regional service | Aim below 80 ms median and 120 ms p95 in selected test regions |
| Deployment acknowledgement | Target below 150 ms p95 in the healthy-network pilot; measure request to accepted/rejected response |
| Intended deploy wind-up | 0.6 s game rule, separate from network delay |
| Ship rendering | Stable frame pacing and few visible corrections, not merely a low average ping |
| Correctness | Zero duplicate charges, free spawns or result grants under retry/loss tests |

The deployment wind-up is an intentional shared combat rule, not permission to delay input feedback. Higher tick rate cannot remove geographic propagation time, mobile radio stalls or packet loss. Avoid globally promising sub-50-ms play.

## State and fairness

Keep both decks, draw queues and resource ledgers on the server. Replicate each player's private hand/next-card/Supply only to that player; broadcast only publicly revealed deployments and world state. Public and private data must be separate replicated objects/channels with verified visibility controls. Do not put both players' hidden fields into an ordinary all-clients MatchState and merely hide their widgets.

Clients never set HP, movement speed, Supply, card levels, match time or winner. The backend validates loadouts before allocation and receives a signed/service-authenticated result once. Match server failure produces an explicit no-contest/recovery policy, not an arbitrary winner chosen by a client. Reconnect restores the private hand and public arena with the original match ID and command acknowledgement baseline.

For slow projectiles and autonomous targeting, use server-simulated projectile impact and authoritative area checks. Fusion's lag-compensated hitbox queries are intended to resolve view-time aiming discrepancies, primarily in server/host mode; they are not an automatic cure for command latency and should not be applied indiscriminately to every cannonball. [Fusion lag compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation).

## Validation before committing production infrastructure

Build a two-client spike with eight cards, crowded channels and the intended worst-case unit count. Use two physical Android devices, including one on cellular data; editor-only tests cannot establish real mobile latency. Record device, region, SDK/build version, server location and transport conditions.

Run controlled profiles at 30, 80, 150 and 250 ms RTT; 0, 20 and 50 ms jitter; and 0%, 1% and 3% packet loss. Include duplication/reordering where the test tooling supports them, three-second connection loss, app background/foreground, reconnect, concurrent deployments at the Supply boundary and the final timer tick. Capture local feedback time, server acceptance, visual appearance, bandwidth, correction frequency and CPU/GPU frame time separately.

Accept the spike only if cards feel immediate locally, both devices agree on outcomes, the advertised deployment rule stays consistent and no duplicated inputs corrupt the economy. Compare 30 Hz and 60 Hz at identical conditions before paying for a higher server budget.

## Cost and setup boundary

Production costs include Photon usage, regional game-server hosting, backend/database services, monitoring and egress where applicable. Capacity should be estimated using peak concurrent matches, server CPU per match and measured traffic. Do not choose a subscription from daily active users alone. A low player count also creates a matchmaking-versus-region trade-off that technology cannot eliminate.

Next production step: integrate backend-issued admission and deck validation, allocate regional match servers, report authenticated results, and add reconnect recovery. Complete worst-case fleet-load and physical-device latency testing before selecting paid capacity. The implemented private-duel pilot is not a production latency benchmark.


