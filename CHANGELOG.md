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

### Known limitations

- Initial compatibility target is Windows 11, Japanese VOCALOID6 6.12, HATSUNE MIKU V6, and current YMM4.
- Automatic mode requires a dedicated VOCALOID6 bridge project. A project with a named non-bridge track falls back to assisted mode.
- Imported bridge tracks remain in the unsaved VOCALOID6 project during a batch. Restart VOCALOID6 without saving to clear them.
- Speech prosody is deterministic and intentionally simple in this beta.
