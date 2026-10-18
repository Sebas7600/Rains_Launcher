namespace Launcher.Services;

public sealed class DownloadProgress
{
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public int Percent =>
        TotalBytes <= 0
            ? 0
            : (int)Math.Clamp(
                BytesDownloaded * 100L / TotalBytes,
                0,
                100);
}

public sealed class DownloadService
{
    private readonly HttpClient _httpClient;

    public DownloadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = destinationPath + ".download";

        try
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;

            await using Stream input =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            await using (FileStream output = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 64,
                useAsync: true))
            {
                byte[] buffer = new byte[1024 * 64];
                long downloaded = 0;

                while (true)
                {
                    int read = await input.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken);

                    if (read == 0)
                        break;

                    await output.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);

                    downloaded += read;

                    progress?.Report(new DownloadProgress
                    {
                        BytesDownloaded = downloaded,
                        TotalBytes = total
                    });
                }

                await output.FlushAsync(cancellationToken);
            }

            // Close the output stream before moving: Windows cannot rename a
            // file while this process still holds it with FileShare.None.
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }
}
