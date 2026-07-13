# End-to-End Evidence

Date: 2026-07-14

Status: **PASS for the supported public-beta environment**

This evidence covers the packaged plugin at source revision
`153e30d48190ded7657f366103c272207910c5c2`. The release gate uses only the
user-installed `HATSUNE_MIKU_V6_ORIGINAL` voicebank. No SOFT voicebank,
voicebank files, character images, or authentication data are release inputs.

## Environment

| Component | Verified version |
| --- | --- |
| Windows 11 | build 26200 |
| .NET SDK | 10.0.301 |
| YMM4 | 4.54.0.1 |
| VOCALOID6 Editor | 6.12.0.1, Japanese UI |
| HATSUNE MIKU V6 | 6.12.0, `HATSUNE_MIKU_V6_ORIGINAL` |

## Final packaged automatic render

The CLI embedded in the final `.ymme` rendered the dialogue
`外部レビュー修正後の初音ミクです。自動生成を確認するね！` through
VOCALOID6 automatic mode.

| Measurement | Result |
| --- | --- |
| Driver | `Vocaloid6AutomationDriver` |
| Assisted fallback | `false` |
| Output size | 1,628,988 bytes |
| Format | PCM signed 16-bit, stereo, 44.1 kHz |
| Duration | 9.234376 seconds |
| Mean / maximum level | -21.9 dB / -4.3 dB |
| SHA-256 | `6CDD931592DAD29441326D800CDDD7646588BF9F6439D91E18F92DC1CAEDC109` |

The run exercised the dedicated bridge project, MIDI import, explicit ORIGINAL
selection, optional style confirmation, bridge-track Solo isolation, Audio
Mixdown, Solo restoration, stable-file waiting, and RIFF/PCM validation.

## Real YMM4 plugin path

The final package was installed into YMM4's user plugin directory. The loaded
plugin DLL reports product version
`0.1.0-beta.3+153e30d48190ded7657f366103c272207910c5c2`.

The real YMM4 4.54.0.1 executable opened `user/debug-integration.ymmp`, loaded a
voice item whose API is `YMM4VocaloidBridge.Vocaloid6`, and restored the
generated audio through the plugin cache. The bridge emitted:

- `lip-sync-aligned`: 15 native YMM4 lip-sync frames
- active audio: 230 ms through 7,220 ms
- output duration: 7,234.3764 ms
- visual lead: 33 ms
- `cache-hit`: 15 frames restored without another VOCALOID render

The earlier uncached run of the same item emitted `render-complete` with
`driver=Vocaloid6AutomationDriver`, `UsedFallback=false`, 12 notes, 15 frames,
and editor version `6.12.0.1`.

These are public `YukkuriMovieMaker.Commons.LipSyncFrame` values, so any
compatible user-owned moving standing-image asset selected for that character
receives the normal YMM4 mouth animation. The project deliberately does not
bundle or copy such an asset; selecting one is user content configuration, not
part of the plugin package.

## Package and automated verification

| Gate | Result |
| --- | --- |
| Unit tests | 29/29 passed |
| Full Release build | Passed, 0 warnings, 0 errors |
| Package boundary | Passed, 20 allow-listed files |
| GitHub Actions CI | `build-and-test` passed |
| GitHub Actions package | `package` passed |
| Final package SHA-256 | `F718864344DD82DEA36DD3CC1EA8D8E84796E7BF4D263BF7732A883709936885` |

The package contains no YMM4 assemblies, VOCALOID binaries, voicebanks,
character images, credentials, generated WAV files, MIDI files, or local user
paths.

## Independent review

GitHub Copilot reviewed the pull request and found a package-scanner memory
issue, which was fixed. Claude Code 2.1.139 reviewed the complete branch, found
two UI-automation correctness risks, and then reviewed commit `153e30d` after
the fixes. Its follow-up result was: all four reported points addressed and no
new actionable regressions.

Gemini CLI 0.43.0 was attempted but returned `UNSUPPORTED_CLIENT` /
`IneligibleTierError`. It is recorded as blocked and is not counted as passing
review evidence.

## Compatibility boundary

The supported beta evidence is the exact environment listed above at the
machine's active display scaling. Other YMM4/VOCALOID6 versions, UI languages,
and DPI settings remain community compatibility targets rather than verified
support claims.
