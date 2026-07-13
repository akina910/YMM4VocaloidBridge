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

## Current automated evidence

| Gate | Result |
| --- | --- |
| Unit tests | 30/30 passed |
| Full Release build | Passed, 0 warnings, 0 errors |
| Distinct Take outputs | 10/10 unique SHA-256 values |
| Silent-output rejection | Passed in unit tests and real driver path |
| Claude Sonnet beta.4 review | No high- or medium-severity actionable finding |
| Gemini CLI | Blocked by `UNSUPPORTED_CLIENT`; not counted as passing |

The beta.4 package revision, package hash, installed YMM4 render, cache reuse,
and final listener verdict are intentionally left pending until the clean
source commit is packaged and installed.

## Compatibility boundary

Evidence applies only to the exact environment above at the machine's active
display scaling. Other YMM4 or VOCALOID6 versions, UI languages, and DPI values
remain community compatibility targets.
