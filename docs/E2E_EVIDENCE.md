# End-to-End Evidence

Date: 2026-07-14

Status: **beta.3 superseded; beta.4 listener acceptance pending**

The earlier beta.3 `PASS` claim covered plugin loading, automatic WAVE generation,
cache reuse, and native YMM4 lip-sync frames, but did not require an
intelligibility or listening gate. That completion claim is withdrawn. This
document records the beta.4 quality rework without treating file creation as
proof that the voice is usable.

No test or package input contains a VOCALOID voicebank, character image,
credential, or authentication data. All renders use the user's installed
`HATSUNE_MIKU_V6_ORIGINAL` voicebank.

## Environment

| Component | Verified version |
| --- | --- |
| Windows 11 | build 26200 |
| .NET SDK | 10.0.301 |
| YMM4 | 4.54.0.1 |
| VOCALOID6 Editor | 6.12.0.1, Japanese UI |
| HATSUNE MIKU V6 | 6.12.0, `HATSUNE_MIKU_V6_ORIGINAL` |
| ASR audit | Whisper medium, Japanese |

## beta.4 defects reproduced and fixed

- The beta.3 planner used a periodic pitch pattern, four mora per second, and a
  gap after every note. It sounded sung and segmented rather than conversational.
- VOCALOID6 could accumulate imported bridge parts across requests, allowing a
  previous request to affect a later export.
- A pointer click reported style-selection success while the actual selected
  item remained `No Effect*`.
- One structurally valid 3.923-second WAVE contained only approximately -91 dB
  signal. The old CLI accepted it because it checked RIFF structure, not speech
  activity.

The driver now verifies bridge-project ownership, starts each render from a
fresh project, preserves the imported ORIGINAL style, verifies custom style and
Take selections through `SelectionItemPattern`, and rejects silent or
below-floor audio before returning success.

## Intelligibility audit

The final candidate uses 130 BPM, 160 ticks per mora, connected notes, flat
statement pitch, a one-semitone question ending, a closed-mouth timing gap for
sokuon, and VOCALOID:AI `Take10`.

Ten separate real VOCALOID6 renders of the same difficult sentence were made,
one for each Take. Takes 1 through 9 were recognized as
`ま、ではもう一度ゆっくり確認します`. Take 10 alone was recognized exactly:

```text
Expected: 待って、もう一度ゆっくり確認します。
Take10:   待って、もう一度ゆっくり確認します
```

Cross-sentence checks found that Take 10 is an improvement, not a complete TTS
substitute:

```text
Expected: 本当にこれで大丈夫？うん、ちゃんと聞こえているよ。
ASR:      本当は鬼これて大丈夫 / うん、ちゃんと聞こえているよ

Expected: こんにちは、初音ミクです。今日は一緒に話そうね？
ASR:      こんやちははずねみくです / 今日は一緒に話そうね
```

These remaining consonant and proper-name errors are recorded rather than
reported as a pass. The generated samples must be listened to in the installed
YMM4 flow before beta.4 can be called complete.

## Packaged YMM4 verification

The clean source commit `22bab9f5d0ff25619deb56a1ff3ebfef17751d94` was
packaged as `0.1.0-beta.4`. Both the installed plugin and bundled self-contained
CLI report that revision in `ProductVersion`. The bundled doctor reported
`ready: true` for YMM4 4.54.0.1, VOCALOID6 Editor 6.12.0.1, and HATSUNE MIKU V6
6.12.0.

The real YMM4 project rendered `こんにちは、初音ミクです。` through the installed
plugin with no assisted fallback:

| Measurement | Result |
| --- | --- |
| Driver | `Vocaloid6AutomationDriver` |
| Assisted fallback | `false` |
| Sequence / lip sync | 12 notes / 15 native YMM4 frames |
| Active audio | 110 ms through 2,125.0113 ms |
| Format | PCM signed 16-bit, stereo, 44.1 kHz |
| Output size | 374,896 bytes |
| SHA-256 | `2AE76DFBAA2025585FD29E9F0705DF53934DBB462E43E5885CD1E5B316C94455` |
| Whisper medium | `こんやちははずね肉です` |

After restarting YMM4 with the same project, the beta.4 cache restored the same
audio and all 15 lip-sync frames without another VOCALOID6 render. YMM4 was left
open and responsive; the bridge-owned VOCALOID6 project was closed afterward.

The package contains 20 allow-listed files and no YMM4 assemblies, VOCALOID
binaries, voicebanks, images, credentials, generated audio, MIDI, or local user
paths. Package size is 78,771,860 bytes and SHA-256 is
`3F5EF2AFF18922A441A3C0421986A1ACFCDE5851457874C71E2F355F1FFCBDD3`.

## beta.5 robot-speech timing verification

The product target changed from natural conversation and lip-sync-first output
to standalone Hatsune Miku robot speech, with YMM4 retained as an optional
integration. The standalone `speak` path and the YMM4 speaker now use the same
`RobotSpeechSequencePlanner`.

An initial robot-speech candidate used 240 ticks per mora at 120 BPM. A real
automatic VOCALOID6 render of `こんにちは、初音ミクです。` completed without
assisted fallback, but its 3.675011-second duration was rejected by listener
feedback as syllable-by-syllable elongation.

The corrected default uses 144 ticks per mora at 120 BPM, a 12-tick note gap,
minimal word-boundary spacing, and only small duration extensions for `ン` and
`ツ`. The same real automatic render then produced:

| Measurement | Result |
| --- | --- |
| Driver | `Vocaloid6AutomationDriver` |
| Assisted fallback | `false` |
| Duration | 2.098957 seconds |
| Format | PCM signed 16-bit, stereo, 44.1 kHz |
| Output size | 370,300 bytes |
| Default pitch | Fixed MIDI note 64 |

The 3.675011-second candidate is not a release artifact. Unit coverage now
enforces a default rate of 5.5 to 7.5 sounding mora per second so the elongated
timing cannot silently return.

## Current automated evidence

| Gate | Result |
| --- | --- |
| Unit tests | 33/33 passed |
| Full Release build | Passed, 0 warnings, 0 errors |
| Package boundary | Passed, 20/20 files allow-listed |
| Installed YMM4 automatic render | Passed mechanically, no assisted fallback |
| YMM4 restart/cache/lip sync | Passed, 15 frames restored |
| Distinct Take outputs | 10/10 unique SHA-256 values |
| Silent-output rejection | Passed in unit tests and real driver path |
| Claude Sonnet beta.4 review | No high- or medium-severity actionable finding |
| Gemini CLI | Blocked by `UNSUPPORTED_CLIENT`; not counted as passing |

The final listener verdict remains intentionally pending. Mechanical rendering
and ASR evidence are not a substitute for the user's quality acceptance.

## Compatibility boundary

Evidence applies only to the exact environment above at the machine's active
display scaling. Other YMM4 or VOCALOID6 versions, UI languages, and DPI values
remain community compatibility targets.
