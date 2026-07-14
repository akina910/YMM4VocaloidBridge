# Changelog

## Unreleased

### Added

- Start VOCALOID6 in the background when the YMM4 current character changes to the bridge's Hatsune Miku voice.
- Add a standalone `MikuRobotSpeech.exe` package and an interactive double-click workflow that writes WAV files to the desktop.
- Add a robot-speech sequence planner with fixed MIDI note 64, explicit mora gaps, word boundaries, sokuon silence, punctuation pauses, and 50-200% speech-rate control.
- Allow CLI work data to be redirected with `YMM4_VOCALOID_BRIDGE_DATA_DIR`.
- Preserve punctuation across YMM4's custom-reading round trip so pauses and question endings reach synthesis.
- Analyze rendered WAVE activity, reject silent output, align native YMM4 vowel frames, and expose a configurable lip-sync lead.
- Add question-ending and sokuon-aware dialogue prosody rules.
- Add moving-standing-image setup instructions and stronger package provenance/boundary checks.
- Expose VOCALOID:AI Take 1 through 10 and default to Take 10 after a real ten-take intelligibility comparison.
- Persist CLI automation failures locally while redacting dialogue passed through `--text`.

### Changed

- Make the robot-speech planner the shared default for the standalone CLI and YMM4 plugin.
- Set the default timing to about 150 ms per mora; a real greeting render is 2.099 seconds instead of the rejected 3.675-second elongated candidate.
- Raise the default robot-speech rate from 100% to 125% after listener feedback, targeting about 120 ms per mora.
- Require a verifiable HATSUNE MIKU V6 ORIGINAL selection during automatic project setup or MIDI import.
- Remove release debug paths and force clean, source-revision-stamped packaging.
- Replace the periodic melody with connected, flat statement notes and a one-semitone question ending.
- Represent sokuon as a closed-mouth timing gap instead of asking VOCALOID6 to sing an independent `ッ` note.
- Preserve the imported ORIGINAL style instead of reapplying a style preset to the generated track.
- Restart only a verified bridge-owned VOCALOID6 project before each render so prior parts cannot leak into later dialogue.

### Fixed

- Decline only the identified VOCALOID6 unlicensed-Voice authorization prompt so unrelated installed Voices do not block Miku startup.
- Reject silent automatic renders instead of treating a structurally valid WAVE file as successful output.
- Select custom styles and takes through UI Automation selection patterns and verify the resulting selection.
- Dismiss only identified update and session-recovery prompts, and never close a project containing a named non-bridge track.

### Known limitations

- Speech prosody is deterministic and intentionally restrained; VOCALOID6 remains a singing engine rather than a dedicated TTS engine.

All notable changes to this project are documented in this file.

## 0.1.0-beta.2 - 2026-07-13

### Fixed

- Publish cache restores and YMM4 output files through same-directory temporary files and atomic replacement.
- Preserve existing completed output when copying is cancelled or fails.
- Handle cache read/write races with delete-sharing and transient Windows destination-lock retries.
- Keep cleanup failures from masking the original render or cancellation exception.

### Verification

- Claude and GitHub Copilot independent reviews report no remaining P0/P1 findings.
- Added cancellation, cache-miss, concurrent store/restore, `.wav`, temporary-extension, and locked-destination tests.

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
- Speech prosody is deterministic and intentionally simple in this beta.
