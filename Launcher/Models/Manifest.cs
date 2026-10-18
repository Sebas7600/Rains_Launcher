namespace Launcher.Models;

public sealed class GameManifest
{
    public string Version { get; set; } = "0.0.0";
    public bool Mandatory { get; set; }
    public string GameExe { get; set; } = "TestGame.exe";
    public List<GameFile> Files { get; set; } = new();
}

public sealed class GameFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}
