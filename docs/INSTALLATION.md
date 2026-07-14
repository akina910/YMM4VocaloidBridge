# YMM4 Plugin Installation and First Run

## Requirements

- Windows 11
- Current YMM4 with .NET 10 support
- VOCALOID6 Editor 6.12.0 or later
- A licensed local installation of HATSUNE MIKU V6

No editor, voicebank, license, account data, or character asset is included in this plugin.

The bundled doctor CLI is self-contained; it does not require a separate global .NET installation.

## Install

1. Close YMM4.
2. Double-click `YMM4VocaloidBridge.v.<version>.ymme` and approve the YMM4 plugin installation.
3. Start YMM4.
4. Select `初音ミク V6 ORIGINAL` under the `VOCALOID6 Bridge` voice engine.

For a manual portable install, extract the package and place its `YMM4VocaloidBridge` folder under:

```text
<YMM4 directory>\user\plugin\YMM4VocaloidBridge
```

## Diagnose

Run the installed doctor before the first render:

```powershell
cd "<YMM4 directory>"
.\user\plugin\YMM4VocaloidBridge\tools\YMM4VocaloidBridge.Cli.exe doctor --ymm4-dir .
```

`READY` means VOCALOID6 Editor and HATSUNE MIKU V6 were detected. YMM4 is reported separately as an optional integration. Add `--json` for machine-readable output.

Add `--ui` to open the desktop diagnostic window:

```powershell
.\user\plugin\YMM4VocaloidBridge\tools\YMM4VocaloidBridge.Cli.exe doctor --ymm4-dir . --ui
```

## First render

The default `Automatic` mode imports the generated lyric MIDI, selects `HATSUNE_MIKU_V6_ORIGINAL`, applies the verified `Take10` timing candidate, and requests Audio Mixdown through VOCALOID6. If a supported UI element is unavailable, the same request falls back to `Assisted` without discarding its MIDI.

In `Assisted`, import the listed MIDI, select `HATSUNE_MIKU_V6_ORIGINAL`, then use Audio Mixdown to save the WAV to the exact listed path. YMM4 resumes when the validated WAV appears.

## Configure your moving standing image

The plugin supplies native YMM4 `A / I / U / E / O / Silent` mouth timing. It does not copy or modify the user's standing-image files.

1. Prepare an `動く立ち絵` asset with a `口` folder. For vowel lip sync, the selected mouth part must include `name.a.png`, `name.i.png`, `name.u.png`, `name.e.png`, `name.o.png`, `name.0.png` (closed), and `name.png` (thumbnail/open).
2. Open the character editor below the YMM4 timeline and select the character that uses this plugin.
3. Under `立ち絵`, set `種類` to `動く立ち絵`, select the asset folder, and select the default parts for the standing-image item and voice-item expression.
4. Add a required `立ち絵アイテム` to the timeline with the toolbar's person icon. A voice item alone does not display the standing image.
5. Add a voice item using `VOCALOID6 Bridge / 初音ミク V6 ORIGINAL`. During preview, YMM4 applies the returned vowel frames to the mouth part selected by the voice item's expression.

YMM4's official asset and setup references:

- [動く立ち絵素材の作り方](https://manjubox.net/ymm4/faq/%E7%AB%8B%E3%81%A1%E7%B5%B5%E6%A9%9F%E8%83%BD/%E5%8B%95%E3%81%8F%E7%AB%8B%E3%81%A1%E7%B5%B5%E7%B4%A0%E6%9D%90%E3%81%AE%E4%BD%9C%E3%82%8A%E6%96%B9/)
- [動く立ち絵の設定方法](https://manjubox.net/ymm4/faq/%E7%AB%8B%E3%81%A1%E7%B5%B5%E6%A9%9F%E8%83%BD/%E5%8B%95%E3%81%8F%E7%AB%8B%E3%81%A1%E7%B5%B5%E3%81%AE%E8%A8%AD%E5%AE%9A%E6%96%B9%E6%B3%95/)
- [立ち絵が表示されない](https://manjubox.net/ymm4/faq/%E7%AB%8B%E3%81%A1%E7%B5%B5%E6%A9%9F%E8%83%BD/%E7%AB%8B%E3%81%A1%E7%B5%B5%E3%81%8C%E8%A1%A8%E7%A4%BA%E3%81%95%E3%82%8C%E3%81%AA%E3%81%84/)

After a render, the bridge analyzes the completed WAVE file, rejects silent output, aligns the planned vowel timeline to the detected speech start/end, and applies the configured `口パク先行` value (default 33 ms).

## Data locations

Generated work files, cache, and privacy-filtered event logs are stored under:

```text
%LOCALAPPDATA%\YMM4VocaloidBridge
```

Logs contain event names, versions, counts, and exception types. They do not contain dialogue text, serial numbers, or authentication data.

## Update

Close YMM4 and install the newer `.ymme` over the existing plugin. Cached WAV files remain compatible only when the cache schema, text, options, and VOCALOID version match.

## Uninstall

Close YMM4 and remove:

```text
<YMM4 directory>\user\plugin\YMM4VocaloidBridge
```

To remove generated data too, delete `%LOCALAPPDATA%\YMM4VocaloidBridge`.

## Troubleshooting

| Symptom | Action |
| --- | --- |
| Voice is not listed in YMM4 | Confirm the plugin folder contains `YMM4VocaloidBridge.Plugin.dll`, then restart YMM4. |
| Doctor reports missing editor | Install VOCALOID6 Editor or set `VOCALOID6_EDITOR` to its executable path. |
| Doctor reports missing voicebank | Install and activate HATSUNE MIKU V6 normally, then rerun doctor. |
| Automatic mode returns to assisted mode | Follow the generated instruction file; report the VOCALOID6/YMM4 versions and event log without attaching licensed files. |
| Render times out | Export to the exact requested WAV path before the configured timeout. |
| Cached result is unwanted | Delete `%LOCALAPPDATA%\YMM4VocaloidBridge\cache`. |
| Audio exists but the mouth does not move | Confirm the mouth part includes `.a` through `.o` plus `.0`, the voice item's expression selects that mouth part, and a standing-image item exists on the timeline. |
