# Replacing the placeholder fleet

Use `Client/Assets/_Game/Resources/Art/card-atlas.png` and the active v2 design boards as visual references. The twelve illustrated cards define the ship silhouettes and unit roles. Do not treat the marketing icon as a detailed production model specification.

## Model handoff

Generate each ship independently in Tripo. Keep the ivory sail, walnut hull and teal/bronze trim readable from a high three-quarter camera. Avoid tiny rigging or sculpted foam. Export a game-ready mesh with separate hull, sail, cannons and trim materials where possible. Start with a 3–8k triangle budget per ship; this is a target that still needs device profiling, not a measured limit.

Import to Unity with URP-compatible materials. A root at scale 1 should face positive Z, sit at waterline Y=0, and fit roughly 0.8–1.3 units wide and 1.1–2.5 units long depending on class. Add no colliders, Rigidbody or gameplay scripts: simulation is independent of meshes. Save a prefab. Team coloring for externally generated prefabs is not automated yet, so include neutral trim until a material-binding convention is added.

Switch Unity's build target to Android or Windows, select the saved prefab, and use **Emberwake → Export selected ship prefab**. This writes a bundle and integrity metadata. Upload to your HTTPS CDN, then add to the card's catalog entry:

```json
"modelBundles": [
  {"platform":"Android","url":"https://your-cdn/cutter-android.bundle","sha256":"REPLACE_WITH_64_CHARACTER_SHA256","asset":"Cutter"},
  {"platform":"Windows","url":"https://your-cdn/cutter-windows.bundle","sha256":"REPLACE_WITH_64_CHARACTER_SHA256","asset":"Cutter"}
]
```

Use each export's actual digest and asset name. The example hashes deliberately fail validation until replaced. A new `artUrl` can point to individual HTTPS card art. Increase catalog version, validate and publish in the operations console. Reconnect the game to load the release. Missing/bad bundles retain placeholder models. Build and test bundles separately for each platform; they are not interchangeable.

## What backend content can change

Names, descriptions, art, cost, health, damage, speed, range, ship count, splash, unlock trophies, enabled state, supported behavior selection, visual models, season text and gold offers. Returning players unlock newly eligible cards at sign-in.

New executable abilities cannot be shipped through a data catalog; add their simulation and presentation code to a client/server release. Real-money DLC needs verified purchase entitlements and restoration before it can be sold. Gold cosmetics are the only implemented shop transaction. A remote model must be verified on-device before its catalog release; automated prefab safety inspection and live CDN bundle tests are not yet implemented.
