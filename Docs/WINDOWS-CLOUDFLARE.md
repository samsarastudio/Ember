# Windows internet test through Cloudflare

Configured on 2026-10-15 for https://royale.inmomentservices.com.

## Start

Double-click `Start-Server-Control.cmd` for the Windows tray panel. It provides start, stop, restart, logs and a live tactical spectator view of active games. Closing/minimizing the window hides it in the tray; **Exit panel (keep server running)** exits only the UI. Restart/stop ends active matches and asks inside the panel before doing so. The Cloudflare tunnel remains running when the API stops. There is no game-launch option in the server panel.

The observer reads local public combat snapshots at 5 Hz, including ships, health, projectiles and zones. It never joins as a player and receives no private hands. Select a battle in the dropdown; its final state remains visible after completion. This operator view is local to the Windows battle host, not a public spectator endpoint.

Build the panel with `.tools/dotnet/dotnet.exe publish ServerControl -c Release -r win-x64 --self-contained true -o Builds/ServerControl`.

Double-click `Start-Internet-Test.cmd` in the project root. It starts/reuses the local account API and Fusion allocator, starts/reuses the Cloudflare tunnel, verifies public health, and opens one Windows player connected to the public API. Processes stay running after the launcher closes. Run it again after reboot; automatic startup at boot is not installed.

On Android, install `Builds/Release/Android/Emberwake.apk`. Turn Wi-Fi off, complete onboarding if shown, then press Battle. The release app connects to the live HTTPS service automatically; there is no service URL to configure. Guest login works; email registration still requires SMTP configuration.

If no suitable human joins within 15 seconds, the allocator starts a Fusion battle against one of **10 persistent AI captains**. Server startup adds a few seconds after that wait. The AI runs on the Windows battle authority and uses the same supply, deployment, targeting, projectile and damage rules. Human pairs take priority. The AI mirrors the human's validated deck, collection levels and trophy count, has a persistent server-only account with no login or client admission ticket, and does not occupy the human leaderboard. Results award the normal server-verified progression to the human and name the AI captain in match history.

Set `EMBERWAKE_AI_WAIT_SECONDS` before starting the API to change the wait (1–120 seconds); set `EMBERWAKE_AI_ENABLED=0` to disable fallback. Both local allocation and the optional remote worker support AI matches. AI battles consume a normal server match slot.

## Configuration

- API: Windows package Start-Server.ps1, bound only to 127.0.0.1:5080; Fusion local allocation, region us, capacity 4.
- Public hostname: royale.inmomentservices.com, routed to tunnel emberwake-royale-windows (bce26c82-6178-4d68-bbab-44a5e8083672).
- Tunnel: official cloudflared 2026.10.0, SHA256 verified against the GitHub release digest before execution.
- Configuration: .tools/cloudflared/royale.yml. HTTP origin is localhost only; external users use HTTPS through Cloudflare.
- Credentials: the user's .cloudflared directory, outside download packages. Keep cert.pem and tunnel JSON private.
- Database/secrets: Builds/Fusion-Windows-Worker-20261014/state. Back up this directory; do not publish it.
- Logs: package test-logs/public and test-logs/internet-*; individual client tests have separate two-windows-* folders.

The Pi is not involved. The tunnel carries account and matchmaking HTTP traffic. Fusion handles battle connectivity separately, with its automatic direct/relay selection. No router port forwards were added. Keep the PC awake and online; shutting it down stops matchmaking and battles.

The public HTTPS health endpoint and two-client flow are tested from this PC. Physical Android cellular connectivity and its latency still require the user's device test. An independent web-fetch tool could not access the health URL, so that tool is not evidence of off-network reachability.

## Verified update, 2026-10-15

- 109 automated logic, account, matchmaking, AI and trophy-unlock checks passed.
- Release Windows ignored an invalid `-api` override and completed a live two-player Fusion duel with matching final state.
- A lone release Windows player matched Finn Ironshore after the fallback wait. Both fleets, attacks, projectile impacts, damage and authoritative settlement passed the combat audit.
- The Windows panel displayed that active battle and live server status. Observer data contains public combat only; it excludes private hands and supply.
- The API and Windows battle executable were replaced and restarted, preserving the existing database and Cloudflare tunnel. Public health reports Fusion matchmaking available.
- Windows release and iOS Xcode export succeeded. Android compilation was cancelled at the user's request. A signed iOS IPA still requires Codemagic licensing/signing setup and a successful macOS build.

The ten AI account names persist across restarts. Their per-match fleet, collection levels and trophies mirror the human. Collection levels are cosmetic prestige in the current game; both sides use the same ranked combat stats. Human trophy leaderboard entries exclude AI accounts.

Trophy unlocks are reconciled and persisted when reading the server profile. Opening Fleet refreshes that profile, and a result arriving while Fleet is open redraws the collection. Tests cover thresholds 60/120/180/240, a real win from 30 to 60, duplicate settlement, and retaining unlocks after trophy loss/reconnect.
