using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public sealed class UpdateResult
{
    public required GameManifest Manifest { get; init; }
    public int FilesChecked { get; init; }
    public int FilesDownloaded { get; init; }
    public long BytesDownloaded { get; init; }
}

public sealed class UpdateService
{
    private readonly LauncherConfig _config;
    private readonly HttpClient _httpClient;
    private readonly DownloadService _downloadService;

    public UpdateService(LauncherConfig config)
    {
        _config = config;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(
                Math.Max(5, config.RequestTimeoutSeconds))
        };

        _downloadService = new DownloadService(_httpClient);
    }

    public async Task<GameManifest> GetManifestAsync(
        CancellationToken cancellationToken = default)
    {
        string url = BuildUrl(_config.ManifestPath);

        using HttpResponseMessage response =
            await _httpClient.GetAsync(url, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using Stream stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        var manifest =
            await JsonSerializer.DeserializeAsync<GameManifest>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

        return manifest
            ?? throw new InvalidDataException("Manifest rỗng hoặc không hợp lệ.");
    }

    public async Task<UpdateResult> UpdateAsync(
        GameManifest manifest,
        IProgress<FileUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string gameRoot = GetGameRoot();

        Directory.CreateDirectory(gameRoot);

        int checkedFiles = 0;
        int downloadedFiles = 0;
        long downloadedBytes = 0;

        foreach (GameFile file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string safeRelativePath = ValidateRelativePath(file.Path);
            string localPath = GetGameFilePath(safeRelativePath);

            checkedFiles++;

            bool valid = await IsFileValidAsync(
                localPath,
                file,
                cancellationToken);

            if (valid)
            {
                progress?.Report(new FileUpdateProgress
                {
                    CurrentFile = file.Path,
                    CurrentFilePercent = 100,
                    OverallPercent = checkedFiles * 100 / Math.Max(1, manifest.Files.Count),
                    Status = "Đã có"
                });

                continue;
            }

            string remotePath =
                string.IsNullOrWhiteSpace(_config.DownloadPath)
                    ? safeRelativePath
                    : $"{_config.DownloadPath.TrimEnd('/')}/{safeRelativePath.Replace('\\', '/')}";

            string url = BuildUrl(remotePath);

            progress?.Report(new FileUpdateProgress
            {
                CurrentFile = file.Path,
                CurrentFilePercent = 0,
                OverallPercent = checkedFiles * 100 / Math.Max(1, manifest.Files.Count),
                Status = "Đang tải"
            });

            var fileProgress = new Progress<DownloadProgress>(p =>
            {
                int basePercent =
                    downloadedFiles * 100 / Math.Max(1, manifest.Files.Count);

                int currentContribution =
                    p.Percent / Math.Max(1, manifest.Files.Count);

                progress?.Report(new FileUpdateProgress
                {
                    CurrentFile = file.Path,
                    CurrentFilePercent = p.Percent,
                    OverallPercent = Math.Min(
                        99,
                        basePercent + currentContribution),
                    Status = "Đang tải"
                });
            });

            await _downloadService.DownloadAsync(
                url,
                localPath,
                fileProgress,
                cancellationToken);

            string actualHash = await HashService.Sha256Async(
                localPath,
                cancellationToken);

            if (!actualHash.Equals(
                NormalizeHash(file.Sha256),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA-256 không khớp cho file: {file.Path}");
            }

            downloadedFiles++;
            downloadedBytes += file.Size;

            progress?.Report(new FileUpdateProgress
            {
                CurrentFile = file.Path,
                CurrentFilePercent = 100,
                OverallPercent =
                    checkedFiles * 100 / Math.Max(1, manifest.Files.Count),
                Status = "Hoàn tất"
            });
        }

        return new UpdateResult
        {
            Manifest = manifest,
            FilesChecked = checkedFiles,
            FilesDownloaded = downloadedFiles,
            BytesDownloaded = downloadedBytes
        };
    }

    private async Task<bool> IsFileValidAsync(
        string localPath,
        GameFile manifestFile,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(localPath))
            return false;

        FileInfo info = new(localPath);

        if (info.Length != manifestFile.Size)
            return false;

        string hash = await HashService.Sha256Async(
            localPath,
            cancellationToken);

        return hash.Equals(
            NormalizeHash(manifestFile.Sha256),
            StringComparison.OrdinalIgnoreCase);
    }

    private string GetGameRoot()
    {
        string root = AppContext.BaseDirectory;

        string candidate = Path.GetFullPath(
            Path.Combine(root, _config.GameDirectory));

        string rootWithSeparator =
            Path.GetFullPath(root)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "GameDirectory không hợp lệ.");
        }

        return candidate;
    }

    public string GetGameFilePath(string relativePath)
    {
        string safeRelativePath = ValidateRelativePath(relativePath);
        string gameRoot = GetGameRoot();
        string fullPath = Path.GetFullPath(
            Path.Combine(gameRoot, safeRelativePath));

        string rootWithSeparator = gameRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Đường dẫn file nằm ngoài thư mục Game: {relativePath}");
        }

        return fullPath;
    }

    public string GetGameExecutablePath(string? manifestGameExe = null)
    {
        string exeName =
            string.IsNullOrWhiteSpace(manifestGameExe)
                ? _config.GameExe
                : manifestGameExe;

        return GetGameFilePath(exeName);
    }

    private string BuildUrl(string relativePath)
    {
        string baseUrl = _config.BaseUrl.TrimEnd('/') + "/";
        return new Uri(
            new Uri(baseUrl),
            relativePath.Replace('\\', '/'))
            .ToString();
    }

    private static string NormalizeHash(string hash) =>
        hash.Trim().Replace(" ", "").ToLowerInvariant();

    private static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException("Đường dẫn file rỗng.");

        string normalized = path.Replace('/', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
            throw new InvalidDataException(
                $"Đường dẫn tuyệt đối không được phép: {path}");

        string full = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "TempRoot", normalized));

        string tempRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "TempRoot"))
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!full.StartsWith(
            tempRoot,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Path traversal bị chặn: {path}");
        }

        return normalized;
    }
}

public sealed class FileUpdateProgress
{
    public string CurrentFile { get; init; } = "";
    public int CurrentFilePercent { get; init; }
    public int OverallPercent { get; init; }
    public string Status { get; init; } = "";
}
