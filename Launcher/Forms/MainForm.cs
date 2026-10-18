using System.Diagnostics;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Launcher.Models;
using Launcher.Services;

namespace Launcher;

public sealed class MainForm : Form
{
    private static readonly Color WindowColor = Color.FromArgb(10, 10, 13);
    private static readonly Color PanelColor = Color.FromArgb(18, 18, 22);
    private static readonly Color MutedColor = Color.FromArgb(170, 170, 176);
    private static readonly Color AccentCyan = Color.FromArgb(42, 232, 255);
    private static readonly Color AccentMagenta = Color.FromArgb(255, 55, 166);
    private static readonly Color CyberFace = Color.FromArgb(15, 19, 31);
    private static readonly PrivateFontCollection LauncherFonts = LoadLauncherFonts();
    private static readonly FontFamily LauncherFontFamily = GetLauncherFontFamily();
    private static readonly Font HeaderTabFont = CreateUiFont(20, FontStyle.Bold);
    private const int PageTransitionDurationMilliseconds = 1000;

    private readonly LauncherConfig _config;
    private readonly UpdateService _updateService;
    private readonly GameService _gameService = new();
    private readonly AuthService _authService;
    private readonly Dictionary<string, RoundedButton> _navigationButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly FlowLayoutPanel _navigationPanel = new();
    private readonly List<Image> _brandLogoImages = new();
    private readonly PictureBox _brandLogoPicture = new();

