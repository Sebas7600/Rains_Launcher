using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public sealed record AuthSession(string AccessToken, string Email, string DisplayName);

/// <summary>
/// Minimal account client. The launcher never persists the user's password;
/// the session token is protected for the current Windows user with DPAPI.
/// </summary>
public sealed class AuthService : IDisposable
{
    private readonly AuthApiConfig _config;
    private readonly HttpClient _httpClient;
    private readonly SessionStore _sessionStore = new();

    public AuthService(AuthApiConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 5, 120))
        };
    }

    public async Task<AuthSession> SignInAsync(string username, string password,
        CancellationToken cancellationToken = default)
    {
        string identifierProperty = string.IsNullOrWhiteSpace(_config.LoginIdentifierProperty)
            ? "username"
            : _config.LoginIdentifierProperty;
        var loginBody = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [identifierProperty] = username,
            ["password"] = password
        };

        using var loginResponse = await _httpClient.PostAsync(
            BuildUri(_config.LoginPath),
            new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json"),
            cancellationToken);

        string loginJson = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(loginResponse, loginJson, "Sign in failed");

        using JsonDocument loginDocument = JsonDocument.Parse(loginJson);
        string tokenProperty = string.IsNullOrWhiteSpace(_config.TokenResponseProperty)
            ? "accessToken"
            : _config.TokenResponseProperty;

        if (!TryGetProperty(loginDocument.RootElement, tokenProperty, out JsonElement tokenElement)
            || tokenElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            throw new InvalidDataException(
                $"Login API returned no '{tokenProperty}' token. Check SignIn.Api.TokenResponseProperty.");
        }

        string token = tokenElement.GetString()!;
        AuthSession session = await FetchProfileAsync(token, username, cancellationToken);
        _sessionStore.Save(token);
        return session;
    }

    public async Task<AuthSession?> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        string? token = _sessionStore.Read();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            AuthSession session = await FetchProfileAsync(token, "", cancellationToken);
            return session;
        }
        catch
        {
            _sessionStore.Clear();
            return null;
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        string? token = _sessionStore.Read();
        try
        {
            if (!string.IsNullOrWhiteSpace(token)
                && !string.IsNullOrWhiteSpace(_config.LogoutPath))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(_config.LogoutPath));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                // Local sign-out should still work if the remote endpoint is unavailable.
                _ = response;
            }
        }
        catch
        {
            // The credential is removed locally even when the server cannot be reached.
        }
        finally
        {
            _sessionStore.Clear();
        }
    }

    private async Task<AuthSession> FetchProfileAsync(string token, string fallbackEmail,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(_config.UserProfilePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, json, "Could not load account profile");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (TryGetProperty(root, "user", out JsonElement nestedUser)
            && nestedUser.ValueKind == JsonValueKind.Object)
        {
            root = nestedUser;
        }

        string profileEmail = GetString(root, "email") ?? fallbackEmail;
        string displayName = GetString(root, "displayName")
            ?? GetString(root, "username")
            ?? GetString(root, "name")
            ?? profileEmail;

        return new AuthSession(token, profileEmail, displayName);
    }

    private Uri BuildUri(string path)
    {
        if (!Uri.TryCreate(_config.BaseUrl, UriKind.Absolute, out Uri? baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("SignIn.Api.BaseUrl must be a valid HTTP or HTTPS URL.");
        }

        return new Uri(baseUri.ToString().TrimEnd('/') + "/" + path.TrimStart('/'));
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string context)
    {
        if (response.IsSuccessStatusCode)
            return;

        string message = TryGetApiMessage(body)
            ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        throw new InvalidOperationException($"{context}: {message}");
    }

    private static string? TryGetApiMessage(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return GetString(document.RootElement, "message")
                ?? GetString(document.RootElement, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public void Dispose() => _httpClient.Dispose();
}

internal sealed class SessionStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyGameLauncher",
        "session.bin");

    public string? Read()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            return Encoding.UTF8.GetString(Protect(File.ReadAllBytes(_path), decrypt: true));
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public void Save(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        byte[] protectedToken = Protect(Encoding.UTF8.GetBytes(token), decrypt: false);
        File.WriteAllBytes(_path, protectedToken);
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static byte[] Protect(byte[] bytes, bool decrypt)
    {
        DataBlob input = new() { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        DataBlob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : CryptProtectData(ref input, "MyGameLauncher session", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);

            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            byte[] result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, output.Length);
            return result;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
                Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
                LocalFree(output.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string description,
        IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description,
        IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, uint flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
