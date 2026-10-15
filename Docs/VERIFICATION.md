# Verification record

This file records checks performed on the development implementation. It does not certify a production release.

Previous local suite: **46 checks passed** on 2026-10-13, including the live backend and authenticated admin API. Windows runtime audit completed across ten screens with no logged runtime exceptions. Windows development build and Android ARM64 development APK compiled successfully; Android package metadata reports minimum API 26 and target API 36. The APK uses development signing and is not a store submission artifact.

Update, 14 October 2026: **50 local logic/storage checks passed** after the Fusion combat changes. The current Windows player was rebuilt successfully with Unity 6000.2.6f2 and Fusion 2.1.3 Stable 2390. At that earlier checkpoint, the Android APK predated Fusion gameplay. The latest APK has now been rebuilt with accounts and both transports; see the final build note below.

## Fusion combat verification — 14 October 2026

`Tools/Play-FusionDuel.ps1 -Automated` runs the actual Boot scene with one headless server and two rendered game clients. Both clients submit deployment requests through Fusion, including their expected card ID. The authority simulates combat for about one minute, then team 1 surrenders. Every received public-state update is checked against the authority's hash (unit/shot/zone/event data, combat tick and winner). The launcher compares final tick, winner and hash across all processes and requires successful exits. It also requires each client to have observed both fleets plus attack, impact and damage events.

| Test | Client observations | Final agreement | Logs |
| --- | --- | --- | --- |
| Dedicated, US East | Both clients: 195 attacks, 195 impacts, 173 damage events | tick 1802, winner 0, hash -2081953009 | `Builds/FusionBattleTests/57b71ddc698c4ab8beecd85d3c1c6ab0` |
| Dedicated, Canada East with simulated impairment | Both clients: 171 attacks, 170 impacts, 165 damage events | tick 1806, winner 0, hash -2110922941 | `Builds/FusionBattleTests/b60090af0dc74d15ae1adbaed92715ff` |
| Client host, US West | Both clients: 173 attacks, 173 impacts, 167 damage events | tick 1802, winner 0, hash -926448281 | `Builds/FusionBattleTests/b80d6cec46aa482db1e86253fdb9157a` |

The impaired run configures Fusion packet simulation on the participants: 80 ms delay, 0–20 ms additional jitter and 2% loss. These are configured packet conditions, not measured round-trip latency. All processes run on this PC; selecting a Photon region does not move the dedicated match process to that region. Both US East screenshots were visually inspected: full portrait HUD, private hands, Supply, health bars and mirrored fleets render correctly. Screenshot capture was corrected to use the render target's dimensions before laying out the canvas.

New logic checks cover delayed-card mismatch rejection, same-tick projectile fire/impact persistence, exactly-once projectile damage, identical event history in both perspectives, repair healing, charged ram damage, bounded event IDs, and a 96-ship simulation within replicated array limits. The 96-ship check is a simulation capacity check, not a network load or mobile performance benchmark.

Runtime testing caught and corrected two integration failures: a Mono `CheckInvokeRpc` access exception (small messages now use Fusion reliable data), and a final winner update ignored when surrender did not advance the combat tick. Packet-simulation setup also preserves the loaded runtime prefab table rather than using a serialized config copy.

The client-host run additionally verifies that the hosting player submits its ready/deck message using its assigned captain identity, rather than relying on the runner's client-mode flag. Both host and joining player received their private hand state and rendered a complete duel. All three successful runs exited cleanly; no test match process is intended to remain running.

## Executable tests

### Photon setup check — 14 October 2026

- Imported Fusion 2.1.3 Stable 2390 successfully into Unity 6000.2.6f2 and built `Builds/FusionProbe/FusionProbe.exe` successfully.
- Verified the supplied App ID with one local headless Fusion server and two local clients in each region: US East (`us`), Canada East (`cae`), US West (`usw`). All nine processes reported `FUSION_PROBE_TWO_PLAYERS_OK` and exited with code 0.
- Logs: `Builds/FusionProbe/Logs/emberwake-probe-d74799ecaa0048bf845e6d3a443ea6c8` (US East), `emberwake-probe-5008a939d9004e3ebdc68d2857323383` (Canada East), `emberwake-probe-8616316b53c04a6bb6737790f6fa37fd` (US West), all under the same Logs directory.
- Headless runs emit shader warnings because graphics are disabled. They are not visual quality tests. This check does not exercise combat, authenticated player admission, mobile devices, or geographically distributed match servers; it does not establish latency targets.

