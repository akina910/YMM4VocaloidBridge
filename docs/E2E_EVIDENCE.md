# End-to-End Evidence

Date: 2026-07-13

Status: **PARTIAL — core automatic render passed; full YMM4 standing-image workflow is not yet complete**

The evidence below proves the original beta automatic-render path and public YMM4 DLL contract. It does not prove installation, voice-item creation, and moving-standing-image preview inside the YMM4 executable. The beta.3 completion-gate changes also require a new ORIGINAL run because they alter punctuation handling, prosody, voicebank verification, rendered-WAVE validation, and lip-sync timing.

## Environment

| Component | Version |
| --- | --- |
| Windows 11 | build 26200 |
| .NET SDK | 10.0.301 |
| YMM4 | 4.54.0.1 |
| VOCALOID6 Editor | 6.12.0.1, Japanese UI |
| HATSUNE MIKU V6 | 6.12.0, `HATSUNE_MIKU_V6_ORIGINAL` |

## HATSUNE MIKU V6 ORIGINAL verification

One short Japanese dialogue (`初音ミクです。よろしくお願いします。`) was rendered through the complete automatic path with `HATSUNE_MIKU_V6_ORIGINAL` explicitly selected.

Result: **passed in 20.4 seconds with no assisted fallback**.

| Measurement | Result |
| --- | --- |
| Output size | 923,388 bytes |
| Format | RIFF/WAVE PCM, stereo, 44.1 kHz |
| SHA-256 | `5840963892DDEAA36822E05F764C6CAFDCF4381AC175920D6D1AB32625670A30` |

The run also verified automatic dismissal of VOCALOID6's optional update prompt, the child-text based `NEW PROJECT` control, the child-text based ORIGINAL voicebank entry, MIDI import, Solo isolation, Audio Mixdown, Solo restoration, and final WAVE validation.

## Automatic render soak baseline

Before the ORIGINAL target was fixed, twenty different short Japanese dialogues were rendered consecutively through the complete automatic path with `HATSUNE_MIKU_V6_SOFT`. This is retained as an automation stability baseline, not as ORIGINAL voicebank evidence:

1. Kuromoji reading and mora generation
2. deterministic dialogue note planning
3. Type 1 Standard MIDI with UTF-8 lyrics
4. VOCALOID6 MIDI import and VOCALOID:AI mapping
5. latest bridge track Solo isolation
6. Audio Mixdown to the requested absolute path
7. Solo restoration and PCM WAV validation

Result: **20/20 passed, 100%, no assisted fallback**. The run took 849.9 seconds. The M3 threshold was at least 95% over 20 consecutive cases.

| Measurement | Result |
| --- | --- |
| Validated WAV count | 20 |
| Minimum size | 430,020 bytes |
| Maximum size | 658,788 bytes |
| Total size | 10,496,688 bytes |
| Quietest peak | -3.8 dB |
| Loudest peak | 0.0 dB |
| First WAV SHA-256 | `1A6FCF9B829F95A0154EDFAC5F40847DECD744FC3369564A851C0600BF6ECD59` |
| Last WAV SHA-256 | `A24177588FF2ACD1DFEE1C2A9FBE94D20E563020E3105A1AE4A4FA340823B570` |

FFmpeg `volumedetect` confirmed that all 20 outputs were non-silent. Generated WAV, MIDI, LAB, and VOCALOID project data are excluded from Git and are not release assets.

## Lip sync

Thirty short Japanese dialogues are covered by a deterministic unit test. Each sequence starts and ends with a closed mouth, contains at least one vowel mouth shape, and produces identical notes and mouth frames on repeated runs. The YMM4 adapter maps these frames to public `YukkuriMovieMaker.Commons.LipSyncFrame` values.

## YMM4 plugin contract

The Release plugin was loaded in-process with the official YMM4 4.54.0.1 assemblies. The contract smoke test passed for plugin discovery metadata, the single HATSUNE MIKU V6 speaker, voice parameter creation, Japanese reading conversion, native YMM4 lip-sync frames, and pronounce cloning. The package doctor also reported YMM4 4.54.0.1, VOCALOID6 6.12.0.1, and HATSUNE MIKU V6 6.12.0 as ready.

This contract test does not open the downloaded YMM4 executable. Final confirmation that the voice appears in YMM4's in-app plugin list remains a first-launch verification step.

## Failure and recovery

- An unavailable or renamed UI element raises `VocaloidAutomationException` with the failed stage information.
- Automatic failure opens the assisted guide and waits for the same requested WAV path.
- Stale Windows file dialogs are dismissed at the next automatic request.
- The known optional VOCALOID6 update prompt is declined without changing update settings.
- A named non-bridge track prevents automatic modification and triggers assisted fallback.
- WAV files are accepted only after their size stabilizes and their RIFF/PCM structure is valid.

## Remaining beta matrix

The compatibility target above is verified at the machine's active display scaling. Additional YMM4/VOCALOID6 versions, UI languages, and DPI settings remain community test targets and must not be described as supported until evidence is added.
