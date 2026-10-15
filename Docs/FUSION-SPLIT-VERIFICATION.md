# Remote Fusion verification — 2026-10-14

- Windows player rebuilt successfully with Fusion 2.1.3, deployment feedback and development-only private-LAN API configuration.
- 88 logic, account and worker lifecycle checks pass. These cover worker ownership, readiness gating, invalid outcomes, both-player settlement and duplicate-result idempotency.
- US remote-worker duel passed: `Builds/RankedTests/5fde767334b54796abf0744f63d08e76`. Both clients: tick 1801, winner 0, hash -868229274; 166 attacks, 166 impacts and 155 damage events. Unauthorized worker polling and unknown assignments rejected; duplicate completion did not double-award.
- Canada East remote-worker duel with configured impairment and reconnect passed: `Builds/RankedTests/fa734af7f9fc4242aef6082e64bb0ce0`. Both clients: tick 1803, winner 0, hash 1639418519; 132 attacks, 129 impacts and 120 damage events. Same-account reconnect retained match/team.
- Deployment acknowledgment samples on this workstation: US n=19, median 83 ms, p95 116 ms; impaired Canada n=15 (post-reconnect logs), median 148 ms, p95 266 ms. These include command/server scheduling and are neither production measurements nor pure RTT. Clients and battle worker ran on one Windows PC while the Android compiler was active; the configured Photon region does not relocate that worker.
- Source launcher and standalone Windows package launcher both started the coordinator, remote worker and Windows client. LAN health endpoint at 10.0.0.135:5080 reported Fusion/rankedAvailable=true. Tested standalone launcher through Windows PowerShell 5 as well as the source launcher in PowerShell 7. Test processes were stopped afterward.
- Pi package cross-published for linux-arm64. Installer shell syntax checked. Actual Pi boot, SMTP, public DNS/HTTPS and external device reachability remain untested.
- Android APK build and metadata checks are recorded separately after compilation. User chose manual installation; no phone was connected, so no physical Android execution was performed.

This is a pilot with one coordinator and one worker, not production capacity or high-availability certification. See FUSION-SPLIT-HOSTING.md for restart semantics and operational limits.

Final Android build succeeded. APK metadata verified: com.emberwake.fleetduel, version 0.1.0/code 1, min SDK 26, target SDK 36, arm64-v8a. This does not substitute for the manual physical-phone test.
2026-10-15: Validated two distinct Windows captains against the user-running coordinator/worker on port 5080. Both joined room 07053a970be24c2bba4064a2bcb982ef, opposite teams; final tick 1803, winner 0, hash -1099944922, 155 attacks/151 impacts/114 damage events. Logs: Builds/Fusion-Windows-Worker-20261014/test-logs/two-windows-20261015-032217. Added an explicit two-client launcher to avoid shared Windows PlayerPrefs accounts.
