using System.Diagnostics;

namespace Launcher.Services;

public sealed class GameService
{
    public bool IsRunning(string executablePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(executablePath);

        return Process.GetProcessesByName(fileName).Length > 0;
    }

    public Process StartGame(
        string executablePath,
        string? arguments = null)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException(
                "Không tìm thấy file game.",
                executablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
                ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            Arguments = arguments ?? ""
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Không thể khởi chạy game.");
    }
}
