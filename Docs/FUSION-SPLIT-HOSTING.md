# Fusion: Pi coordinator + Windows battle worker

Photon Fusion 2 Server Mode is the gameplay transport. The Pi runs accounts, trophy matchmaking and SQLite progression; the Windows x64 worker runs headless Unity battles. Players exchange combat state with the Fusion battle server, not through the Pi. Photon Cloud provides session/connectivity services, not execution of the Unity simulation.

## Windows and Android test now

For the standalone Windows package, extract the entire folder and double-click Start-Windows-Android-Test.cmd. It runs the same test setup without requiring Unity or .NET installation. Install the separately supplied APK on your phone. The source-workspace alternative is described below.

1. Install `Builds/Android/Emberwake-preview.apk` manually on an ARM64 Android phone (Android 8 or newer). This is a development-signed test APK.
2. Connect the phone and PC to the same trusted Wi-Fi network.
3. In PowerShell, from the project directory, run `./Tools/Start-MobileTest.ps1 -Lan -Region us`. The script starts the coordinator and remote worker as separate processes, then opens the Windows game. It prints the PC's private network address.
4. On Android, open Settings, set Service address to the printed `http://192.168.x.x:5080` (or other printed private address), and press Reconnect. Use guest/disposable accounts for this HTTP development test. Production requires HTTPS. Allow the .NET API on the Windows private-network firewall if necessary; do not expose port 5080 to the internet. Guest Wi-Fi/client isolation can block this connection.
5. Complete the tutorial if shown, then press Battle on both devices. Each installation gets its own account. Check card placement, movement, projectile impacts, damage and results. Close/reopen one game within 30 seconds and press Battle to rejoin the same match.
6. Keep the PowerShell launcher open. Press Enter there after testing to stop its services and Windows game. Test profiles remain in `Backend/data/mobile-test.db`.

`-Region cae` tests Photon Canada East; `-Region usw` tests US West. These flags change the Photon session region, not the physical location of your PC. This local setup cannot measure production data-center latency. The phone must have internet access to Photon.

If using USB instead, run `./Tools/Start-MobileTest.ps1 -InstallAndroid` with one authorized USB-debugging device. This installs the APK and forwards only the account API; use `http://127.0.0.1:5080` in Android Settings.

## Two Windows clients on one PC

Keep the server/worker launcher running, then double-click `Start-Two-Windows-Clients.cmd` in the Windows package. It opens two captains with separate persistent test credentials and connects both explicitly to `http://127.0.0.1:5080`. Press Battle in each window. Close any older game windows first to avoid confusion.

Do not test by opening `Game/Emberwake.exe` twice: ordinary instances share Windows PlayerPrefs, including the remembered account. Two copies using the same account cannot match against each other. The standalone EXE also defaults to the production service address unless overridden. The two-client launcher handles both issues and writes separate client logs under `test-logs/two-windows-*`.

The two test credentials are stored in `state/two-windows`; keep that directory private. This launcher does not start or stop your server. It skips tutorial onboarding only for its dedicated test captains.
## Pi installation

Use 64-bit Raspberry Pi OS (`uname -m` must return `aarch64`). Copy the newly generated Pi hosting folder onto the Pi, install Caddy, OpenSSL and ICU, then run `sudo bash install.sh` from that folder. The installer preserves existing data and environment files.

In `/etc/emberwake.env`, ensure these settings exist, including when updating an older installation:

```ini
EMBERWAKE_BATTLE_TRANSPORT=fusion
EMBERWAKE_REMOTE_WORKER=1
EMBERWAKE_MATCH_REGION=us
EMBERWAKE_MATCH_CAPACITY=4
EMBERWAKE_WORKER_KEY=<a randomly generated secret of at least 32 characters>
```

The new installer generates a worker key automatically for fresh installations. For an older installation, generate one with `openssl rand -hex 32`. Keep this key private; it authorizes battle outcomes. Configure SMTP separately for email codes. Existing guest play does not need SMTP.

Set `royale.inmomentservices.com` DNS to the Pi's public address and forward TCP 80/443 to Caddy. Import `/etc/caddy/sites/*.caddy` from the existing Caddyfile, validate and reload Caddy. Keep API port 5080 private. Home connections behind CGNAT require a suitable public ingress setup; no DNS or router changes have been made automatically.

Run `sudo systemctl restart emberwake`. Before the Windows worker connects, `/health` should show `transport: fusion` and `rankedAvailable: false`.

## Windows worker installation

Copy the new Windows hosting package with its `Api`, `Game`, and `Start-Worker.ps1` onto the battle PC. Create `worker-key.txt` alongside the script containing exactly the Pi's worker key. Restrict access to this folder to the service operator; assignment files contain match tickets. Do not put it in a website's public folder.

```powershell
./Start-Worker.ps1 -Coordinator https://royale.inmomentservices.com
```

The worker makes outbound HTTPS requests to the Pi and launches Unity/Fusion processes. Permit Fusion's required outbound network access and test direct/relay connectivity on the intended host. It does not open an administrative HTTP listener. Run the launcher with a service supervisor or Windows Task Scheduler for unattended use.

Once the worker connects, check `/health` for `rankedAvailable: true`. Set both games' service address to `https://royale.inmomentservices.com`. Do not also run `Start-Server.ps1`; that is the alternative all-on-Windows deployment.

## Current guarantees and limits

- One coordinator and one trusted worker; up to the configured number of concurrent matches. This implementation does not yet provide multi-worker scheduling, automatic multi-region selection or high availability.
- The coordinator checks worker authentication and assignment ownership. Results are validated and stored transactionally for both players; duplicate delivery does not double-award rewards.
- Workers poll every two seconds. This affects match startup, not combat latency. Warm process pools are not implemented.
- Worker connectivity loss aborts affected battles without rewards. A worker stops simulations after 30 seconds without coordinator contact; the coordinator expires worker ownership after 45 seconds. A restarted worker does not restart an interrupted match from tick zero.
- Finish battles before restarting/updating the coordinator. In-flight matches are not recovered after coordinator restart. Completed result files are recovered idempotently at API startup. Keep database and signing keys backed up.
- Local placement markers respond immediately; actual deployment and combat remain server-owned. The client logs `FUSION_DEPLOY_ACK_MS` to measure command acknowledgment (including processing), not pure network RTT. Full client simulation prediction is not implemented.
- The target is smooth 60 FPS rendering with the existing 30 Hz combat simulation. Real phones, internet connections, worker capacity and temperatures still require profiling. No latency guarantee or production capacity has been established.
- Packages are test/pilot builds. Production still needs operational monitoring, backups, secure mobile credential storage, store signing and real SMTP/device testing.

Rebuild using `Tools/Build-Preview.ps1 -Target Windows`, then `-Target Android`. Package using `Tools/Package-PiServer.ps1` and `Tools/Package-Server.ps1`. Automated remote-worker battle validation: `Tools/Test-RankedDuel.ps1 -Remote -Region us`; use `-Region cae -Impaired -Reconnect` for the adverse-network/rejoin case.
