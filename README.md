# This Ain't Moon Lander

<p align="center">
  <img src="Assets/Publishing/Feature-Graphic.png" width="960" alt="This Ain't Moon Lander">
</p>

<p align="center"><strong>A lunar-landing prototype that combines piloting, EVA exploration and landing challenges.</strong></p>

<p align="center">
  <img src="https://img.shields.io/badge/Engine-Unity%206-222c32?logo=unity&logoColor=white" alt="Unity 6">
  <img src="https://img.shields.io/badge/Genre-Lunar%20Landing-5b8def" alt="Lunar landing">
  <img src="https://img.shields.io/badge/Status-On%20hold-f0ad4e" alt="On hold">
</p>

## About

**This Ain't Moon Lander** is a lunar-landing prototype that combines piloting, EVA exploration and landing challenges.

## Highlights

- **Choose a lander:** Select and pilot different lander types.
- **Explore on foot:** Leave the craft and move across the lunar surface.
- **Master the landing:** Gravity, landing-pad placement and scoring reward control and planning.
- **Persistent story prototype:** Story and local save support are included.
- **Station radio:** Registration issues a portable radio with saved messages, optional video portraits, short replies and selectable signal tracking.

## Technical details

- **Engine:** Unity `6000.0.59f2`
- **Status:** On hold

## Game concept

The detailed story, progression and long-term vision are documented in [GAME_CONCEPT.md](GAME_CONCEPT.md).

## Run locally
1. Clone the repository.
2. Open it in Unity Hub with the listed Unity version.
3. Open a scene in `Assets/Scenes` and press Play.

## Android release workflow

[Unity Android Release Tools](https://github.com/WRB-Studio/unity-android-release-tools)
is integrated from `origin/main`, commit `6d4c1fc9eda2a2ba1e2df63bae2133fd63cbd438`.
The scripts and `Assets/Editor/UnityAndroidBuild.cs` must be updated together.
Follow the upstream [integration guide](https://github.com/WRB-Studio/unity-android-release-tools/blob/main/INTEGRATION.md)
and [update guide](https://github.com/WRB-Studio/unity-android-release-tools/blob/main/UPDATING.md).

Project configuration is in `scripts/release.config.json`. Signing credentials and
the shared Play service-account file are referenced by encrypted local settings
under `%LOCALAPPDATA%/UnityAndroidRelease/com-WRBStudio-ThisAintMoonLander/`.
Keep credentials and personal paths outside Git. Save and close this project's
Unity Editor before building. The scripts read the Unity version from the project.

```powershell
# Local workflow checks; no build or upload:
./scripts/Test-ReleaseWorkflow.ps1

# Build a signed APK locally:
./scripts/Build-Apk.ps1

# Check Play access and the next version code; no build or upload:
./scripts/Build-AabAndSubmitToPlay.ps1 -CheckOnly

# Build and upload to the internal test track when requested:
./scripts/Build-AabAndSubmitToPlay.ps1 -Track internal
```

Drive export requires a separately configured local destination via
`scripts/Set-DriveExportDirectory.ps1`. See the upstream
[README](https://github.com/WRB-Studio/unity-android-release-tools/blob/main/README.md)
for APK/AAB export and Production release commands. Copying to a synchronized
Drive folder may trigger cloud synchronization. Git pushes do not publish releases.
