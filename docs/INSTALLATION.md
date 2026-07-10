# Installation and First Run

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
4. Select `初音ミク V6` under the `VOCALOID6 Bridge` voice engine.

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

`READY` means YMM4, VOCALOID6 Editor, and HATSUNE MIKU V6 were detected. Add `--json` for machine-readable output.

Add `--ui` to open the desktop diagnostic window:

```powershell
.\user\plugin\YMM4VocaloidBridge\tools\YMM4VocaloidBridge.Cli.exe doctor --ymm4-dir . --ui
```

## First render

The default `Assisted` mode creates a lyric MIDI and opens VOCALOID6 plus an instruction file. Import the listed MIDI, select `HATSUNE_MIKU_V6_SOFT`, then use Audio Mixdown to save the WAV to the exact listed path. YMM4 resumes when the validated WAV appears.

After assisted mode works, change `連携モード` to `Automatic`. If a supported UI element is unavailable, the same request returns to assisted mode without discarding its MIDI.

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