    private readonly BackdropLayoutPanel _root = new() { Dock = DockStyle.Fill, BackColor = WindowColor };
    private readonly Panel _contentHost = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    private readonly WipeTransitionOverlay _wipeTransition = new();
    private readonly Label _footerStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _footerAccount = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private readonly Panel _newsPage = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    private readonly Panel _playPage = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
    private readonly Panel _signInPage = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };

    private readonly Label _newsEyebrow = new();
    private readonly Label _newsHeadline = new();
    private readonly Label _newsDate = new();
    private readonly Label _newsSummary = new();
    private readonly Label _newsCounter = new();
    private readonly PictureBox _newsImage = new();
    private readonly RoundedButton _newsPrevious = new();
    private readonly RoundedButton _newsNext = new();
    private readonly RoundedButton _newsArticleButton = new();

    private readonly Label _versionLabel = new();
    private readonly Label _updateStatusLabel = new();
    private readonly Label _fileLabel = new();
    private readonly CyberProgressBar _progressBar = new();
    private readonly Label _techLoadingLabel = new();
    private readonly RoundedButton _checkButton = new();
    private readonly RoundedButton _repairButton = new();
    private readonly RoundedButton _playButton = new();

    private readonly TextBox _emailTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly RoundedButton _showPasswordButton = new();
    private readonly CheckBox _legalCheckBox = new();
    private readonly RoundedButton _signInSubmitButton = new();
    private readonly LinkLabel _createAccountLink = new();
    private readonly Label _signInHeading = new();
    private readonly Label _signInDescription = new();
    private readonly Label _signInEmailLabel = new();
    private readonly Label _signInPasswordLabel = new();
    private readonly Label _signInEyebrow = new();
    private readonly Panel _signInArtPanel = new() { BackColor = Color.Transparent };
    private readonly PictureBox _signInBackground = new();
    private readonly System.Windows.Forms.Timer _signInImageAnimationTimer = new() { Interval = 33 };
    private readonly Stopwatch _signInImageAnimationClock = new();
    private readonly System.Windows.Forms.Timer _pageTransitionTimer = new() { Interval = 16 };
    private readonly Stopwatch _pageTransitionClock = new();
    private readonly RoundedButton _accountButton = new();
    private readonly RoundedButton _languageButton = new();

    private GameManifest? _manifest;
    private AuthSession? _authSession;
    private string _language;
    private int _newsIndex;
    private int _signInImageBaseTop;
    private string? _transitionTargetPage;
    private string? _queuedTransitionPage;
    private bool _transitionPageChanged;
    private bool _hasBeenShown;
    private bool _isTransitioning;
    private string _currentPage = "News";
    private bool _busy;

    public MainForm()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _config = ConfigService.Load(configPath);
        _updateService = new UpdateService(_config);
        _authService = new AuthService(_config.SignIn.Api);
        _language = _config.Language.Options.Contains(_config.Language.Default,
            StringComparer.OrdinalIgnoreCase)
            ? _config.Language.Default.ToUpperInvariant()
            : "EN";

        InitializeUi();
    }

    private void InitializeUi()
    {
        Text = "Game Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 780);
        MinimumSize = new Size(1020, 650);
        BackColor = WindowColor;
        DoubleBuffered = true;
        ResizeRedraw = true;
        Font = CreateUiFont(10F);

        _root.ColumnCount = 1;
        _root.RowCount = 3;
        _root.Margin = Padding.Empty;
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _root.Controls.Add(BuildHeader(), 0, 0);
        BuildNewsPage();
        BuildPlayPage();
        BuildSignInPage();
        _contentHost.Controls.Add(_newsPage);
        _contentHost.Controls.Add(_playPage);
        _contentHost.Controls.Add(_signInPage);
        _wipeTransition.Visible = false;
        _contentHost.Controls.Add(_wipeTransition);
        _root.Controls.Add(_contentHost, 0, 1);
        _root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(_root);

        ApplyPageImmediately("News");
        RefreshNewsPost();
        ApplyLanguage();
        _pageTransitionTimer.Tick += (_, _) => UpdatePageTransition();

        Shown += async (_, _) =>
        {
            _hasBeenShown = true;
            _authSession = await _authService.RestoreSessionAsync();
            UpdateAccountButton();
            await CheckAndUpdateAsync(forceRepair: false, showDialogOnError: false);
        };
        FormClosed += (_, _) =>
        {
            _authService.Dispose();
            _signInImageAnimationTimer.Stop();
            _signInImageAnimationTimer.Dispose();
            _pageTransitionTimer.Stop();
            _pageTransitionTimer.Dispose();
            _newsImage.Image?.Dispose();
            _signInBackground.Image?.Dispose();
            foreach (Image image in _brandLogoImages)
                image.Dispose();
            _root.Backdrop?.Dispose();
        };
        Resize += (_, _) => InvalidateShell();
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(18, 6, 18, 6)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var brand = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 1, 16, 1),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        Image? logo = LoadConfiguredImage(_config.Branding.Logo);
        if (logo is not null)
        {
            _brandLogoImages.Add(logo);
            _brandLogoPicture.Image = logo;
            _brandLogoPicture.Dock = DockStyle.Fill;
            _brandLogoPicture.SizeMode = PictureBoxSizeMode.Zoom;
            _brandLogoPicture.BackColor = Color.Transparent;
            _brandLogoPicture.Margin = Padding.Empty;
            brand.Controls.Add(_brandLogoPicture);
        }

        FlowLayoutPanel navigation = _navigationPanel;
        navigation.Dock = DockStyle.Fill;
        navigation.WrapContents = false;
        navigation.FlowDirection = FlowDirection.LeftToRight;
        navigation.Padding = new Padding(0, 7, 0, 0);
        navigation.Margin = Padding.Empty;
        navigation.BackColor = Color.Transparent;

        _languageButton.Text = _language;
        _languageButton.Size = new Size(72, 52);
        _languageButton.Margin = new Padding(0, 0, 18, 0);
        StyleOutlineButton(_languageButton);
        _languageButton.Font = HeaderTabFont;
        _languageButton.Click += (_, _) => ShowLanguageMenu();
        navigation.Controls.Add(_languageButton);

        foreach (string tab in GetNavigationOrder())
        {
            var button = new RoundedButton
            {
                Name = $"nav{tab}",
                Text = TextForTab(tab),
                Tag = tab,
                AutoSize = false,
                Height = 52,
                MinimumSize = new Size(72, 52),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(15, 15, 18),
                ForeColor = Color.FromArgb(220, 220, 224),
                Font = HeaderTabFont,
                Margin = new Padding(0, 0, 5, 0),
                CornerRadius = 10,
                Padding = new Padding(12, 0, 12, 0),
                Cursor = Cursors.Hand
            };
            button.Width = MeasureNavigationButtonWidth(button.Text, button.Font);
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => Navigate(tab);
            navigation.Controls.Add(button);
            _navigationButtons[tab] = button;
        }

        _accountButton.Text = T("signIn");
        _accountButton.Size = new Size(174, 48);
        _accountButton.Anchor = AnchorStyles.None;
        _accountButton.Margin = Padding.Empty;
        _accountButton.CornerRadius = 9;
        StyleOutlineButton(_accountButton);
        _accountButton.Font = HeaderTabFont;
        _accountButton.Click += async (_, _) => await AccountActionAsync();

        var accountHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        void LayoutAccountButton()
        {
            _accountButton.Location = new Point(
                Math.Max(0, accountHost.ClientSize.Width - _accountButton.Width - 8),
                Math.Max(0, (accountHost.ClientSize.Height - _accountButton.Height) / 2));
        }
        accountHost.Resize += (_, _) => LayoutAccountButton();
        accountHost.Controls.Add(_accountButton);
        LayoutAccountButton();

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(navigation, 1, 0);
        layout.Controls.Add(accountHost, 2, 0);
        header.Controls.Add(layout);
        return header;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(30, 0, 30, 0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        _footerStatus.ForeColor = MutedColor;
        _footerStatus.Font = CreateUiFont(8.5F);
        _footerStatus.Text = T("ready");
        _footerAccount.ForeColor = MutedColor;
        _footerAccount.Font = CreateUiFont(8.5F);
        layout.Controls.Add(_footerStatus, 0, 0);
        layout.Controls.Add(_footerAccount, 1, 0);
        footer.Controls.Add(layout);
        return footer;
    }

    private void BuildNewsPage()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(58, 36, 58, 38),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

        var story = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(0, 16, 42, 12),
            BackColor = Color.Transparent
        };
        story.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        story.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
        story.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        story.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        story.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        story.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        _newsEyebrow.Dock = DockStyle.Fill;
        _newsEyebrow.ForeColor = AccentCyan;
        _newsEyebrow.Font = CreateUiFont(9, FontStyle.Bold);
        _newsEyebrow.TextAlign = ContentAlignment.MiddleLeft;

        _newsHeadline.Dock = DockStyle.Fill;
        _newsHeadline.ForeColor = Color.White;
        _newsHeadline.Font = CreateUiFont(30, FontStyle.Bold);
        _newsHeadline.TextAlign = ContentAlignment.MiddleLeft;
        _newsHeadline.AutoEllipsis = true;

        _newsDate.Dock = DockStyle.Fill;
        _newsDate.ForeColor = MutedColor;
        _newsDate.Font = CreateUiFont(9.5F);
        _newsDate.TextAlign = ContentAlignment.MiddleLeft;

        _newsSummary.Dock = DockStyle.Fill;
        _newsSummary.ForeColor = Color.FromArgb(218, 218, 222);
        _newsSummary.Font = CreateUiFont(11.5F);
        _newsSummary.TextAlign = ContentAlignment.TopLeft;
        _newsSummary.AutoEllipsis = true;

        _newsArticleButton.Text = "READ ARTICLE  →";
        _newsArticleButton.Size = new Size(184, 44);
        _newsArticleButton.Anchor = AnchorStyles.Left;
        StyleCyberButton(_newsArticleButton);
        _newsArticleButton.Click += (_, _) =>
        {
            NewsPostConfig? post = CurrentNewsPost;
            if (post is not null && !string.IsNullOrWhiteSpace(post.ArticleUrl))
                OpenWebAddress(post.ArticleUrl);
        };

        var carousel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        _newsPrevious.Text = "‹";
        _newsNext.Text = "›";
        foreach (RoundedButton button in new[] { _newsPrevious, _newsNext })
        {
            button.Size = new Size(40, 36);
            button.Font = CreateUiFont(21, FontStyle.Regular);
            button.CornerRadius = 7;
            StyleOutlineButton(button);
            button.Margin = new Padding(0, 0, 8, 0);
        }
        _newsPrevious.Click += (_, _) => SelectNewsPost(-1);
        _newsNext.Click += (_, _) => SelectNewsPost(1);
        _newsCounter.ForeColor = MutedColor;
        _newsCounter.TextAlign = ContentAlignment.MiddleLeft;
        _newsCounter.AutoSize = true;
        _newsCounter.Margin = new Padding(12, 9, 0, 0);
        carousel.Controls.AddRange(new Control[] { _newsPrevious, _newsNext, _newsCounter });

        story.Controls.Add(_newsEyebrow, 0, 0);
        story.Controls.Add(_newsHeadline, 0, 1);
        story.Controls.Add(_newsDate, 0, 2);
        story.Controls.Add(_newsSummary, 0, 3);
        story.Controls.Add(_newsArticleButton, 0, 4);
        story.Controls.Add(carousel, 0, 5);

        var imageFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = Padding.Empty,
            Margin = new Padding(12, 8, 0, 8)
        };
        _newsImage.Dock = DockStyle.Fill;
        _newsImage.SizeMode = PictureBoxSizeMode.Zoom;
        _newsImage.BackColor = Color.Transparent;
        imageFrame.Controls.Add(_newsImage);

        layout.Controls.Add(story, 0, 0);
        layout.Controls.Add(imageFrame, 1, 0);
        _newsPage.Controls.Add(layout);
    }

    private void BuildPlayPage()
    {
        _versionLabel.ForeColor = Color.FromArgb(220, 220, 222);
        _versionLabel.Font = CreateUiFont(8.5F);
        _versionLabel.Dock = DockStyle.Fill;
        _versionLabel.TextAlign = ContentAlignment.MiddleLeft;
        _versionLabel.Text = $"{T("serverVersion")}: —";

        var controlPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 154,
            BackColor = Color.FromArgb(205, 11, 11, 14),
            Padding = new Padding(42, 18, 48, 18)
        };
        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var statusStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0, 0, 20, 0)
        };
        statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        statusStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        statusStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _updateStatusLabel.ForeColor = Color.White;
        _updateStatusLabel.Font = CreateUiFont(10, FontStyle.Bold);
        _updateStatusLabel.Dock = DockStyle.Fill;
        _fileLabel.ForeColor = MutedColor;
        _fileLabel.Font = CreateUiFont(8.5F);
        _fileLabel.Dock = DockStyle.Fill;
        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Margin = new Padding(0, 2, 0, 2);
        _techLoadingLabel.Dock = DockStyle.Fill;
        _techLoadingLabel.ForeColor = AccentCyan;
        _techLoadingLabel.Font = new Font(FontFamily.GenericMonospace, 8.5F, FontStyle.Bold, GraphicsUnit.Point);
        _techLoadingLabel.TextAlign = ContentAlignment.MiddleLeft;
        _techLoadingLabel.Text = "SYSTEM STANDBY... [0%]";
        statusStack.Controls.Add(_updateStatusLabel, 0, 0);
        statusStack.Controls.Add(_fileLabel, 0, 1);
        statusStack.Controls.Add(_progressBar, 0, 2);
        statusStack.Controls.Add(_techLoadingLabel, 0, 3);

        var maintenance = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };
        maintenance.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        maintenance.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var maintenanceButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        _checkButton.Size = new Size(126, 36);
        _repairButton.Size = new Size(94, 36);
        _checkButton.Margin = new Padding(0, 0, 10, 0);
        _repairButton.Margin = Padding.Empty;
        StyleOutlineButton(_checkButton);
        StyleOutlineButton(_repairButton);
        _checkButton.Click += async (_, _) => await CheckAndUpdateAsync(false, true);
        _repairButton.Click += async (_, _) => await CheckAndUpdateAsync(true, true);
        maintenanceButtons.Controls.AddRange(new Control[] { _checkButton, _repairButton });
        maintenance.Controls.Add(maintenanceButtons, 0, 0);
        maintenance.Controls.Add(_versionLabel, 0, 1);

        var playButtonHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 12, 0, 4) };
        _playButton.Dock = DockStyle.Fill;
        _playButton.MinimumSize = new Size(174, 54);
        _playButton.Margin = Padding.Empty;
        StyleCyberButton(_playButton);
        _playButton.Font = CreateUiFont(11, FontStyle.Bold);
        _playButton.Click += (_, _) => StartGame();
        playButtonHost.Controls.Add(_playButton);

        controls.Controls.Add(statusStack, 0, 0);
        controls.Controls.Add(maintenance, 1, 0);
        controls.Controls.Add(playButtonHost, 2, 0);
        controlPanel.Controls.Add(controls);
        _playPage.Controls.Add(controlPanel);
    }

    private void BuildSignInPage()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var artPanel = _signInArtPanel;
        artPanel.Dock = DockStyle.Fill;
        _signInBackground.Dock = DockStyle.None;
        _signInBackground.SizeMode = PictureBoxSizeMode.Zoom;
        _signInBackground.BackColor = Color.Transparent;
        _signInBackground.Image = LoadConfiguredImage(_config.SignIn.BackgroundImage);
        artPanel.Controls.Add(_signInBackground);
        artPanel.Resize += (_, _) => LayoutSignInImage();
        _signInImageAnimationTimer.Tick += (_, _) => AnimateSignInImage();
        LayoutSignInImage();

        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(22, 18, 16, 10)
        };
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        _signInEyebrow.ForeColor = AccentCyan;
        _signInEyebrow.Font = CreateUiFont(9, FontStyle.Bold);
        _signInEyebrow.Dock = DockStyle.Fill;
        _signInHeading.Text = _config.SignIn.Heading;
        _signInHeading.ForeColor = Color.White;
        _signInHeading.Font = CreateUiFont(30, FontStyle.Bold);
        _signInHeading.Dock = DockStyle.Fill;
        _signInHeading.TextAlign = ContentAlignment.MiddleLeft;
        _signInDescription.Text = _config.SignIn.Description;
        _signInDescription.ForeColor = Color.FromArgb(208, 208, 214);
        _signInDescription.Font = CreateUiFont(10);
        _signInDescription.Dock = DockStyle.Fill;
        _signInDescription.AutoEllipsis = true;

        ConfigureInputLabel(_signInEmailLabel);
        ConfigureInputLabel(_signInPasswordLabel);
        ConfigureTextBox(_emailTextBox);
        ConfigureTextBox(_passwordTextBox);
        _passwordTextBox.UseSystemPasswordChar = true;
        _emailTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.Dock = DockStyle.Fill;

        var passwordRow = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(25, 24, 26),
            Margin = new Padding(0, 0, 0, 3)
        };
        var passwordHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 66, 0) };
        passwordHost.Controls.Add(_passwordTextBox);
        _showPasswordButton.Text = "SHOW";
        _showPasswordButton.Dock = DockStyle.Right;
        _showPasswordButton.Width = 66;
        _showPasswordButton.FlatStyle = FlatStyle.Flat;
        _showPasswordButton.ForeColor = AccentCyan;
        _showPasswordButton.BackColor = Color.FromArgb(25, 24, 26);
        _showPasswordButton.Font = CreateUiFont(8, FontStyle.Bold);
        _showPasswordButton.Cursor = Cursors.Hand;
        _showPasswordButton.CornerRadius = 0;
        _showPasswordButton.FlatAppearance.BorderSize = 0;
        _showPasswordButton.Click += (_, _) =>
        {
            _passwordTextBox.UseSystemPasswordChar = !_passwordTextBox.UseSystemPasswordChar;
            _showPasswordButton.Text = _passwordTextBox.UseSystemPasswordChar ? "SHOW" : "HIDE";
        };
        passwordRow.Controls.Add(passwordHost);
        passwordRow.Controls.Add(_showPasswordButton);

        _legalCheckBox.ForeColor = MutedColor;
        _legalCheckBox.BackColor = Color.Transparent;
        _legalCheckBox.Dock = DockStyle.Fill;
        _legalCheckBox.Font = CreateUiFont(16);

        _signInSubmitButton.Dock = DockStyle.Fill;
        _signInSubmitButton.Margin = new Padding(0, 4, 0, 4);
        StyleCyberButton(_signInSubmitButton);
        _signInSubmitButton.Click += async (_, _) => await SubmitSignInAsync();
        _emailTextBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await SubmitSignInAsync(); };
        _passwordTextBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await SubmitSignInAsync(); };

        _createAccountLink.Dock = DockStyle.Fill;
        _createAccountLink.LinkColor = AccentCyan;
        _createAccountLink.ActiveLinkColor = AccentMagenta;
        _createAccountLink.VisitedLinkColor = AccentCyan;
        _createAccountLink.Font = CreateUiFont(16);
        _createAccountLink.TextAlign = ContentAlignment.MiddleCenter;
        _createAccountLink.LinkClicked += (_, _) => OpenWebAddress(_config.SignIn.CreateAccountUrl);

        form.Controls.Add(_signInEyebrow, 0, 0);
        form.Controls.Add(_signInHeading, 0, 1);
        form.Controls.Add(_signInDescription, 0, 2);
        form.Controls.Add(_signInEmailLabel, 0, 3);
        form.Controls.Add(_emailTextBox, 0, 4);
        form.Controls.Add(_signInPasswordLabel, 0, 5);
        form.Controls.Add(passwordRow, 0, 6);
        form.Controls.Add(_legalCheckBox, 0, 7);
        form.Controls.Add(_signInSubmitButton, 0, 8);
        form.Controls.Add(_createAccountLink, 0, 10);
        formPanel.Controls.Add(form);

        layout.Controls.Add(artPanel, 0, 0);
        layout.Controls.Add(formPanel, 1, 0);
        _signInPage.Controls.Add(layout);
    }

    private void Navigate(string tab)
    {
        if (tab.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
        {
            OpenWebAddress(_config.Purchase.Url);
            return;
        }

        if (tab.Equals("Play", StringComparison.OrdinalIgnoreCase)
            && _config.Play.RequireSignIn
            && _authSession is null)
        {
            ShowPage("SignIn");
            return;
        }

        ShowPage(tab);
    }

    private void ShowPage(string page)
    {
        if (!_hasBeenShown || page.Equals(_currentPage, StringComparison.OrdinalIgnoreCase))
        {
            ApplyPageImmediately(page);
            return;
        }

        if (_isTransitioning)
        {
            _queuedTransitionPage = page;
            return;
        }

        _transitionTargetPage = page;
        _transitionPageChanged = false;
        _isTransitioning = true;
        _contentHost.Enabled = false;
        _wipeTransition.EdgeAtLeft = false;
        _wipeTransition.Visible = true;
        _wipeTransition.Bounds = new Rectangle(0, 0, 0, _contentHost.ClientSize.Height);
        _wipeTransition.BringToFront();
        _pageTransitionClock.Restart();
        _pageTransitionTimer.Start();
    }

    private void UpdatePageTransition()
    {
        double progress = Math.Clamp(
            _pageTransitionClock.Elapsed.TotalMilliseconds / PageTransitionDurationMilliseconds,
            0,
            1);
        double halfProgress = Math.Clamp(progress * 2, 0, 1);
        double eased = 0.5 - (0.5 * Math.Cos(Math.PI * halfProgress));
        int width = (int)Math.Round(_contentHost.ClientSize.Width * eased);
        if (progress < 0.5)
        {
            _wipeTransition.Bounds = new Rectangle(0, 0, width, _contentHost.ClientSize.Height);
        }
        else
        {
            _wipeTransition.EdgeAtLeft = true;
            if (!_transitionPageChanged && _transitionTargetPage is not null)
            {
                ApplyPageImmediately(_transitionTargetPage);
                _root.PerformLayout();
                _contentHost.PerformLayout();
                _root.Invalidate(true);
                _root.Refresh();
                _wipeTransition.BringToFront();
                _transitionPageChanged = true;
            }

            int remainingWidth = (int)Math.Round(_contentHost.ClientSize.Width * (1 - eased));
            _wipeTransition.Bounds = new Rectangle(
                _contentHost.ClientSize.Width - remainingWidth,
                0,
                remainingWidth,
                _contentHost.ClientSize.Height);
        }

        if (progress < 1)
            return;

        _pageTransitionTimer.Stop();
        _pageTransitionClock.Reset();
        _wipeTransition.Bounds = new Rectangle(_contentHost.ClientSize.Width, 0, 0, _contentHost.ClientSize.Height);
        _wipeTransition.Visible = false;
        _transitionTargetPage = null;
        _transitionPageChanged = false;
        _isTransitioning = false;
        _contentHost.Enabled = true;

        string? queuedPage = _queuedTransitionPage;
        _queuedTransitionPage = null;
        if (!string.IsNullOrWhiteSpace(queuedPage)
            && !queuedPage.Equals(_currentPage, StringComparison.OrdinalIgnoreCase))
        {
            ShowPage(queuedPage);
        }
    }

    private void ApplyPageImmediately(string page)
    {
        _currentPage = page;
        _newsPage.Visible = page.Equals("News", StringComparison.OrdinalIgnoreCase);
        _playPage.Visible = page.Equals("Play", StringComparison.OrdinalIgnoreCase);
        _signInPage.Visible = page.Equals("SignIn", StringComparison.OrdinalIgnoreCase);
        SetSignInImageAnimation(_signInPage.Visible);
        string pageBackground = page.ToLowerInvariant() switch
        {
            "news" => _config.News.BackgroundImage,
            "play" => _config.Play.BackgroundImage,
            "signin" => _config.SignIn.FullBackgroundImage,
            _ => ""
        };
        SetShellBackground(string.IsNullOrWhiteSpace(pageBackground)
            ? _config.Branding.BackgroundImage
            : pageBackground);

        foreach ((string tab, RoundedButton button) in _navigationButtons)
        {
            bool active = tab.Equals(page, StringComparison.OrdinalIgnoreCase);
            button.ForeColor = active ? AccentCyan : Color.FromArgb(220, 220, 224);
            button.Font = HeaderTabFont;
            button.Width = MeasureNavigationButtonWidth(button.Text, button.Font);
            button.FlatAppearance.BorderSize = active ? 1 : 0;
            button.FlatAppearance.BorderColor = AccentCyan;
        }

        UpdatePlayAvailability();
        InvalidateShell();
    }

    private void LayoutSignInImage()
    {
        if (_signInArtPanel.ClientSize.Width <= 0 || _signInArtPanel.ClientSize.Height <= 0)
            return;

        int width = Math.Max(1, (int)Math.Round(_signInArtPanel.ClientSize.Width * (2.0 / 3.0)));
        int height = Math.Max(1, (int)Math.Round(_signInArtPanel.ClientSize.Height * (2.0 / 3.0)));
        int left = (_signInArtPanel.ClientSize.Width - width) / 2;
        _signInImageBaseTop = (_signInArtPanel.ClientSize.Height - height) / 2;
        _signInBackground.Bounds = new Rectangle(left, _signInImageBaseTop, width, height);
        if (!_signInImageAnimationTimer.Enabled)
            _signInBackground.Top = _signInImageBaseTop;
    }

    private void SetSignInImageAnimation(bool enabled)
    {
        if (enabled)
        {
            if (!_signInImageAnimationTimer.Enabled)
            {
                _signInImageAnimationClock.Restart();
                _signInImageAnimationTimer.Start();
            }
            return;
        }

        _signInImageAnimationTimer.Stop();
        _signInImageAnimationClock.Reset();
        _signInBackground.Top = _signInImageBaseTop;
    }

    private void AnimateSignInImage()
    {
        if (_signInBackground.Image is null)
            return;

        double phase = _signInImageAnimationClock.Elapsed.TotalSeconds % 2.0;
        double progress = phase <= 1.0
            ? 0.5 - (0.5 * Math.Cos(Math.PI * phase))
            : 0.5 + (0.5 * Math.Cos(Math.PI * (phase - 1.0)));
        int travel = (int)Math.Round(DeviceDpi * 0.5 / 2.54 * progress);
        _signInBackground.Top = Math.Max(0, _signInImageBaseTop - travel);
    }

    private void SetShellBackground(string relativePath)
    {
        Image? next = LoadConfiguredImage(relativePath);
        Image? previous = _root.Backdrop;
        _root.Backdrop = next;
        previous?.Dispose();
        _root.Invalidate(true);
    }

    private void InvalidateShell()
    {
        if (_root.IsDisposed || !_root.IsHandleCreated)
            return;
        _navigationPanel.PerformLayout();
        _root.Invalidate(true);
        _root.Update();
    }

    private static int MeasureNavigationButtonWidth(string text, Font font) =>
        Math.Max(72, TextRenderer.MeasureText(text, font,
            new Size(int.MaxValue, 36), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 24);

    private static PrivateFontCollection LoadLauncherFonts()
    {
        var fonts = new PrivateFontCollection();
        string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Quicksand[wght].ttf");
        if (File.Exists(fontPath))
        {
            try { fonts.AddFontFile(fontPath); }
            catch (ArgumentException) { }
            catch (ExternalException) { }
        }
        return fonts;
    }

    private static FontFamily GetLauncherFontFamily() =>
        LauncherFonts.Families.FirstOrDefault(family =>
            family.Name.Equals("Quicksand", StringComparison.OrdinalIgnoreCase))
        ?? FontFamily.GenericSansSerif;

    private static Font CreateUiFont(float size, FontStyle style = FontStyle.Regular)
    {
        FontStyle supportedStyle = LauncherFontFamily.IsStyleAvailable(style)
            ? style
            : FontStyle.Regular;
        return new Font(LauncherFontFamily, size, supportedStyle, GraphicsUnit.Point);
    }

    private async Task AccountActionAsync()
    {
        if (_authSession is null)
        {
            ShowPage("SignIn");
            return;
        }

        DialogResult answer = MessageBox.Show(this,
            $"{T("signedInAs")} {_authSession.DisplayName}\n\n{T("signOutQuestion")}",
            Text,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return;

        await _authService.SignOutAsync();
        _authSession = null;
        UpdateAccountButton();
        SetFooterStatus(T("signedOut"));
        ShowPage("SignIn");
    }

    private async Task SubmitSignInAsync()
    {
        if (_busy)
            return;

        string email = _emailTextBox.Text.Trim();
        string password = _passwordTextBox.Text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowSignInError(T("enterCredentials"));
            return;
        }
        if (!_legalCheckBox.Checked)
        {
            ShowSignInError(T("acceptTerms"));
            return;
        }

        _busy = true;
        _signInSubmitButton.Enabled = false;
        _signInSubmitButton.Text = T("signingIn");
        SetFooterStatus(T("connectingAccount"));
        try
        {
            _authSession = await _authService.SignInAsync(email, password);
            _passwordTextBox.Clear();
            UpdateAccountButton();
            SetFooterStatus(T("signedIn"));
            ShowPage("Play");
        }
        catch (Exception ex)
        {
            ShowSignInError(ex.Message);
            SetFooterStatus(T("signInFailed"));
        }
        finally
        {
            _busy = false;
            _signInSubmitButton.Enabled = true;
            _signInSubmitButton.Text = T("signIn");
            UpdatePlayAvailability();
        }
    }

    private async Task CheckAndUpdateAsync(bool forceRepair, bool showDialogOnError)
    {
        if (_busy)
            return;

        _busy = true;
        _checkButton.Enabled = false;
        _repairButton.Enabled = false;
        _progressBar.Value = 0;
        _techLoadingLabel.Text = "SYSTEM LOADING... [0%]";
        SetFooterStatus(forceRepair ? T("repairing") : T("checking"));
        _updateStatusLabel.Text = forceRepair ? T("repairing") : T("checking");

        try
        {
            _manifest = await _updateService.GetManifestAsync();
            UpdateServerVersionLabel();
            int fileCount = _manifest.Files.Count;

            if (forceRepair)
            {
                foreach (GameFile file in _manifest.Files)
                {
                    string path = _updateService.GetGameFilePath(file.Path);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }

            var progress = new Progress<FileUpdateProgress>(p =>
            {
                _progressBar.Value = Math.Clamp(p.OverallPercent, 0, 100);
                _techLoadingLabel.Text = $"SYSTEM LOADING... [{p.OverallPercent}%]";
                _updateStatusLabel.Text = $"{p.Status}: {p.OverallPercent}%";
                _fileLabel.Text = fileCount > 0
                    ? $"{T("file")}: {p.CurrentFile} ({p.CurrentFilePercent}%)"
                    : "";
            });

            UpdateResult result = await _updateService.UpdateAsync(_manifest, progress);
            _progressBar.Value = 100;
            _techLoadingLabel.Text = "SYSTEM READY... [100%]";
            _updateStatusLabel.Text = result.FilesDownloaded > 0
                ? $"{T("updateComplete")}: {result.FilesDownloaded}"
                : T("upToDate");
            _fileLabel.Text = $"{T("checkedFiles")}: {result.FilesChecked}";
            SetFooterStatus(_updateStatusLabel.Text);
            UpdatePlayAvailability();
        }
        catch (Exception ex)
        {
            _manifest = null;
            UpdateServerVersionLabel();
            _techLoadingLabel.Text = "SYSTEM LINK ERROR... [0%]";
            _updateStatusLabel.Text = T("updateFailed");
            _fileLabel.Text = ex.Message;
            SetFooterStatus($"{T("updateFailed")}: {ex.Message}");
            UpdatePlayAvailability();

            if (showDialogOnError)
            {
                MessageBox.Show(this, ex.Message, T("launcherError"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _busy = false;
            _checkButton.Enabled = true;
            _repairButton.Enabled = true;
            UpdatePlayAvailability();
        }
    }

    private void StartGame()
    {
        if (_config.Play.RequireSignIn && _authSession is null)
        {
            ShowPage("SignIn");
            return;
        }
        if (_busy)
        {
            SetFooterStatus(T("waitUpdate"));
            return;
        }
        if (_manifest is null)
        {
            MessageBox.Show(this, T("gameNotReady"), T("launcherError"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            string gamePath = _updateService.GetGameExecutablePath(_manifest.GameExe);
            if (_gameService.IsRunning(gamePath))
            {
                MessageBox.Show(this, T("gameAlreadyRunning"), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _gameService.StartGame(gamePath);
            if (_config.CloseAfterGameStarts)
                BeginInvoke(Close);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("launchFailed"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdatePlayAvailability()
    {
        bool hasGame = _manifest is not null;
        _playButton.Enabled = hasGame && !_busy
            && (!_config.Play.RequireSignIn || _authSession is not null);
        _checkButton.Enabled = !_busy;
        _repairButton.Enabled = !_busy;
    }

    private void UpdateAccountButton()
    {
        _accountButton.Text = _authSession is null
            ? T("signIn")
            : _authSession.DisplayName;
        _footerAccount.Text = _authSession is null
            ? T("notSignedIn")
            : $"{T("signedInAs")} {_authSession.DisplayName}";
        UpdatePlayAvailability();
    }

    private void UpdateServerVersionLabel() =>
        _versionLabel.Text = $"{T("serverVersion")}: {_manifest?.Version ?? "—"}";

    private void ApplyLanguage()
    {
        _languageButton.Text = _language;
        foreach ((string tab, RoundedButton button) in _navigationButtons)
        {
            button.Text = TextForTab(tab);
            button.Width = MeasureNavigationButtonWidth(button.Text, button.Font);
        }

        _accountButton.Text = _authSession is null ? T("signIn") : _authSession.DisplayName;
        _newsEyebrow.Text = T("latestNews");
        _newsArticleButton.Text = T("readArticle");
        _newsPrevious.AccessibleName = T("previous");
        _newsNext.AccessibleName = T("next");
        _updateStatusLabel.Text = string.IsNullOrWhiteSpace(_updateStatusLabel.Text)
            ? T("checking")
            : _updateStatusLabel.Text;
        _fileLabel.Text = string.IsNullOrWhiteSpace(_fileLabel.Text) ? T("preparing") : _fileLabel.Text;
        _checkButton.Text = T("checkUpdate");
        _repairButton.Text = T("repair");
        _playButton.Text = Localized(_config.Play.PlayButtonText, _config.Play.PlayButtonTextVn);
        UpdateServerVersionLabel();
        _signInEyebrow.Text = T("accountAccess");
        _signInHeading.Text = Localized(_config.SignIn.Heading, _config.SignIn.HeadingVn);
        _signInDescription.Text = Localized(_config.SignIn.Description, _config.SignIn.DescriptionVn);
        _signInEmailLabel.Text = T("email");
        _signInPasswordLabel.Text = T("password");
        _legalCheckBox.Text = T("legalAgreement");
        _signInSubmitButton.Text = T("signIn");
        _createAccountLink.Text = T("createAccount");
        _footerStatus.Text = T("ready");
        RefreshNewsPost();
        UpdateNewsControlsLanguage();
        UpdateAccountButton();
        InvalidateShell();
    }

    private void UpdateNewsControlsLanguage()
    {
        _newsEyebrow.Text = T("latestNews");
        _newsArticleButton.Text = T("readArticle");
        if (_config.News.Posts.Count == 0)
        {
            _newsHeadline.Text = T("noNews");
            _newsSummary.Text = T("noNewsDescription");
            _newsCounter.Text = "0 / 0";
        }
    }

    private void SetLanguage(string language)
    {
        if (!_config.Language.Options.Contains(language, StringComparer.OrdinalIgnoreCase))
            return;
        _language = language.ToUpperInvariant();
        ApplyLanguage();
    }

    private void ShowLanguageMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (string option in _config.Language.Options)
        {
            string language = option;
            menu.Items.Add(language, null, (_, _) => SetLanguage(language));
        }
        menu.Show(_languageButton, new Point(0, _languageButton.Height));
    }

    private void RefreshNewsPost()
    {
        NewsPostConfig? post = CurrentNewsPost;
        if (post is null)
        {
            _newsHeadline.Text = T("noNews");
            _newsDate.Text = "";
            _newsSummary.Text = T("noNewsDescription");
            _newsCounter.Text = "0 / 0";
            _newsImage.Image?.Dispose();
            _newsImage.Image = null;
            _newsArticleButton.Enabled = false;
            _newsPrevious.Enabled = false;
            _newsNext.Enabled = false;
            return;
        }

        _newsHeadline.Text = Localized(post.Title, post.TitleVn);
        _newsDate.Text = post.PublishedAt;
        _newsSummary.Text = Localized(post.Summary, post.SummaryVn);
        _newsCounter.Text = $"{_newsIndex + 1} / {_config.News.Posts.Count}";
        _newsArticleButton.Enabled = !string.IsNullOrWhiteSpace(post.ArticleUrl);
        _newsPrevious.Enabled = _config.News.Posts.Count > 1;
        _newsNext.Enabled = _config.News.Posts.Count > 1;
        _newsImage.Image?.Dispose();
        _newsImage.Image = LoadConfiguredImage(post.Image);
    }

    private void SelectNewsPost(int delta)
    {
        int count = _config.News.Posts.Count;
        if (count < 2)
            return;
        _newsIndex = (_newsIndex + delta + count) % count;
        RefreshNewsPost();
    }

    private NewsPostConfig? CurrentNewsPost =>
        _config.News.Posts.Count == 0 ? null : _config.News.Posts[_newsIndex % _config.News.Posts.Count];

    private string[] GetNavigationOrder()
    {
        string[] valid = ["Play", "News", "Purchase"];
        var configured = _config.NavigationOrder
            .Where(tab => valid.Contains(tab, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string tab in valid)
        {
            if (!configured.Contains(tab, StringComparer.OrdinalIgnoreCase))
                configured.Add(tab);
        }
        return configured.ToArray();
    }

    private string TextForTab(string tab) => tab.ToLowerInvariant() switch
    {
        "play" => T("play"),
        "news" => T("news"),
        "purchase" => T("purchase"),
        _ => tab
    };

    private string Localized(string english, string vietnamese)
    {
        string selected = _language.Equals("VN", StringComparison.OrdinalIgnoreCase)
            ? vietnamese
            : english;
        return string.IsNullOrWhiteSpace(selected) ? english : selected;
    }

    private string T(string key)
    {
        bool vi = _language.Equals("VN", StringComparison.OrdinalIgnoreCase);
        return (key, vi) switch
        {
            ("play", true) => "CHƠI GAME",
            ("news", true) => "TIN TỨC",
            ("purchase", true) => "CỬA HÀNG",
            ("play", false) => "PLAY",
            ("news", false) => "NEWS",
            ("purchase", false) => "PURCHASE",
            ("signIn", true) => "ĐĂNG NHẬP",
            ("signIn", false) => "SIGN IN",
            ("ready", true) => "Launcher sẵn sàng",
            ("notSignedIn", true) => "Chưa đăng nhập",
            ("signedInAs", true) => "Đang đăng nhập:",
            ("signOutQuestion", true) => "Bạn muốn đăng xuất chứ?",
            ("signedOut", true) => "Đã đăng xuất",
            ("latestNews", true) => "TIN MỚI NHẤT  /  CẬP NHẬT GAME",
            ("readArticle", true) => "ĐỌC BÀI VIẾT  →",
            ("previous", true) => "Trước",
            ("next", true) => "Tiếp",
            ("noNews", true) => "Chưa có tin mới",
            ("noNewsDescription", true) => "Thêm bài viết trong Launcher.News.Posts của appsettings.json.",
            ("serverVersion", true) => "Phiên bản máy chủ",
            ("file", true) => "File",
            ("updateComplete", true) => "Cập nhật xong, đã tải file",
            ("upToDate", true) => "Game đã ở phiên bản mới nhất",
            ("checkedFiles", true) => "Số file đã kiểm tra",
            ("updateFailed", true) => "Cập nhật thất bại",
            ("checking", true) => "Đang kiểm tra file game...",
            ("repairing", true) => "Đang sửa chữa game...",
            ("preparing", true) => "Đang chuẩn bị",
            ("checkUpdate", true) => "KIỂM TRA CẬP NHẬT",
            ("repair", true) => "SỬA GAME",
            ("waitUpdate", true) => "Vui lòng chờ cập nhật hoàn tất.",
            ("gameNotReady", true) => "Game chưa sẵn sàng. Hãy kiểm tra kết nối máy chủ và cập nhật.",
            ("gameAlreadyRunning", true) => "Game đang chạy.",
            ("launcherError", true) => "Lỗi Launcher",
            ("launchFailed", true) => "Không thể chạy game",
            ("accountAccess", true) => Localized(_config.SignIn.AccountType, _config.SignIn.AccountTypeVn),
            ("email", true) => "Tên đăng nhập",
            ("password", true) => "Mật khẩu",
            ("legalAgreement", true) => "Tôi xác nhận đủ tuổi và đồng ý với Điều khoản dịch vụ.",
            ("createAccount", true) => "Chưa có tài khoản? Tạo tài khoản miễn phí",
            ("enterCredentials", true) => "Vui lòng nhập email và mật khẩu.",
            ("acceptTerms", true) => "Vui lòng xác nhận điều khoản trước khi đăng nhập.",
            ("signingIn", true) => "ĐANG ĐĂNG NHẬP...",
            ("connectingAccount", true) => "Đang kết nối dịch vụ tài khoản...",
            ("signedIn", true) => "Đăng nhập thành công",
            ("signInFailed", true) => "Đăng nhập thất bại",
            ("ready", false) => "Launcher ready",
            ("notSignedIn", false) => "Not signed in",
            ("signedInAs", false) => "Signed in as:",
            ("signOutQuestion", false) => "Would you like to sign out?",
            ("signedOut", false) => "Signed out",
            ("latestNews", false) => "LATEST NEWS  /  GAME UPDATES",
            ("readArticle", false) => "READ ARTICLE  →",
            ("previous", false) => "Previous",
            ("next", false) => "Next",
            ("noNews", false) => "No news yet",
            ("noNewsDescription", false) => "Add posts under Launcher.News.Posts in appsettings.json.",
            ("serverVersion", false) => "Server version",
            ("file", false) => "File",
            ("updateComplete", false) => "Update complete. Files downloaded",
            ("upToDate", false) => "Game is up to date",
            ("checkedFiles", false) => "Files checked",
            ("updateFailed", false) => "Update failed",
            ("checking", false) => "Checking game files...",
            ("repairing", false) => "Repairing game...",
            ("preparing", false) => "Preparing",
            ("checkUpdate", false) => "CHECK UPDATE",
            ("repair", false) => "REPAIR",
            ("waitUpdate", false) => "Please wait for the update to finish.",
            ("gameNotReady", false) => "The game is not ready. Check the server connection and update.",
            ("gameAlreadyRunning", false) => "The game is already running.",
            ("launcherError", false) => "Launcher error",
            ("launchFailed", false) => "Could not start the game",
            ("accountAccess", false) => Localized(_config.SignIn.AccountType, _config.SignIn.AccountTypeVn),
            ("email", false) => "Username",
            ("password", false) => "Password",
            ("legalAgreement", false) => "I confirm that I am of legal age and accept the Terms of Service.",
            ("createAccount", false) => "New here? Create a free account",
            ("enterCredentials", false) => "Enter your email and password.",
            ("acceptTerms", false) => "Please accept the terms before signing in.",
            ("signingIn", false) => "SIGNING IN...",
            ("connectingAccount", false) => "Connecting to the account service...",
            ("signedIn", false) => "Signed in successfully",
            ("signInFailed", false) => "Sign in failed",
            _ => key
        };
    }

    private void SetFooterStatus(string text) => _footerStatus.Text = text;

    private void ShowSignInError(string message) => MessageBox.Show(this, message,
        T("signInFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void OpenWebAddress(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            MessageBox.Show(this, "Configure a valid HTTPS or HTTP URL in appsettings.json.",
                T("launcherError"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, T("launcherError"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private Image? LoadConfiguredImage(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        try
        {
            string basePath = Path.GetFullPath(AppContext.BaseDirectory);
            string fullPath = Path.GetFullPath(Path.Combine(basePath,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string root = basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                return null;

            using Image source = Image.FromFile(fullPath);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static void ConfigureInputLabel(Label label)
    {
        label.ForeColor = Color.FromArgb(220, 220, 224);
        label.Font = CreateUiFont(17);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.BackColor = Color.FromArgb(25, 24, 26);
        textBox.ForeColor = Color.White;
        textBox.BorderStyle = BorderStyle.None;
        textBox.Font = CreateUiFont(17);
        textBox.Margin = new Padding(0, 0, 0, 3);
    }

    private static void StyleCyberButton(RoundedButton button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = AccentCyan;
        button.BackColor = CyberFace;
        button.ForeColor = Color.White;
        button.Font = CreateUiFont(11, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.CornerRadius = 10;
        button.CyberpunkStyle = true;
    }

    private static void StyleOutlineButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(185, 185, 190);
        button.BackColor = Color.FromArgb(15, 15, 18);
        button.ForeColor = Color.White;
        button.Font = CreateUiFont(9, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        if (button is RoundedButton rounded)
            rounded.CornerRadius = 8;
    }

    private sealed class WipeTransitionOverlay : Control
    {
        private static readonly Color WipeColor = Color.FromArgb(8, 10, 16);

        public bool EdgeAtLeft { get; set; }

        public WipeTransitionOverlay()
        {
            BackColor = WipeColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(WipeColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 3 || Height <= 0)
                return;

            int edgeX = EdgeAtLeft ? 1 : Width - 3;
            using var cyan = new SolidBrush(AccentCyan);
            using var magenta = new SolidBrush(AccentMagenta);
            e.Graphics.FillRectangle(cyan, edgeX, 0, 2, Height);
            int magentaX = EdgeAtLeft ? edgeX + 2 : edgeX - 2;
            if (magentaX >= 0 && magentaX < Width)
                e.Graphics.FillRectangle(magenta, magentaX, 0, 1, Height);
        }
    }

    private sealed class BackdropLayoutPanel : TableLayoutPanel
    {
        public Image? Backdrop { get; set; }

        public BackdropLayoutPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Backdrop is null || ClientSize.Width == 0 || ClientSize.Height == 0)
            {
                e.Graphics.Clear(BackColor);
                return;
            }

            float imageRatio = (float)Backdrop.Width / Backdrop.Height;
            float viewRatio = (float)ClientSize.Width / ClientSize.Height;
            Rectangle source;
            if (imageRatio > viewRatio)
            {
                int width = (int)(Backdrop.Height * viewRatio);
                source = new Rectangle((Backdrop.Width - width) / 2, 0, width, Backdrop.Height);
            }
            else
            {
                int height = (int)(Backdrop.Width / viewRatio);
                source = new Rectangle(0, (Backdrop.Height - height) / 2, Backdrop.Width, height);
            }

            e.Graphics.DrawImage(Backdrop, ClientRectangle, source, GraphicsUnit.Pixel);
        }
    }

    private sealed class CyberProgressBar : Control
    {
        private readonly System.Windows.Forms.Timer _pulseTimer = new() { Interval = 45 };
        private int _minimum;
        private int _maximum = 100;
        private int _value;

        public int Minimum
        {
            get => _minimum;
            set
            {
                _minimum = Math.Max(0, value);
                if (_maximum <= _minimum)
                    _maximum = _minimum + 1;
                Value = _value;
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(_minimum + 1, value);
                Value = _value;
            }
        }

        public int Value
        {
            get => _value;
            set
            {
                int next = Math.Clamp(value, _minimum, _maximum);
                if (_value == next)
                    return;
                _value = next;
                if (_value > _minimum && _value < _maximum)
                    _pulseTimer.Start();
                else
                    _pulseTimer.Stop();
                Invalidate();
            }
        }

        public CyberProgressBar()
        {
            Height = 20;
            MinimumSize = new Size(80, 16);
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.SupportsTransparentBackColor
                | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            _pulseTimer.Tick += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientSize.Width < 8 || ClientSize.Height < 8)
                return;

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle frame = ClientRectangle;
            frame.Inflate(-1, -1);
            Rectangle inner = Rectangle.Inflate(frame, -2, -3);
            if (inner.Width <= 0 || inner.Height <= 0)
                return;

            using (var frameBrush = new SolidBrush(Color.FromArgb(235, 5, 11, 19)))
                e.Graphics.FillRectangle(frameBrush, frame);
            using (var framePen = new Pen(Color.FromArgb(130, AccentCyan), 1))
                e.Graphics.DrawRectangle(framePen, frame);
            using (var trackBrush = new SolidBrush(Color.FromArgb(255, 10, 19, 28)))
                e.Graphics.FillRectangle(trackBrush, inner);

            int segmentCount = Math.Clamp(inner.Width / 15, 8, 72);
            int gap = Math.Max(2, (int)Math.Round(DeviceDpi / 96.0 * 3));
            int segmentWidth = Math.Max(1, (inner.Width - gap * (segmentCount - 1)) / segmentCount);
            float filledWidth = inner.Width * (float)(_value - _minimum) / (_maximum - _minimum);
            float edgeX = inner.Left + filledWidth;

            for (int i = 0; i < segmentCount; i++)
            {
                int x = inner.Left + i * (segmentWidth + gap);
                int width = i == segmentCount - 1 ? inner.Right - x : segmentWidth;
                if (width <= 0)
                    continue;
                var segment = new Rectangle(x, inner.Top, width, inner.Height);
                using var inactiveBrush = new SolidBrush(Color.FromArgb(255, 13, 25, 32));
                e.Graphics.FillRectangle(inactiveBrush, segment);

                float filledInSegment = Math.Clamp(filledWidth - (x - inner.Left), 0, width);
                if (filledInSegment > 0)
                {
                    var fill = new RectangleF(x, inner.Top, filledInSegment, inner.Height);
                    using var fillBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        fill, Color.FromArgb(20, 145, 58), Color.FromArgb(61, 255, 106),
                        System.Drawing.Drawing2D.LinearGradientMode.Vertical);
                    e.Graphics.FillRectangle(fillBrush, fill);
                    using var shinePen = new Pen(Color.FromArgb(115, 206, 255, 201), 1);
                    e.Graphics.DrawLine(shinePen, x, inner.Top + 1, x + filledInSegment, inner.Top + 1);
                }
            }

            if (_value > _minimum)
            {
                int laserX = Math.Clamp((int)Math.Round(edgeX), inner.Left, inner.Right - 1);
                int pulse = 150 + (int)(105 * (0.5 + 0.5 * Math.Sin(Environment.TickCount64 / 105.0)));
                using var laserGlow = new SolidBrush(Color.FromArgb(pulse / 2, 85, 255, 134));
                e.Graphics.FillRectangle(laserGlow,
                    new Rectangle(Math.Max(inner.Left, laserX - 5), inner.Top - 1,
                        Math.Min(11, inner.Right - Math.Max(inner.Left, laserX - 5)), inner.Height + 2));
                using var laserPen = new Pen(Color.FromArgb(pulse, 217, 255, 229), 1.5F);
                e.Graphics.DrawLine(laserPen, laserX, inner.Top - 2, laserX, inner.Bottom + 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pulseTimer.Stop();
                _pulseTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class RoundedButton : Button
    {
        private int _cornerRadius = 9;
        private bool _hovered;
        private bool _pressed;

        public bool CyberpunkStyle { get; set; }

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                UpdateRoundedRegion();
                Invalidate();
            }
        }

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            UpdateRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRoundedRegion();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Paint the rounded face below; leaving the corners clear reveals the parent backdrop.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            bounds.Inflate(-1, -1);
            using System.Drawing.Drawing2D.GraphicsPath path = CreateRoundedPath(bounds, _cornerRadius);

            if (CyberpunkStyle)
            {
                PaintCyberpunk(e.Graphics, bounds, path);
                return;
            }

            Color fill = BackColor;
            if (!Enabled)
                fill = ControlPaint.Dark(BackColor, 0.2f);
            else if (_pressed)
                fill = ControlPaint.Dark(BackColor, 0.1f);
            else if (_hovered)
                fill = ControlPaint.Light(BackColor, 0.08f);

            using (var brush = new SolidBrush(fill))
                e.Graphics.FillPath(brush, path);

            if (FlatAppearance.BorderSize > 0)
            {
                using var pen = new Pen(FlatAppearance.BorderColor, FlatAppearance.BorderSize);
                e.Graphics.DrawPath(pen, path);
            }

            Color textColor = Enabled ? ForeColor : SystemColors.GrayText;
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void PaintCyberpunk(Graphics graphics, Rectangle bounds,
            System.Drawing.Drawing2D.GraphicsPath path)
        {
            bool active = Enabled;
            Color faceStart = !active
                ? Color.FromArgb(25, 29, 39)
                : _pressed ? Color.FromArgb(17, 37, 55)
                : _hovered ? Color.FromArgb(43, 34, 67)
                : Color.FromArgb(26, 37, 61);
            Color faceEnd = !active
                ? Color.FromArgb(12, 23, 30)
                : _pressed ? Color.FromArgb(18, 55, 64)
                : _hovered ? Color.FromArgb(17, 66, 81)
                : Color.FromArgb(13, 52, 68);

            int glowAlpha = !active ? 28 : _hovered ? 145 : 82;
            using (var glowPen = new Pen(Color.FromArgb(glowAlpha, AccentCyan), active && _hovered ? 7 : 5))
                graphics.DrawPath(glowPen, path);
            using (var faceBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                bounds, faceStart, faceEnd, System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal))
                graphics.FillPath(faceBrush, path);

            Color cyanBorder = Color.FromArgb(active ? 250 : 95, AccentCyan);
            using (var borderPen = new Pen(cyanBorder, active && _hovered ? 2.1F : 1.55F))
                graphics.DrawPath(borderPen, path);

            Rectangle inset = bounds;
            inset.Inflate(-4, -4);
            if (inset.Width > 8 && inset.Height > 8)
            {
                using System.Drawing.Drawing2D.GraphicsPath insetPath = CreateRoundedPath(inset, Math.Max(2, _cornerRadius - 4));
                using var insetPen = new Pen(Color.FromArgb(!active ? 42 : _hovered ? 175 : 125, AccentMagenta), 1);
                graphics.DrawPath(insetPen, insetPath);
            }

            using (var accentPen = new Pen(Color.FromArgb(active ? 230 : 82, AccentMagenta), 2))
            {
                graphics.DrawLine(accentPen, bounds.Left + 12, bounds.Top + 2, bounds.Left + 36, bounds.Top + 2);
                graphics.DrawLine(accentPen, bounds.Right - 36, bounds.Bottom - 2, bounds.Right - 12, bounds.Bottom - 2);
            }

            Color textColor = active ? Color.White : Color.FromArgb(125, 140, 151);
            TextRenderer.DrawText(graphics, Text, Font, bounds, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void UpdateRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
                return;
            using System.Drawing.Drawing2D.GraphicsPath path = CreateRoundedPath(
                new Rectangle(0, 0, Width, Height), _cornerRadius);
            Region? previous = Region;
            Region = new Region(path);
            previous?.Dispose();
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 1)
            {
                path.AddRectangle(bounds);
                return path;
            }

            var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class CoverPictureBox : PictureBox
    {
        public CoverPictureBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            if (Image is null || ClientSize.Width == 0 || ClientSize.Height == 0)
            {
                base.OnPaint(pe);
                return;
            }

            float imageRatio = (float)Image.Width / Image.Height;
            float viewRatio = (float)ClientSize.Width / ClientSize.Height;
            Rectangle source;
            if (imageRatio > viewRatio)
            {
                int width = (int)(Image.Height * viewRatio);
                source = new Rectangle((Image.Width - width) / 2, 0, width, Image.Height);
            }
            else
            {
                int height = (int)(Image.Width / viewRatio);
                source = new Rectangle(0, (Image.Height - height) / 2, Image.Width, height);
            }

            pe.Graphics.DrawImage(Image, ClientRectangle, source, GraphicsUnit.Pixel);
        }
    }
}
