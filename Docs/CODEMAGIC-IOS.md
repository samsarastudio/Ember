# iOS release build in Codemagic

## One-time setup

1. Add `samsarastudio/Ember` in Codemagic and use the repository's `codemagic.yaml`. Select `ios-release` on `main`.
2. Configure the `unity_credentials` environment group with secret values `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_SERIAL`. This workflow uses Codemagic's documented serial-based Unity license activation. Supply a license that permits this CI activation method. If using a Unity license without serial-based CI activation, arrange the supported licensing method first; do not put a license file or credentials in Git.
3. In Apple Developer / App Store Connect, register `com.emberwake.fleetduel`. In Codemagic Team settings > Code signing identities, upload an Apple Distribution certificate (.p12, with its password) and matching App Store provisioning profile, or fetch them through your configured App Store Connect integration. These require Apple Developer membership.
4. The workflow selects matching profiles with `ios_signing`, exports Unity on a macOS M2 worker, applies signing and generates the IPA. Xcode is selected from the Codemagic image. Unity is pinned to 6000.2.6f2; confirm the version/modules are available in your Codemagic environment.
5. Download the IPA and build logs from artifacts. An App Store-signed IPA is for App Store Connect/TestFlight, not arbitrary direct installation. Upload using your normal App Store Connect process. No automatic store publication is enabled by this repository.

## App configuration

- Product: Emberwake Fleet Duel; version 0.2.0.
- iOS bundle ID: com.emberwake.fleetduel; minimum iOS 15.
- ARM64 device export through IL2CPP, non-development build.
- API is fixed to https://royale.inmomentservices.com; no user URL entry or release command-line override.
- Build number: Codemagic BUILD_NUMBER + 2, passed as EMBERWAKE_BUILD_NUMBER. If a larger build number already exists in App Store Connect, raise the offset before uploading.
- Unity activation is returned during workflow publishing cleanup. Manually cancelled runs may require releasing the seat in your Unity account.

## Validation limits

Windows can prepare/export the Unity project when the iOS module is installed, but cannot run Xcode, sign an IPA, or test an iPhone. A successful local export is not a successful Codemagic build. The first cloud run must validate the account's Unity licensing, signing files, Xcode toolchain and real device compatibility.

Email delivery, privacy/terms URLs, secure device credential storage and production backend availability still need release validation. The existing server is a Windows-hosted pilot behind Cloudflare; it must remain awake and connected.

Official references: https://docs.codemagic.io/yaml-quick-start/building-a-unity-app/ and https://docs.codemagic.io/yaml-code-signing/signing-ios/.
