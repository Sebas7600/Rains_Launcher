using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length < 2)
{
    Console.WriteLine(
        "Usage: ManifestTool <files-root> <version> [gameExe]");
    return;
}

string root = Path.GetFullPath(args[0]);
string version = args[1];
string gameExe = args.Length >= 3 ? args[2] : "TestGame.exe";

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Directory not found: {root}");
    Environment.ExitCode = 1;
    return;
}

var manifest = new GameManifest
{
    Version = version,
    Mandatory = false,
    GameExe = gameExe
};

foreach (string file in Directory
    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
{
    string relative = Path.GetRelativePath(root, file)
        .Replace(Path.DirectorySeparatorChar, '/');

    FileInfo info = new(file);

    string hash = await CalculateSha256Async(file);

    manifest.Files.Add(new GameFile
    {
        Path = relative,
        Size = info.Length,
        Sha256 = hash
    });
}

string json = JsonSerializer.Serialize(
    manifest,
    new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition =
            JsonIgnoreCondition.WhenWritingNull
    });

string output = Path.Combine(
    Directory.GetParent(root)!.FullName,
    "manifest.json");

await File.WriteAllTextAsync(output, json);

Console.WriteLine($"Generated: {output}");
Console.WriteLine($"Version: {version}");
Console.WriteLine($"Files: {manifest.Files.Count}");

static async Task<string> CalculateSha256Async(string path)
{
    await using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        1024 * 64,
        useAsync: true);

    using SHA256 sha = SHA256.Create();

    byte[] hash = await sha.ComputeHashAsync(stream);

    return Convert.ToHexString(hash).ToLowerInvariant();
}

public sealed class GameManifest
{
    public string Version { get; set; } = "";
    public bool Mandatory { get; set; }
    public string GameExe { get; set; } = "";
    public List<GameFile> Files { get; set; } = new();
}

public sealed class GameFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}
