# Emberwake operations

This repository contains a Unity client, dedicated Fusion ranked battles and a .NET 10 / SQLite backend with email-code registration/login and guest linking. Manual Windows hosting is documented in MANUAL-HOSTING.md. This remains a preview: billing, regional autoscaling, production operations and store release validation are not complete.

## Run locally

Run `Tools/Start-Backend.ps1` from PowerShell (it creates a local development admin token in `.tools/local-admin-token.txt` if none is configured), then open the Windows build or Unity `Client` project. The standalone client defaults to `https://royale.inmomentservices.com`. Use `Tools/Play-Preview.ps1` or `-api http://localhost:5080` for local development. Offline training works if the service is unavailable. Two Windows clients can match online, including private room codes. On a physical Android device, localhost is the device: use `adb reverse tcp:5080 tcp:5080` over USB for local development, or configure a public HTTPS service.

Use Unity 6000.2.6f2 for the current validated preview toolchain. The installed 6000.3.6f1 editor is incomplete. Before production, migrate and validate on a complete supported LTS editor and verify the chosen Photon SDK's compatibility.

## Backend

`dotnet run --project Backend/Emberwake.Backend.csproj` requires .NET 10. Set `ASPNETCORE_URLS`, `EMBERWAKE_DB`, `EMBERWAKE_ADMIN_TOKEN` (random, at least 32 characters), `EMBERWAKE_PRIVACY_URL` and `EMBERWAKE_TERMS_URL`. No token is committed. Profiles, guest credentials (hashed), expiring bearer sessions, catalog releases and reward ledgers persist in SQLite WAL mode.

`docker compose up --build -d` builds the backend from the repository root and persists the database in a volume. Supply variables through your deployment secret manager or an uncommitted `.env`. The bound port is loopback-only. Put Caddy/nginx or a managed HTTPS ingress in front, including WebSocket upgrades. Restrict the operations console and `/admin` by network policy; do not expose your token in client builds. Back up SQLite using its online backup API or stop the service before copying the database and WAL files. Test restoring a backup before launch.

Matches currently live in one process and are lost on restart. Deploying during an active match interrupts that match. No horizontal scaling, allocation, regional routing or automatic failover is implemented. Do not run independent replicas against the same database. Add match draining, durable settlement recovery, load testing and monitoring before commercial traffic.

## Content releases

Open the backend root URL for the operations console. Load the current catalog with the admin token, edit JSON, validate and publish an increased version. Publication checks the version loaded to prevent concurrent overwrites. Rollback selects an immutable older release. Current matches hold their original catalog reference; clients refresh on reconnect. Catalog fields include card definitions, news, season name, gold offers, individual card art and optional model bundle metadata.

Starter atlas: `Backend/wwwroot/content/card-atlas.png`. Per-card `artUrl` requires HTTPS. Card `bundleUrl` requires HTTPS, a 64-character `bundleSha256` and `bundleAsset` prefab name. The client hashes downloads and cached bundles before loading, and retains procedural placeholders if a model fails. Build bundles with matching Unity version, URP materials and target platform. Use the card `modelBundles` array for platform-specific content: each entry has `platform` (`Windows`, `Android`, `iOS`), `url`, `sha256` and `asset`. The legacy single bundle URL is a fallback. Bundles cannot introduce new C# behavior. New behavior requires a client and server release. Existing twelve behavior types support new ships with distinct stats and visuals.

Remote image/bundle downloading currently buffers the download before checking size. Add CDN object-size limits, cancellation, quotas and eviction for production. The client cache is not a general mod loader. Publish only trusted prefabs without unexpected scripts.

## Accounts and economy

The preview uses a persistent device guest credential stored in PlayerPrefs; this does not provide secure cross-device recovery. Link to platform identity and use OS-protected credential storage before launch. Account deletion and profile export exist. Profile upgrades are collection prestige only; battle stats are normalized. Shop purchases spend server-owned earned gold. Real-money purchases, ad SDKs and subscriptions are deliberately absent. Add store receipt verification, entitlement reconciliation, refunds and restoration before enabling paid products. Never accept client-reported wins or currency balances.

## Networking

The main Battle flow uses trophy matchmaking and the backend-advertised transport: Pi-compatible WebSockets by default, or Fusion when EMBERWAKE_BATTLE_TRANSPORT=fusion. Private Fusion duels remain developer tests. Run `Tools/Play-FusionDuel.ps1` for one local headless authoritative match server plus two playable clients, or use the private-room command-line launch options. Use `-ServerOnly -Room CODE -Region us` to let two separate devices join a dedicated test server. All peers need the same build, room and region (`us`, `usw` or `cae`). A headless normal Windows player is used for this preview, not a production server-subtarget build.

The match authority runs the shared simulation at 30 ticks/sec. One Fusion NetworkObject replicates bounded public unit, projectile, zone and event arrays; clients never decide damage. A circular history retains rapid fire/impact/heal/ram events between snapshots. Small deployment commands and owner-only hand/Supply updates use Fusion reliable data messages. This avoids an observed SDK 2.1.3 Mono RPC `CheckInvokeRpc` MethodAccessException without modifying vendor binaries. Public state uses ordinary Fusion delta replication, not a JSON snapshot stream. Sequence checks prevent duplicate commands, and an expected-card check rejects a delayed command after its hand slot changes.

Private duels pin the built-in catalog and accept starter decks only. Player identity is bound to the connection's PlayerRef, but this is not backend account authentication. Room codes are not a ranked admission system. No Fusion result grants account rewards. Dedicated-server disconnects forfeit an active match; host loss ends a hosted duel. Reconnect and host migration are not implemented. Do not expose this test flow as ranked play without signed admission, deck validation, backend settlement and recovery.

The older Social queue still uses authenticated WebSocket commands and the backend's 30 Hz combat/10 Hz snapshots. Photon region selection alone does not locate the dedicated match process; regional hosting/allocation and physical-device latency tests remain. See `Design/Production/PHOTON-NETWORKING.md` for the architecture.

## Release gate

- Complete supported Unity LTS toolchain; validated Android ARM64 release AAB and iOS build on macOS/Xcode.
- Production HTTPS service, regional deployment and latency/loss testing on physical devices.
- Production Fusion allocation, authenticated admission, signed results, reconnect/recovery, SDK licensing/account and hosting costs.
- Identity recovery, protected tokens, abuse controls, load tests, restart recovery and operational alerts.
- Secure store billing and verified entitlements if selling DLC; signed bundles per platform.
- Public privacy/terms/support URLs, data disclosures, deletion workflow and age rating.
- Owned bundle identifier, store accounts, signing keys, icons, listing media and review credentials.
- Device performance, touch safe areas, accessibility, localization and playtesting for balance and retention.

Development builds must not be uploaded as final releases. No revenue, retention or review acceptance is guaranteed.


