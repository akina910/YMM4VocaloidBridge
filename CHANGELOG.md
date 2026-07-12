# Changelog

All notable changes to this project are documented in this file.

## 0.1.0-beta.1 - 2026-07-10

### Added

- .NET 10 YMM4 voice plugin for HATSUNE MIKU V6.
- Kuromoji Japanese reading, mora splitting, deterministic dialogue pitch, and UTF-8 lyric MIDI generation.
- Assisted VOCALOID6 render path with an on-screen instruction file.
- Japanese VOCALOID6 6.12 UI Automation with automatic assisted fallback.
- Native YMM4 `LipSyncFrames` and `.lab` sidecar generation.
- PCM WAV validation and SHA-256 synthesis cache.
- CLI commands for environment diagnosis, artifact generation, synthesis, and UI inspection.
- Desktop `doctor --ui` diagnostic window.
- `.ymme` packaging, Windows CI, package verification, notices, and public contribution templates.

### Changed

- Default voicebank and YMM4 speaker label now target `HATSUNE_MIKU_V6_ORIGINAL`.
- Automatic mode is now the default YMM4 parameter; assisted fallback remains available.

### Fixed

- Render to an internal `.wav` before publishing to YMM4's temporary output extension.
- Handle the VOCALOID6 update prompt and nameless controls whose labels are exposed as child text.
- Launch VOCALOID6 against its installed .NET 8 runtime when the bridge itself runs under a private .NET 10 runtime.
- Preserve lip-sync pronounce data across Newtonsoft.Json project serialization.
- Report the failed automation stage and restore Solo without masking the original error.

### Known limitations

- Initial compatibility target is Windows 11, Japanese VOCALOID6 6.12, HATSUNE MIKU V6, and current YMM4.
- Automatic mode requires a dedicated VOCALOID6 bridge project. A project with a named non-bridge track falls back to assisted mode.
- Imported bridge tracks remain in the unsaved VOCALOID6 project during a batch. Restart VOCALOID6 without saving to clear them.
- Speech prosody is deterministic and intentionally simple in this beta.
