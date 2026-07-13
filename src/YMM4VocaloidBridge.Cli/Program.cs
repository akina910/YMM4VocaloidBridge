using System.Diagnostics;
using System.Text.Json;

using YMM4VocaloidBridge.Automation;
using YMM4VocaloidBridge.Core;
using YMM4VocaloidBridge.Core.Audio;

return await BridgeCli.RunAsync(args).ConfigureAwait(false);

internal static class BridgeCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var options = CliArguments.Parse(args.Skip(1));
            return args[0].ToLowerInvariant() switch
            {
                "doctor" => RunDoctor(options),
                "inspect-ui" => RunInspectUi(options),
                "inspect-tracks" => RunInspectTracks(),
                "generate" => await RunGenerateAsync(options).ConfigureAwait(false),
                "synthesize" => await RunSynthesizeAsync(options).ConfigureAwait(false),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            WriteDiagnosticLog(args, exception);
            return 1;
        }
    }

    private static void WriteDiagnosticLog(IReadOnlyList<string> arguments, Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YMM4VocaloidBridge");
            Directory.CreateDirectory(directory);
            var entry = $"[{DateTimeOffset.Now:O}] command={string.Join(' ', RedactDialogue(arguments))}{Environment.NewLine}"
                + exception
                + Environment.NewLine;
            File.AppendAllText(Path.Combine(directory, "cli-errors.log"), entry);
        }
        catch
        {
            // Diagnostics must not replace the original CLI failure.
        }
    }

    private static IEnumerable<string> RedactDialogue(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            yield return index > 0
                && string.Equals(arguments[index - 1], "--text", StringComparison.OrdinalIgnoreCase)
                    ? "<redacted>"
                    : arguments[index];
        }
    }

    private static int RunDoctor(CliArguments arguments)
    {
        var report = new VocaloidInstallationDetector().Detect();
        var ymm4Directory = arguments.GetOptional("ymm4-dir")
            ?? Environment.GetEnvironmentVariable("YMM4_DIR");
        var ymm4Executable = string.IsNullOrWhiteSpace(ymm4Directory)
            ? null
            : Path.Combine(Path.GetFullPath(ymm4Directory), "YukkuriMovieMaker.exe");
        var ymm4Ready = ymm4Executable is not null && File.Exists(ymm4Executable);
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YMM4VocaloidBridge");
        Directory.CreateDirectory(appDataDirectory);

        var result = new DoctorResult(
            report.IsReady && ymm4Ready,
            Environment.OSVersion.VersionString,
            Environment.Version.ToString(),
            new Ymm4DoctorResult(
                ymm4Ready,
                ymm4Directory,
                ymm4Ready ? FileVersionInfo.GetVersionInfo(ymm4Executable!).FileVersion : null),
            report.Installation,
            report.Diagnostics,
            appDataDirectory);

        if (arguments.HasFlag("ui"))
        {
            DoctorWindow.ShowModal(result);
            return result.Ready ? 0 : 2;
        }

        if (arguments.HasFlag("json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));
        }
        else
        {
            Console.WriteLine(result.Ready ? "READY" : "NOT READY");
            Console.WriteLine($"[dotnet] {result.Dotnet}");
            Console.WriteLine($"[ymm4] {(ymm4Ready ? "OK" : "MISSING")} {result.Ymm4.Version ?? string.Empty}");
            foreach (var diagnostic in report.Diagnostics)
            {
                Console.WriteLine($"[{diagnostic.Code}] {(diagnostic.Success ? "OK" : "MISSING")} {diagnostic.Message}");
            }
        }

        return result.Ready ? 0 : 2;
    }

    private static async Task<int> RunGenerateAsync(CliArguments arguments)
    {
        var text = arguments.GetRequired("text");
        var outputDirectory = Path.GetFullPath(arguments.GetRequired("out-dir"));
        var options = CreateOptions(arguments);
        var artifacts = await SynthesisArtifactBuilder.CreateDefault()
            .BuildAsync(text, options, outputDirectory)
            .ConfigureAwait(false);

        Console.WriteLine($"reading={artifacts.Reading.Pronunciation}");
        Console.WriteLine($"notes={artifacts.Sequence.Notes.Count}");
        Console.WriteLine($"midi={artifacts.MidiPath}");
        Console.WriteLine($"lab={artifacts.LabPath}");
        return 0;
    }

    private static int RunInspectUi(CliArguments arguments)
    {
        var depth = arguments.GetInt("depth", 6);
        var elements = new VocaloidUiInspector().Capture(depth, openMenuName: arguments.GetOptional("menu"));
        Console.WriteLine(JsonSerializer.Serialize(elements, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
        return 0;
    }

    private static int RunInspectTracks()
    {
        Console.WriteLine(JsonSerializer.Serialize(new VocaloidUiInspector().CaptureTrackNames(), new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
        return 0;
    }

    private static async Task<int> RunSynthesizeAsync(CliArguments arguments)
    {
        var text = arguments.GetRequired("text");
        var outputPath = Path.GetFullPath(arguments.GetRequired("output"));
        var options = CreateOptions(arguments);
        var installation = new VocaloidInstallationDetector().Detect();
        if (!installation.IsReady || installation.Installation is null)
        {
            throw new InvalidOperationException("VOCALOID6 Editor or HATSUNE MIKU V6 is missing. Run doctor first.");
        }

        var workDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YMM4VocaloidBridge",
            "cli-work",
            Guid.NewGuid().ToString("N"));
        var artifacts = await SynthesisArtifactBuilder.CreateDefault()
            .BuildAsync(text, options, workDirectory)
            .ConfigureAwait(false);
        var waiter = new FileReadyWaiter(new WaveFileValidator());
        IVocaloidDriver assisted = new AssistedVocaloidDriver(waiter);
        var automatic = new Vocaloid6AutomationDriver(waiter);
        IVocaloidDriver driver = options.DriverMode switch
        {
            VocaloidDriverMode.Automatic when arguments.HasFlag("no-fallback") => automatic,
            VocaloidDriverMode.Automatic => new FallbackVocaloidDriver(automatic, assisted),
            _ => assisted,
        };
        var result = await driver.RenderAsync(
            new VocaloidRenderRequest(artifacts, options, outputPath, installation.Installation))
            .ConfigureAwait(false);

        Console.WriteLine($"driver={result.DriverName}");
        Console.WriteLine($"fallback={result.UsedFallback}");
        Console.WriteLine($"wave={result.OutputWavePath}");
        return 0;
    }

    private static BridgeOptions CreateOptions(CliArguments arguments)
    {
        var mode = arguments.GetOptional("mode")?.ToLowerInvariant() switch
        {
            "automatic" or "auto" => VocaloidDriverMode.Automatic,
            null or "assisted" or "manual" => VocaloidDriverMode.Assisted,
            var value => throw new ArgumentException($"Unknown mode: {value}"),
        };

        return new BridgeOptions
        {
            DriverMode = mode,
            TempoBpm = arguments.GetInt("tempo", BridgeOptions.DefaultTempoBpm),
            BaseNote = arguments.GetInt("base-note", 60),
            TimeoutSeconds = arguments.GetInt("timeout", 300),
            VoicebankName = arguments.GetOptional("voicebank") ?? BridgeOptions.DefaultVoicebankName,
            VoiceStyleName = arguments.GetOptional("style") ?? BridgeOptions.DefaultVoiceStyleName,
            VoiceTakeNumber = arguments.GetInt("take", BridgeOptions.DefaultVoiceTakeNumber),
        }.Validate();
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            YMM4 Vocaloid Bridge CLI

            doctor --ymm4-dir <directory> [--json|--ui]
            inspect-ui [--depth 6] [--menu ファイル]
            inspect-tracks
            generate --text <dialogue> --out-dir <directory> [--tempo 120] [--base-note 60]
            synthesize --text <dialogue> --output <file.wav> [--mode assisted|automatic] [--take 1-10] [--no-fallback] [--timeout 300]
            """);
    }
}

internal sealed class CliArguments
{
    private readonly Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);

    public static CliArguments Parse(IEnumerable<string> arguments)
    {
        var result = new CliArguments();
        var tokens = arguments.ToArray();
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument: {token}");
            }

            var key = token[2..];
            if (index + 1 >= tokens.Length || tokens[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result.values[key] = null;
                continue;
            }

            result.values[key] = tokens[++index];
        }

        return result;
    }

    public string GetRequired(string name) =>
        GetOptional(name) ?? throw new ArgumentException($"Missing required option --{name}.");

    public string? GetOptional(string name) => values.TryGetValue(name, out var value) ? value : null;

    public bool HasFlag(string name) => values.ContainsKey(name);

    public int GetInt(string name, int defaultValue)
    {
        var value = GetOptional(name);
        return value is null
            ? defaultValue
            : int.TryParse(value, out var parsed)
                ? parsed
                : throw new ArgumentException($"--{name} must be an integer.");
    }
}

internal sealed record Ymm4DoctorResult(bool Ready, string? Directory, string? Version);

internal sealed record DoctorResult(
    bool Ready,
    string OperatingSystem,
    string Dotnet,
    Ymm4DoctorResult Ymm4,
    VocaloidInstallation? Vocaloid,
    IReadOnlyList<InstallationDiagnostic> Diagnostics,
    string ApplicationData);
