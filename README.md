# Ember

Emberwake Fleet Duel is a Unity naval card battler using Photon Fusion 2 Server Mode. Player releases connect automatically to **https://royale.inmomentservices.com**. There is no server-address setting; old developer URL preferences are ignored.

## Build iOS with Codemagic

Connect this repository to Codemagic and select the **ios-release** workflow from `codemagic.yaml`. The project is under `Client`, using **Unity 6000.2.6f2** and bundle identifier **com.emberwake.fleetduel**. See [the iOS setup guide](Docs/CODEMAGIC-IOS.md) for required Unity licensing and Apple signing configuration. The workflow creates a signed IPA; it does not automatically publish to the App Store or TestFlight.

## Local builds

With Unity 6000.2.6f2 and the target platform module installed:

```powershell
./Tools/Build-Preview.ps1 -Target WindowsRelease
./Tools/Build-Preview.ps1 -Target AndroidRelease
./Tools/Build-Preview.ps1 -Target IOS
```

Outputs are under `Builds/Release/Windows`, `Builds/Release/Android` and `Builds/iOS` respectively. iOS exports an Xcode project; signing and IPA packaging require macOS/Xcode or Codemagic. The Android release APK uses local debug signing for sideload testing unless a release keystore is configured; it is not a Play Store upload artifact.

Development-only Windows/Android builds remain available with `-Target Windows` / `-Target Android`. Their server override is a command-line flag for test tools, never an in-app setting. Release builds ignore that flag and use the production address.

## Server

The live test service runs the account API and authoritative Fusion battles on Windows, with Cloudflare Tunnel publishing the HTTPS API. The Windows machine must remain online. The account API validates decks, pairs players by trophies, allocates Fusion matches, and stores server-verified results. Ships, attacks, projectiles and damage are simulated by the battle authority.

Open `Start-Server-Control.cmd` for the Windows tray panel: start/stop/restart, logs, and a live tactical view of active battles. Closing the panel minimizes to the tray. It has no game-launch action.

Human opponents take priority. After 15 seconds without a suitable opponent, the server offers a labelled one of **10 persistent AI captains** battle through Fusion. The AI uses normal combat rules and a validated mirrored deck; the human receives normal progression. Configure `EMBERWAKE_AI_WAIT_SECONDS` (1–120) or disable with `EMBERWAKE_AI_ENABLED=0` before starting the API.

- [Windows/Cloudflare operation](Docs/WINDOWS-CLOUDFLARE.md)
- [Manual server hosting](Docs/MANUAL-HOSTING.md)
- [Optional Pi coordinator and remote Windows worker](Docs/FUSION-SPLIT-HOSTING.md)
- [Verification history](Docs/FUSION-SPLIT-VERIFICATION.md)

Guest play is available. Email sign-in requires SMTP configuration. Public privacy/terms URLs, store signing, secure credential storage, capacity testing and production operations remain release prerequisites; using the live endpoint does not certify those items complete.

## Source layout

- `Client/Assets/_Game`: client UI, gameplay, shared simulation and build scripts.
- `Client/Assets/Photon`: Fusion SDK and its bundled notices; third-party rights remain with their owners.
- `Backend`: .NET 10 accounts, matchmaking, persistence and battle allocation.
- `Tests`: logic/account/worker checks (`dotnet run --project Tests`).
- `Tools`: local test, build and deployment helpers.

Generated builds, Unity caches, local databases, tunnel credentials, signing material and test logs are excluded from Git. Photon App IDs are client identifiers, not account-management credentials.
