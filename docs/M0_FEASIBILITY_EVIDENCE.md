# M0 Feasibility Evidence

Date: 2026-07-10

Status: **PASS**

## Tested environment

| Component | Version |
| --- | --- |
| Windows | Windows 11, build 26200 |
| .NET SDK | 10.0.301 |
| .NET runtime used by YMM4 | 10.0.9 |
| YMM4 | 4.54.0.1 portable release |
| VOCALOID6 Editor | 6.12.0.1 |
| HATSUNE MIKU V6 | 6.12.0 |

The CLI `doctor` command detected all required products and returned `ready: true` in this environment.

## MIDI to Miku WAV proof

An original Type 1 Standard MIDI file was generated with 480 PPQ, a tempo track, five notes, and UTF-8 lyric meta events. It was imported into VOCALOID6, mapped to `HATSUNE_MIKU_V6_SOFT`, and exported with Audio Mixdown.

The proof files are intentionally not committed because generated media and product projects are excluded from the repository.

| Evidence | Result |
| --- | --- |
| MIDI SHA-256 | `7EB893BA7038DBB943F1E14821454A151D865F0D1BFE72122FDC1E2F85BD6350` |
| WAV SHA-256 | `79186AB85F03600B448FDCCBFF87E5391E9D2D741FB975FC4E8F14BCC9B46CAA` |
| WAV format | 44.1 kHz, stereo, 16-bit PCM |
| WAV duration | 2.4375 seconds |
| WAV size | 430,020 bytes |
| Signal check | RMS 687, peak 3705; non-silent |

## Public automation surface

VOCALOID6 exposes stable Windows UI Automation elements in the tested Japanese UI:

| Screen/control | Automation ID |
| --- | --- |
| Home window | `xHomeWindow` |
| Main editor window | `xMainWindow` |
| Add Track dialog | `xAddTrackDlg` |
| VOCALOID:AI track button | `xAiTrackButton` |
| Voicebank selector | `xVoiceBankComboBox` |

The implementation uses these IDs, standard menu item names, and standard Windows file dialogs. It does not use process memory access or private project formats. A failure at any automatic step falls back to the same generated MIDI in assisted mode.

## YMM4 integration decision

Current YMM4 exposes `IVoicePlugin`, `IVoiceSpeaker`, and `IVoicePronounce.LipSyncFrames`. The bridge therefore returns mouth timing through the public voice plugin API. `.lab` is also generated as a portable sidecar and diagnostic artifact, but a post-processing plugin is not required.

## Adopted decisions

- Default mode: assisted MIDI import and WAV export.
- Optional mode: Windows UI Automation with automatic assisted fallback.
- Reading: Apache Lucene.NET Kuromoji with its packaged dictionary.
- Exchange formats: Type 1 Standard MIDI with UTF-8 lyric events and PCM WAV.
- Lip sync: native YMM4 `LipSyncFrames`, also exported as `.lab`.
- Cache: SHA-256 of normalized text, synthesis options, cache schema, and VOCALOID version.

## M0 exit gate

- Real Miku V6 WAV generation: passed.
- Deterministic output path and validation: passed.
- Assisted mode: adopted as default.
- Automatic mode: adopted with fallback.
- Public lip-sync integration: confirmed.
- Distribution boundary: documented; no product binaries or character assets are redistributed.
