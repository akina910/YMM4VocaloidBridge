using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PluginSmoke <plugin-publish-directory> <YMM4-directory>");
    return 2;
}

var pluginDirectory = Path.GetFullPath(args[0]);
var ymm4Directory = Path.GetFullPath(args[1]);
RequireDirectory(pluginDirectory, "plugin publish");
RequireDirectory(ymm4Directory, "YMM4");

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    foreach (var directory in new[] { pluginDirectory, ymm4Directory })
    {
        var candidate = Path.Combine(directory, name.Name + ".dll");
        if (File.Exists(candidate))
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
        }
    }

    return null;
};

var pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(
    Path.Combine(pluginDirectory, "YMM4VocaloidBridge.Plugin.dll"));
var pluginType = RequireType(pluginAssembly, "YMM4VocaloidBridge.Plugin.MikuV6VoicePlugin");
var plugin = Activator.CreateInstance(pluginType)
    ?? throw new InvalidOperationException("Voice plugin could not be instantiated.");
RequireEqual("YMM4 VOCALOID Bridge", GetProperty<string>(plugin, "Name"), "plugin name");

var voices = GetProperty<IEnumerable>(plugin, "Voices").Cast<object>().ToArray();
RequireEqual(1, voices.Length, "voice count");
var speaker = voices[0];
RequireEqual("VOCALOID6 Bridge", GetProperty<string>(speaker, "EngineName"), "engine name");
RequireEqual("初音ミク V6", GetProperty<string>(speaker, "SpeakerName"), "speaker name");
RequireEqual("YMM4VocaloidBridge.Vocaloid6", GetProperty<string>(speaker, "API"), "speaker API");
RequireEqual("HATSUNE_MIKU_V6", GetProperty<string>(speaker, "ID"), "speaker ID");

var parameter = Invoke(speaker, "CreateVoiceParameter")
    ?? throw new InvalidOperationException("Voice parameter could not be created.");
RequireEqual("MikuV6VoiceParameter", parameter.GetType().Name, "parameter type");

var readingTask = (Task)(Invoke(speaker, "ConvertKanjiToYomiAsync", "今日は初音ミクです。", parameter)
    ?? throw new InvalidOperationException("Reading task was not returned."));
await readingTask.ConfigureAwait(false);
var reading = readingTask.GetType().GetProperty("Result")?.GetValue(readingTask) as string;
RequireEqual("キョーワハツネミクデス", reading, "Japanese reading");

var coreAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(
    Path.Combine(pluginDirectory, "YMM4VocaloidBridge.Core.dll"));
var coreFrameType = RequireType(coreAssembly, "YMM4VocaloidBridge.Core.LipSync.LipSyncFrame");
var coreShapeType = RequireType(coreAssembly, "YMM4VocaloidBridge.Core.Reading.MouthShape");
var frames = Array.CreateInstance(coreFrameType, 2);
frames.SetValue(CreateCoreFrame(coreFrameType, coreShapeType, TimeSpan.Zero, "Closed"), 0);
frames.SetValue(CreateCoreFrame(coreFrameType, coreShapeType, TimeSpan.FromMilliseconds(100), "A"), 1);

var pronounceType = RequireType(pluginAssembly, "YMM4VocaloidBridge.Plugin.MikuV6Pronounce");
var pronounce = Activator.CreateInstance(pronounceType, [frames])
    ?? throw new InvalidOperationException("Pronounce object could not be instantiated.");
var ymmFrames = GetProperty<Array>(pronounce, "LipSyncFrames");
RequireEqual(2, ymmFrames.Length, "YMM4 lip-sync frame count");
var clone = Invoke(pronounce, "Clone")
    ?? throw new InvalidOperationException("Pronounce clone was not returned.");
if (ReferenceEquals(pronounce, clone))
{
    throw new InvalidOperationException("Pronounce clone returned the original instance.");
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "PASS",
    plugin = GetProperty<string>(plugin, "Name"),
    speaker = GetProperty<string>(speaker, "SpeakerName"),
    reading,
    lipSyncFrames = ymmFrames.Length,
}));
return 0;

static object CreateCoreFrame(Type frameType, Type shapeType, TimeSpan time, string shape)
{
    var shapeValue = Enum.Parse(shapeType, shape);
    return Activator.CreateInstance(frameType, [time, shapeValue])
        ?? throw new InvalidOperationException("Core lip-sync frame could not be instantiated.");
}

static Type RequireType(Assembly assembly, string name) =>
    assembly.GetType(name, throwOnError: false)
    ?? throw new TypeLoadException($"Required type was not found: {name}");

static T GetProperty<T>(object instance, string name)
{
    var value = instance.GetType().GetProperty(name)?.GetValue(instance);
    return value is T typed
        ? typed
        : throw new InvalidOperationException($"Property '{name}' did not return {typeof(T).Name}.");
}

static object? Invoke(object instance, string name, params object[] arguments)
{
    var method = instance.GetType().GetMethods()
        .SingleOrDefault(x => x.Name == name && x.GetParameters().Length == arguments.Length)
        ?? throw new MissingMethodException(instance.GetType().FullName, name);
    return method.Invoke(instance, arguments);
}

static void RequireEqual<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Unexpected {description}: expected '{expected}', actual '{actual}'.");
    }
}

static void RequireDirectory(string path, string description)
{
    if (!Directory.Exists(path))
    {
        throw new DirectoryNotFoundException($"The {description} directory does not exist: {path}");
    }
}
