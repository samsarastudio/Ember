# Manual hosting: royale.inmomentservices.com

This package targets a **Windows x64 VPS/dedicated machine**. It contains a self-contained .NET API and the tested Windows Unity player, run headlessly per match. Shared website hosting cannot launch these processes. Linux requires a separate Unity Linux build and launcher; the current Docker compose does not run the ranked allocator.

## Install and start

1. Copy the entire Hosting folder to the server, for example `C:\Emberwake`. Keep `Api`, `Game`, `state`, and the launcher together. Restrict this directory to the service operator account; `state` holds account data, signing keys and match admission tickets. Never put it in a public website directory.
2. Point the DNS A record `royale.inmomentservices.com` at the server's public IPv4 address. Add an AAAA record only if IPv6 is configured. DNS was not resolving during local preparation.
3. Install [Caddy for Windows](https://caddyserver.com/download). Place `caddy.exe` alongside the supplied `Caddyfile`. Allow incoming TCP 80/443 to Caddy. Keep API port 5080 private. Permit the Unity processes outbound access to Photon Cloud and UDP; their connection setup uses Fusion's automatic port selection. Test your provider's firewall/NAT with real remote clients.
4. Configure email variables below in the environment of the API launcher. Run `powershell -File .\Start-Server.ps1 -Region us -Capacity 4`. The first launch creates `state/secrets.json` with random admin and account-signing keys. Preserve it between restarts. No .NET runtime installation is required.
5. In another terminal in this folder, run `.\caddy.exe run --config .\Caddyfile`. Caddy obtains and renews the HTTPS certificate once DNS and ports work. See [Caddy's HTTPS setup](https://caddyserver.com/docs/quick-starts/https).
6. Visit `https://royale.inmomentservices.com/health`; expect `rankedAvailable: true`. The new Windows game defaults to this URL. Existing installations can change the service address in Settings. For local tests, use `-api http://127.0.0.1:5080`.
7. Register two test accounts through **Settings → Account**, verify both emails, and press **Battle** on both devices. Check movement, attacks, card/supply ownership, result persistence and reconnect from two actual networks before inviting players.

Use Windows Task Scheduler (run whether a user is logged in; restart on failure) or your normal service supervisor for the launcher and Caddy. Run under a dedicated, non-administrator service account with write access to `state`. Configure a single API instance; this version uses a local queue and SQLite, not a distributed cluster.

## Email registration / sign-in

Set these in the environment before starting `Start-Server.ps1`:

```powershell
$env:EMBERWAKE_SMTP_HOST = 'smtp.your-provider.example'
$env:EMBERWAKE_SMTP_PORT = '587'
$env:EMBERWAKE_SMTP_USER = 'your-smtp-user'
$env:EMBERWAKE_SMTP_PASSWORD = 'your-provider-secret'
$env:EMBERWAKE_SMTP_FROM = 'Emberwake <login@your-verified-domain.example>'
$env:EMBERWAKE_PRIVACY_URL = 'https://your-published-privacy-policy'
$env:EMBERWAKE_TERMS_URL = 'https://your-published-terms'
```

SMTP must support STARTTLS, normally port 587; implicit TLS port 465 is not supported by this sender. Verify the sending domain and SPF/DKIM with your mail provider. Codes are six digits, expire after 10 minutes, allow five attempts, and cannot be reused. Resends have a one-minute cooldown; auth endpoints also have per-IP limits. Codes are HMAC protected in SQLite and never returned by the API or logged. Email delivery requires your real SMTP configuration and has not been tested against your provider.

Guests can play immediately. Register/link preserves their current captain. Sign in with an existing email loads that captain on a new device; progress is not merged. Each new device receives its own credential. Sign-out revokes that device credential and current session. Account deletion removes the email binding as well as progression. Device credentials are scoped to the selected API origin in the client; they currently use Unity PlayerPrefs (OS secure storage remains a mobile-release hardening task).

## Battle hosting and matchmaking

The main Battle button uses `/v1/fusion/queue`. Accounts are authenticated, decks checked against server ownership, then matched by similar trophies. The initial band is ±100 trophies, expanding by 50 every five seconds up to ±600. Both players' waiting ranges must allow the match. These are **our tunable rules**, not Supercell's private algorithm. Deck composition and card level do not affect pairing, following [Clash Royale's public 1v1 description](https://support.supercell.com/clash-royale/en/articles/1v1-battle-2.html).

Each match gets a separate Fusion `GameMode.Server` process, two random admission tickets, pinned catalog/decks, and a server seed. Neither player hosts the battle. The server validates deploy commands and owns movement, projectiles, impacts, damage, victory and rewards. Client visuals interpolate replicated state. A dropped player has 30 seconds to return to harbor and tap Battle to reconnect; after that the server forfeits that player. If both clients never become ready, the allocation times out without rewards.

The API reads an atomic result file produced by its own child process and records rewards idempotently in SQLite. There is no public client endpoint that submits wins. API restarts abort in-flight battles; this is not seamless server failover. Completed result files can recover settlement on restart. Keep `state/matches` until results are settled; archive older logs and completed directories to control disk usage.

Start with `-Region us` on an eastern US machine for eastern US/Canada tests. `usw` selects US West and `cae` Canada East Photon regions. Changing the Photon region does **not** move the physical battle server. Deploy geographically close to your players and measure latency. This package has one configured region, not multi-region autoscaling. Internet play cannot be latency-free. Capacity defaults to four simultaneous processes; profile CPU/RAM and respect the Photon plan's CCU limit before raising it. [Photon documents the dedicated-server hosting model here](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/dedicated-server-overview).

## Updates and operations

- Back up `state/emberwake.db` and `state/secrets.json` using SQLite backup tooling or stop the service first and copy the database and WAL/SHM files together. Do not overwrite state with a new package.
- To update safely, stop accepting players, let matches finish, stop the API, replace `Api` and `Game`, then restart. This version does not provide a maintenance/drain admin command.
- The original WebSocket friend/test queue remains available; private Fusion launch scripts remain developer tools. The new ranked path is separate. The Android development APK has been rebuilt with the new account/ranked UI; physical-device testing remains.
- This delivers accounts, progression, matchmaking and authoritative combat. Clans, purchases, push notifications, automated regional scaling, seamless failover, abuse monitoring and app-store release work are separate features; this is not a clone of Supercell's entire live service.

## Build again from the source workspace

```powershell
.\Tools\Build-Preview.ps1 -Target Windows
.\Tools\Package-Server.ps1
.\Tools\Test-RankedDuel.ps1 -Region us
```

The package script creates a fresh timestamped folder and never overwrites an existing deployment.

