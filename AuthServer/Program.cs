using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
// Development API is bound to loopback only. Use HTTPS and a real identity
// provider/API before exposing authentication to other machines.
builder.WebHost.UseUrls("http://127.0.0.1:8090");
var app = builder.Build();

string seedPath = Path.Combine(app.Environment.ContentRootPath, "SeedData", "users.seed.json");
if (!File.Exists(seedPath))
    throw new FileNotFoundException("User seed file is missing.", seedPath);

SeedFile seed = JsonSerializer.Deserialize<SeedFile>(
    await File.ReadAllTextAsync(seedPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("User seed file is empty or invalid.");

var users = seed.Users.ToDictionary(user => user.Username, StringComparer.OrdinalIgnoreCase);
var sessions = new ConcurrentDictionary<string, ActiveSession>(StringComparer.Ordinal);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/auth/login", (LoginRequest request) => Login(request, users, sessions));
app.MapGet("/users/me", (HttpContext context) => GetProfile(context, sessions));
app.MapPost("/auth/logout", (HttpContext context) => Logout(context, sessions));

app.Run();

static IResult Login(LoginRequest request,
    IReadOnlyDictionary<string, SeedUser> users,
    ConcurrentDictionary<string, ActiveSession> sessions)
{
    if (string.IsNullOrWhiteSpace(request.Username)
        || string.IsNullOrEmpty(request.Password)
        || !users.TryGetValue(request.Username.Trim(), out SeedUser? user)
        || !VerifyPassword(request.Password, user))
    {
        return Results.Json(new { message = "Invalid username or password." }, statusCode: 401);
    }

    byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
    string token = Convert.ToBase64String(tokenBytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
    sessions[token] = new ActiveSession(user, DateTimeOffset.UtcNow.AddHours(8));

    return Results.Ok(new
    {
        accessToken = token,
        user = PublicProfile(user)
    });
}

static IResult GetProfile(HttpContext context,
    ConcurrentDictionary<string, ActiveSession> sessions)
{
    if (!TryGetSession(context, sessions, out ActiveSession? session))
        return Results.Json(new { message = "Session is missing or expired." }, statusCode: 401);

    return Results.Ok(new { user = PublicProfile(session!.User) });
}

static IResult Logout(HttpContext context,
    ConcurrentDictionary<string, ActiveSession> sessions)
{
    string? token = ReadBearerToken(context);
    if (token is not null)
        sessions.TryRemove(token, out _);
    return Results.NoContent();
}

static bool TryGetSession(HttpContext context,
    ConcurrentDictionary<string, ActiveSession> sessions,
    out ActiveSession? session)
{
    string? token = ReadBearerToken(context);
    if (token is not null
        && sessions.TryGetValue(token, out ActiveSession? found)
        && found.ExpiresAt > DateTimeOffset.UtcNow)
    {
        session = found;
        return true;
    }

    if (token is not null)
        sessions.TryRemove(token, out _);
    session = null;
    return false;
}

static string? ReadBearerToken(HttpContext context)
{
    string? authorization = context.Request.Headers.Authorization;
    const string prefix = "Bearer ";
    return authorization is not null
        && authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
}

static bool VerifyPassword(string password, SeedUser user)
{
    if (!user.PasswordHashAlgorithm.Equals("PBKDF2-HMAC-SHA256", StringComparison.OrdinalIgnoreCase)
        || user.PasswordIterations < 100_000
        || user.PasswordIterations > 2_000_000)
    {
        return false;
    }

    try
    {
        byte[] salt = Convert.FromBase64String(user.PasswordSalt);
        byte[] expected = Convert.FromBase64String(user.PasswordHash);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            user.PasswordIterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
    catch (FormatException)
    {
        return false;
    }
}

static object PublicProfile(SeedUser user) => new
{
    username = user.Username,
    displayName = user.DisplayName,
    email = user.Email
};

sealed class SeedFile
{
    public List<SeedUser> Users { get; set; } = new();
}

sealed class SeedUser
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHashAlgorithm { get; set; } = "";
    public int PasswordIterations { get; set; }
    public string PasswordSalt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

sealed record LoginRequest(string Username, string Password);
sealed record ActiveSession(SeedUser User, DateTimeOffset ExpiresAt);
