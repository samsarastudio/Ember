# Stylized water implementation

The chosen approach is a purpose-built Unity 6.2 / URP 17.2 water surface for the fixed, high-angle naval arena. There is no universal best water shader: here, readable miniature ships and restrained motion matter more than a realistic ocean simulation. The user's supplied target is saved at `Design/Reference/water-style-target.png`: quiet teal facets, stepped turquoise shores, pale contact foam, and narrow broken ship wakes. The old voyage/joystick UI in that image does not change the card-battler game design.

## Research and decisions

- [Unity world position reconstruction](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/writing-shaders-urp-reconstruct-world-position.html): reconstruct the opaque scene position from depth and inverse view-projection. This avoids perspective-only eye-depth formulas with our orthographic camera. Water depth is the vertical distance from the surface to the opaque scene.
- [Unity URP asset reference](https://docs.unity3d.com/6000.2/Documentation/Manual/urp/universalrp-asset.html): depth and opaque color textures are separate requirements. The default needs depth only; optional refraction requests the opaque color copy.
- [Catlike Coding wave tutorial](https://catlikecoding.com/unity/tutorials/flow/waves/): wave displacement and the surface normal need to agree. We use two gentle directional sine waves and derivative-based faceted normals. No simulation or buoyancy is added to the server.
- [Catlike Coding water depth discussion](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/): use depth to drive absorption and submerged color. Its older built-in-pipeline implementation is not copied into URP.
- [Stylized Water performance guidance](https://alexander-ameye.gitbook.io/stylized-water/support/performance): water should be evaluated on actual target devices. A purchased general-purpose package is not necessary for this arena's specific feature set.
- [Unity shader variant stripping](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/shader-troubleshooting/shader-reducing/shader-variants/shader-variant-stripping): runtime-controlled variants must remain in the build.

## Included

Deep teal to shallow turquoise absorption with six stylized depth bands; submerged shelves around rocks and harbor foundations; broken animated ivory contact foam; two-direction vertex swell; broad faceted lighting; small moving shallow-water caustic patterns; restrained sun highlights and Fresnel tint; directional light shadows; pooled directional broken wake trails; deployment ripples; projectile-end and destroyed-ship splash rings; optional guarded screen-color refraction. All are original procedural shader/code effects, requiring no imported water textures or licensed package.

Files: `Client/Assets/_Game/Resources/Water/StylizedSea.shader`, `Foam.shader`, and `Client/Assets/_Game/Runtime/StylizedWater.cs`. `ArenaView` wires the effect to the existing client snapshots. The previous teal cube and static glint cubes are removed.

## Controls and limits

Edit `Client/Assets/_Game/Resources/Water/Sea.mat` to tune deep/shallow/foam colors, absorption depth, foam width, wave height/speed, facet strength and caustic strength. Runtime instances clone this material. Wave height is limited to 0.095 with the current fixed foam-mesh height to avoid intersections with crests. Default height is 0.085. Water level is -0.18; gameplay deployment remains on the original Y=0 plane. Do not change simulation coordinates to fit visual waves.

Depth copying must run **After Opaques**, before the transparent water pass. `BuildGame.Setup` persists this on the renderer and enables the URP depth texture. Copying after transparents produces a uniform deep sea with missing contact foam. The highest seabed shelf is -0.60, safely below wave troughs. The refraction toggle is compiled using a local multi-compile so runtime enabling survives build stripping.

`SetRefraction(camera, true)` enables the optional color-copy path. It rejects distorted samples whose reconstructed position lies above water. Default remains off. This is a screen-space approximation, not physical transmission or reflection. Fresnel is an artistic tint, not a reflected scene. Caustics are a surface color approximation, not light projected onto underwater meshes.

Two 112-by-112 grids (25,088 triangles each) supply the sea and seabed. The foam pool is capped at 192 quads in a single dynamic mesh (384 triangles), with no per-frame object spawning. Its draw geometry is bounded independently of fleet size. Meshes and private materials are released on destruction. New matches clear transient effects. Ships teleporting more than five units reset their wake history.

No planar-reflection camera, FFT ocean, tessellation, SSR, underwater camera, dynamic fluids, or GPU readback is used. Those costs do not serve this portrait above-water card battler. Mobile GPU timings and thermal behavior still require device profiling; a desktop capture is not proof of a mobile frame-rate target.

## Visual verification

Launch a development Windows build with `--water-capture=ABSOLUTE_DIRECTORY`. The offline showcase creates the actual arena, moving ships, depth-only water, impact rings, and an optional refraction frame. It saves four PNGs and exits. It requires a graphics device (do not pass `-nographics`). This harness does not require or mutate a backend account.

Verified on 2026-10-13: Unity 6000.2.6f2 completed both Windows and Android development builds without C# or shader compilation errors. The final Windows build completed the offline capture harness without reported exceptions; arena, detail, and refraction captures were visually inspected. Captures are in `Builds/Water/`. The Android APK passed `apksigner verify` with a valid v2 signature. No Android device was connected, so device rendering, GPU frame times, and thermal behavior remain unverified. These are development previews, not store-release packages. The existing primitive ship and rock placeholders remain.
