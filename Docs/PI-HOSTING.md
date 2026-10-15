# Raspberry Pi coordinator for Photon Fusion

The selected architecture uses the Pi for login, accounts and trophy matchmaking, and a separate Windows x64 worker for authoritative Unity/Fusion battles.

Follow [FUSION-SPLIT-HOSTING.md](FUSION-SPLIT-HOSTING.md) for installation, worker configuration and Windows/Android testing. The Pi-only WebSocket prototype is not the selected deployment.

Use 64-bit Raspberry Pi OS (`aarch64`). The installer generates `EMBERWAKE_WORKER_KEY` for a new installation. Existing `/etc/emberwake.env` files are preserved: update their transport to `fusion`, enable `EMBERWAKE_REMOTE_WORKER=1`, set the region, and add a worker key before restarting. Without a connected worker, ranked battles are unavailable.