`Tests/Program.cs` exercises catalog rules, platform model manifests, numeric validation, deployment authority, supply, command retries, hidden hand scope, overtime, simultaneous destruction, a complete bot duel, finite combat state, authentication, starter ownership, deck rejection, idempotent rewards, upgrades, season gating, content publication/rollback, newly published starter unlocks, guest resumption and deletion.

With a local backend URL, it also checks two-player matchmaking, authenticated streaming, proper WebSocket close handshake, unauthorized access rejection, live deployment and retry acceptance, surrender and persisted settlement. Tests create isolated storage for logic tests and disposable captains for the live HTTP tests.

## Render audit

The Windows development player supports `--capture="ABSOLUTE_DIRECTORY/audit.png"`. It runs a deterministic screen tour and writes harbor, fleet, card detail, season, shop, social, settings, tutorial, history and an active practice battle to that directory. It renders through an explicit URP render request so the audit works with a hidden test window. The command then exits. These are real runtime renders, not AI UI mockups. Screens are under `Builds/Screenshots` after the audit.

The initial audit found and fixed atlas slicing after Unity resized a non-power-of-two texture. Live integration testing found and fixed a missing WebSocket close handshake. Android compilation initially ran out of memory; IL2CPP conversion now uses two jobs. Low-memory build flags are included in the build script.

## Not verified

- Android installation, touch interaction and thermal/performance behavior on physical devices: no ADB device was connected during this session.
- iOS compilation, signing or StoreKit behavior.
- Hosted production behavior, server failover/host migration and physical-device gameplay latency. Local authenticated admission, trusted rewards and reconnect have now been exercised as recorded below.
- Production HTTPS, region latency, packet loss, failover, large concurrency or load.
- Paid products, receipts, refunds, ad attribution or revenue.
- Live external model CDN delivery and Tripo model integration.
- Final artwork quality, balance, retention, accessibility and localization.

Compiled development binaries and passing local tests do not remove these release requirements.


## Accounts, ranked matchmaking and Raspberry Pi hosting — 14 October 2026

- Latest logic/storage plus live HTTP suite: **84 checks passed**. Includes guest linking, cross-device email login, one-use codes, attempt/cooldown limits, device sign-out, deletion, server trophy pairing, pinned catalogs, atomic/idempotent two-account settlement and rollback on failure. SMTP provider delivery remains unconfigured/unverified.
- Pi-compatible .NET/WebSocket server with two real Windows Unity clients: `Builds/PiServerTests/3dbc446b7cf64096a7fbb27af556214f`. One client was killed and restarted during combat; it resumed the same account/team and match. Both finished at tick 1802, winner 0, public hash 1712232355. Both observed fleets, attacks, impacts and damage; each account received one persisted result.
- Final atomic-settlement regression: `Builds/PiServerTests/5ace45d86bb94c8b87d27dceefc43e29`. Both clients finished at tick 1801, winner 0, hash -159832440, with 165 attacks, 163 impacts and 158 damage events. Saved rewards were verified for both accounts.
- Fusion ranked US: `Builds/RankedTests/6173040659c64c9a94e92febae0635d1`, both clients/server tick 1801, winner 0, hash -2102646004. API allocation, ticket admission and persisted rewards passed.
- Fusion Canada East with configured 80 ms delay, 0–20 ms jitter, 2% packet loss and a restarted client: `Builds/RankedTests/b66adcd930614f8aba9333b6b666c7d5`. Final tick 1802, winner 0, hash 1688125787 matched. This is packet simulation on local clients, not measured US/Canada end-user latency.
- ARM64 self-contained server cross-published successfully. Main executable and SQLite native library are ELF machine 183 (AArch64). `install.sh` passed Bash syntax validation. Neither the installer nor the server has been executed on the user's physical Pi.
- Windows workstation benchmark (four artificial 96-ship battles with JSON serialization): mean 2.31 ms, p95 6.52 ms, max 15.26 ms against a 33.33 ms tick budget; mean snapshot 27,449 bytes. This is not Pi performance or a concurrency guarantee. The `--benchmark` command is included for measuring on the Pi and excludes TLS/network/storage/thermal load.
- Domain deployment, DNS/port forwarding, real SMTP delivery and actual Pi load/latency remain manual verification steps. See PI-HOSTING.md. No hosting or DNS changes were made.

Final build note: Windows development player and Android ARM64 APK both built successfully. Android package is com.emberwake.fleetduel, versionCode 1, minimum API 26, target API 36, arm64-v8a, 79,324,790 bytes. No physical Android device was tested. The final local logic/storage suite has 77 passing checks, adding guest-credential/session retirement during account linking after the 84-check combined suite above. Final Pi archive: Builds/Emberwake-Pi5-20261014.tar.gz (48,032,414 bytes).

