namespace Launcher.Models;

public sealed class LauncherConfig
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080/";
    public string ManifestPath { get; set; } = "manifest.json";
    public string GameDirectory { get; set; } = "Game";
    public string GameExe { get; set; } = "TestGame.exe";
    public string DownloadPath { get; set; } = "files";
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool CloseAfterGameStarts { get; set; }

    public BrandingConfig Branding { get; set; } = new();
    public List<string> NavigationOrder { get; set; } = ["Play", "News", "Purchase"];
    public LanguageConfig Language { get; set; } = new();
    public PlayPageConfig Play { get; set; } = new();
    public NewsPageConfig News { get; set; } = new();
    public PurchasePageConfig Purchase { get; set; } = new();
    public SignInPageConfig SignIn { get; set; } = new();
}

public sealed class BrandingConfig
{
    public string Logo { get; set; } = "Assets/launcher-logo.png";
    public string BackgroundImage { get; set; } = "Assets/play-background.jpg";
    public string Theme { get; set; } = "dark-gold";
}

public sealed class LanguageConfig
{
    public string Default { get; set; } = "EN";
    public List<string> Options { get; set; } = ["EN", "VN"];
}

public sealed class PlayPageConfig
{
    public string BackgroundImage { get; set; } = "Assets/play-background.jpg";
    public string PlayButtonText { get; set; } = "PLAY GAME";
    public string PlayButtonTextVn { get; set; } = "CHƠI GAME";
    public bool RequireSignIn { get; set; } = true;
}

public sealed class NewsPageConfig
{
    public string Title { get; set; } = "News";
    public string TitleVn { get; set; } = "Tin tức";
    public string BackgroundImage { get; set; } = "";
    public List<NewsPostConfig> Posts { get; set; } = new();
}

public sealed class NewsPostConfig
{
    public string Title { get; set; } = "";
    public string TitleVn { get; set; } = "";
    public string Summary { get; set; } = "";
    public string SummaryVn { get; set; } = "";
    public string Image { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string ArticleUrl { get; set; } = "";
}

public sealed class PurchasePageConfig
{
    public string Url { get; set; } = "https://example.com/purchase";
    public bool OpenInExternalBrowser { get; set; } = true;
}

public sealed class SignInPageConfig
{
    public string BackgroundImage { get; set; } = "Assets/sign-in-background.jpg";
    public string FullBackgroundImage { get; set; } = "";
    public string AccountType { get; set; } = "FROZEVERSE ACCOUNT";
    public string AccountTypeVn { get; set; } = "TÀI KHOẢN FROZEVERSE";
    public string Heading { get; set; } = "START NOW";
    public string HeadingVn { get; set; } = "BẮT ĐẦU NGAY";
    public string Description { get; set; } = "Sign in to continue your adventure.";
    public string DescriptionVn { get; set; } = "Đăng nhập để tiếp tục cuộc phiêu lưu của bạn.";
    public string CreateAccountUrl { get; set; } = "https://example.com/register";
    public AuthApiConfig Api { get; set; } = new();
}

public sealed class AuthApiConfig
{
    public string BaseUrl { get; set; } = "https://api.example.com/";
    public string LoginPath { get; set; } = "auth/login";
    public string LoginIdentifierProperty { get; set; } = "username";
    public string UserProfilePath { get; set; } = "users/me";
    public string LogoutPath { get; set; } = "auth/logout";
    public int TimeoutSeconds { get; set; } = 20;
    public string TokenResponseProperty { get; set; } = "accessToken";
}

public sealed class AppSettings
{
    public LauncherConfig Launcher { get; set; } = new();
}
