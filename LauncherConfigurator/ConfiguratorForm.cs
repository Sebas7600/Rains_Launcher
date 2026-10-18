using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LauncherConfigurator;

public sealed class ConfiguratorForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(20, 20, 24);
    private static readonly Color SurfaceColor = Color.FromArgb(30, 30, 35);
    private static readonly Color AccentColor = Color.FromArgb(232, 166, 59);
    private readonly Dictionary<string, TextBox> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBox _targetFolder = new();
    private readonly Label _status = new();
    private readonly ListBox _newsList = new();
    private readonly TextBox _newsTitleEn = new();
    private readonly TextBox _newsTitleVn = new();
    private readonly TextBox _newsSummaryEn = new();
    private readonly TextBox _newsSummaryVn = new();
    private readonly TextBox _newsDate = new();
    private readonly TextBox _newsUrl = new();
    private readonly TextBox _newsImagePath = new();
    private readonly Button _newsConfirmButton = MakeButton("XÁC NHẬN BÀI VIẾT", outlined: false);
    private JsonObject? _document;
    private bool _loadingNews;
    private int _editingNewsIndex = -1;

    public ConfiguratorForm()
    {
        Text = "Launcher Studio — Game Launcher Configurator";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 790);
        MinimumSize = new Size(1020, 700);
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);

        BuildUi();
        string? sourceLauncher = FindNearbyLauncherFolder();
        if (sourceLauncher is not null)
            LoadTarget(sourceLauncher);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundColor,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = BackgroundColor,
            Margin = Padding.Empty
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var title = new Label
        {
            Text = "LAUNCHER STUDIO",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = AccentColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var folderLabel = new Label
        {
            Text = "Thư mục chứa appsettings.json của launcher",
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _targetFolder.Dock = DockStyle.Fill;
        _targetFolder.ReadOnly = true;
        StyleTextBox(_targetFolder);
        var browse = MakeButton("CHỌN THƯ MỤC", outlined: true);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(5);
        browse.Click += (_, _) => BrowseTarget();
        var reload = MakeButton("NẠP LẠI", outlined: true);
        reload.Dock = DockStyle.Fill;
        reload.Margin = new Padding(5);
        reload.Click += (_, _) =>
        {
            if (Directory.Exists(_targetFolder.Text))
                LoadTarget(_targetFolder.Text);
        };

        top.Controls.Add(title, 0, 0);
        top.SetColumnSpan(title, 3);
        top.Controls.Add(folderLabel, 0, 1);
        top.Controls.Add(browse, 1, 1);
        top.Controls.Add(reload, 2, 1);

        var bodyHost = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        var body = new TabControl { Font = new Font("Segoe UI", 9.5F) };
        body.TabPages.Add(BuildBrandTab());
        body.TabPages.Add(BuildNewsTab());
        body.TabPages.Add(BuildSignInTab());
        bodyHost.Controls.Add(body);
        void LayoutBody()
        {
            if (bodyHost.ClientSize.Width <= 0 || bodyHost.ClientSize.Height <= 0)
                return;
            int width = Math.Min(bodyHost.ClientSize.Width, 1360);
            body.SetBounds((bodyHost.ClientSize.Width - width) / 2, 0, width, bodyHost.ClientSize.Height);
        }
        bodyHost.Resize += (_, _) => LayoutBody();

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        _status.Text = "Chọn thư mục launcher để bắt đầu.";
        _status.ForeColor = Color.Gainsboro;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        var save = MakeButton("LƯU CẤU HÌNH", outlined: false);
        save.Dock = DockStyle.Fill;
        save.Click += (_, _) => SaveConfig();
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(save, 1, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(bodyHost, 0, 1);
        LayoutBody();
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
    }

    private TabPage BuildBrandTab()
    {
        var page = MakeTabPage("Thương hiệu & giao diện");
        var grid = CreateConfigGrid();
        AddAssetField(grid, "Launcher.Branding.Logo", "Logo có tên game (ảnh PNG)", "Branding", "logo");
        AddAssetField(grid, "Launcher.Branding.BackgroundImage", "Nền mặc định toàn cửa sổ", "Branding", "background");
        AddTextField(grid, "Launcher.Language.Default", "Ngôn ngữ mở đầu (EN/VN)");
        AddTextField(grid, "Launcher.BaseUrl", "URL máy chủ cập nhật game");
        AddTextField(grid, "Launcher.ManifestPath", "Đường dẫn manifest");
        AddTextField(grid, "Launcher.GameDirectory", "Thư mục game đã cài");
        AddTextField(grid, "Launcher.GameExe", "Tên file game .exe");
        AddTextField(grid, "Launcher.DownloadPath", "Thư mục tải/cập nhật");
        AddAssetField(grid, "Launcher.Play.BackgroundImage", "Nền toàn cửa sổ — Play", "Play", "play-background");
        AddTextField(grid, "Launcher.Play.PlayButtonText", "Nút Play — English");
        AddTextField(grid, "Launcher.Play.PlayButtonTextVn", "Nút Play — Tiếng Việt");
        AddAssetField(grid, "Launcher.News.BackgroundImage", "Nền toàn cửa sổ — News", "News", "news-background");
        AddAssetField(grid, "Launcher.SignIn.FullBackgroundImage", "Nền toàn cửa sổ — Sign in", "SignIn", "signin-background");
        AddAssetField(grid, "Launcher.SignIn.BackgroundImage", "Ảnh panel trái — Sign in", "SignIn", "signin-art");
        AddTextField(grid, "Launcher.Purchase.Url", "URL trang Purchase");
        AddTextField(grid, "Launcher.SignIn.CreateAccountUrl", "URL tạo tài khoản");
        AddTextField(grid, "Launcher.SignIn.Api.BaseUrl", "API tài khoản (BaseUrl)");
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildNewsTab()
    {
        var page = MakeTabPage("Bài viết News");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = SurfaceColor,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var listPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty
        };
        listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        var listLabel = new Label
        {
            Text = "BÀI VIẾT",
            Dock = DockStyle.Fill,
            ForeColor = AccentColor,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _newsList.Dock = DockStyle.Fill;
        _newsList.BackColor = BackgroundColor;
        _newsList.ForeColor = Color.White;
        _newsList.BorderStyle = BorderStyle.FixedSingle;
        _newsList.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingNews)
                return;
            SaveCurrentNewsToDocument();
            _editingNewsIndex = _newsList.SelectedIndex;
            LoadSelectedNews();
        };
        var listButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 82,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = SurfaceColor,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        var add = MakeButton("+ Thêm", outlined: true);
        add.Size = new Size(112, 32);
        add.Click += (_, _) => AddNewsPost();
        var remove = MakeButton("Xóa", outlined: true);
        remove.Size = new Size(74, 32);
        remove.Click += (_, _) => RemoveNewsPost();
        _newsConfirmButton.Size = new Size(216, 32);
        _newsConfirmButton.Click += (_, _) => ConfirmCurrentNews();
        listButtons.Controls.Add(add);
        listButtons.Controls.Add(remove);
        listButtons.Controls.Add(_newsConfirmButton);
        listPanel.Controls.Add(listLabel, 0, 0);
        listPanel.Controls.Add(_newsList, 0, 1);
        listPanel.Controls.Add(listButtons, 0, 2);

        var editor = new Panel { Dock = DockStyle.Fill, BackColor = SurfaceColor, Padding = new Padding(12), AutoScroll = true };
        var editorGrid = CreateConfigGrid();
        AddEditorField(editorGrid, "Tiêu đề — EN", _newsTitleEn);
        AddEditorField(editorGrid, "Tiêu đề — VN", _newsTitleVn);
        AddEditorField(editorGrid, "Mô tả — EN", _newsSummaryEn, multiline: true);
        AddEditorField(editorGrid, "Mô tả — VN", _newsSummaryVn, multiline: true);
        AddEditorField(editorGrid, "Ngày đăng", _newsDate);
        AddEditorField(editorGrid, "Link bài viết", _newsUrl);
        AddAssetEditorField(editorGrid, "Poster / ảnh bài viết", _newsImagePath, "News", "news-poster");
        editor.Controls.Add(editorGrid);

        layout.Controls.Add(listPanel, 0, 0);
        layout.Controls.Add(editor, 1, 0);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildSignInTab()
    {
        var page = MakeTabPage("Sign in & nội dung");
        var grid = CreateConfigGrid();
        AddTextField(grid, "Launcher.SignIn.Heading", "Heading — English");
        AddTextField(grid, "Launcher.SignIn.HeadingVn", "Heading — Tiếng Việt");
        AddTextField(grid, "Launcher.SignIn.AccountType", "Loại tài khoản — English");
        AddTextField(grid, "Launcher.SignIn.AccountTypeVn", "Loại tài khoản — Tiếng Việt");
        AddTextField(grid, "Launcher.SignIn.Description", "Mô tả — English");
        AddTextField(grid, "Launcher.SignIn.DescriptionVn", "Mô tả — Tiếng Việt");
        AddTextField(grid, "Launcher.SignIn.Api.LoginPath", "API path — login");
        AddTextField(grid, "Launcher.SignIn.Api.LoginIdentifierProperty", "Tên field username gửi tới API");
        AddTextField(grid, "Launcher.SignIn.Api.UserProfilePath", "API path — user profile");
        AddTextField(grid, "Launcher.SignIn.Api.LogoutPath", "API path — logout");
        var note = new Label
        {
            Text = "Tiện ích này không lưu mật khẩu. Tài khoản được quản lý ở AuthServer/SeedData/users.seed.json.",
            AutoSize = false,
            Height = 50,
            Dock = DockStyle.Fill,
            ForeColor = Color.LightGray
        };
        AddGridNote(grid, note);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage MakeTabPage(string title)
    {
        var page = new TabPage(title)
        {
            BackColor = SurfaceColor,
            Padding = new Padding(8),
            AutoScroll = true
        };
        return page;
    }

    private static TableLayoutPanel CreateConfigGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            BackColor = SurfaceColor,
            ColumnCount = 3,
            RowCount = 0,
            Padding = new Padding(8),
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        return grid;
    }

    private static int AddGridRow(TableLayoutPanel grid, int rowHeight)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        return row;
    }

    private void AddTextField(TableLayoutPanel grid, string key, string label)
    {
        int row = AddGridRow(grid, 50);
        var caption = MakeCaption(label, Point.Empty);
        caption.Dock = DockStyle.Fill;
        caption.Margin = new Padding(6, 4, 8, 4);
        var input = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(6, 7, 6, 7) };
        StyleTextBox(input);
        _fields[key] = input;
        grid.Controls.Add(caption, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.SetColumnSpan(input, 2);
    }

    private void AddAssetField(TableLayoutPanel grid, string key, string label, string section, string fileName)
    {
        int row = AddGridRow(grid, 50);
        var caption = MakeCaption(label, Point.Empty);
        caption.Dock = DockStyle.Fill;
        caption.Margin = new Padding(6, 4, 8, 4);
        var input = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Margin = new Padding(6, 7, 6, 7) };
        StyleTextBox(input);
        var browse = MakeButton("CHỌN ẢNH", outlined: true);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(6, 6, 6, 6);
        browse.Click += (_, _) => ImportAsset(input, section, fileName);
        _fields[key] = input;
        grid.Controls.Add(caption, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.Controls.Add(browse, 2, row);
    }

    private void AddEditorField(TableLayoutPanel grid, string label, TextBox input, bool multiline = false)
    {
        int row = AddGridRow(grid, multiline ? 90 : 50);
        var caption = MakeCaption(label, Point.Empty);
        caption.Dock = DockStyle.Fill;
        caption.Margin = new Padding(6, 4, 8, 4);
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(6, 7, 6, 7);
        input.Multiline = multiline;
        input.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
        StyleTextBox(input);
        grid.Controls.Add(caption, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.SetColumnSpan(input, 2);
    }

    private void AddAssetEditorField(TableLayoutPanel grid, string label, TextBox input, string section, string fileName)
    {
        int row = AddGridRow(grid, 50);
        var caption = MakeCaption(label, Point.Empty);
        caption.Dock = DockStyle.Fill;
        caption.Margin = new Padding(6, 4, 8, 4);
        input.Dock = DockStyle.Fill;
        input.ReadOnly = true;
        input.Margin = new Padding(6, 7, 6, 7);
        StyleTextBox(input);
        var browse = MakeButton("CHỌN POSTER", outlined: true);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(6, 6, 6, 6);
        browse.Click += (_, _) => ImportNewsImage(section, fileName);
        grid.Controls.Add(caption, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.Controls.Add(browse, 2, row);
    }

    private void AddGridNote(TableLayoutPanel grid, Label note)
    {
        int row = AddGridRow(grid, 58);
        note.Dock = DockStyle.Fill;
        note.Margin = new Padding(6);
        grid.Controls.Add(note, 0, row);
        grid.SetColumnSpan(note, 3);
    }

    private void BrowseTarget()
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Chọn thư mục Launcher hoặc thư mục Release có appsettings.json",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_targetFolder.Text) ? _targetFolder.Text : AppContext.BaseDirectory
        };
        if (picker.ShowDialog(this) == DialogResult.OK)
            LoadTarget(picker.SelectedPath);
    }

    private void LoadTarget(string folder)
    {
        string configPath = Path.Combine(folder, "appsettings.json");
        if (!File.Exists(configPath))
        {
            MessageBox.Show(this, "Không tìm thấy appsettings.json trong thư mục đã chọn.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _document = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                ?? throw new InvalidDataException("Cấu hình JSON cần có một object ở cấp gốc.");
            _targetFolder.Text = folder;
            _editingNewsIndex = -1;
            LoadFieldsFromDocument();
            LoadNewsList(0);
            _status.Text = $"Đã nạp: {configPath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không thể nạp cấu hình", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadFieldsFromDocument()
    {
        foreach ((string key, TextBox input) in _fields)
        {
            input.Text = ReadPath(_document, key);
            if (!HasPath(_document, key))
            {
                if (key.Equals("Launcher.SignIn.AccountType", StringComparison.OrdinalIgnoreCase))
                    input.Text = "FROZEVERSE ACCOUNT";
                else if (key.Equals("Launcher.SignIn.AccountTypeVn", StringComparison.OrdinalIgnoreCase))
                    input.Text = "TÀI KHOẢN FROZEVERSE";
            }
        }
    }

    private void SaveConfig()
    {
        if (_document is null || string.IsNullOrWhiteSpace(_targetFolder.Text))
        {
            MessageBox.Show(this, "Hãy chọn thư mục launcher trước.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            SaveCurrentNewsToDocument();
            foreach ((string key, TextBox input) in _fields)
                WritePath(_document, key, input.Text.Trim());
            GetObject(GetObject(_document, "Launcher", create: true), "Branding", create: true)
                .Remove("GameName");

            string path = Path.Combine(_targetFolder.Text, "appsettings.json");
            string temp = path + ".tmp";
            string json = _document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                string backupPath = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".bak";
                File.Copy(path, backupPath, overwrite: false);
            }
            File.Move(temp, path, overwrite: true);
            _status.Text = $"Đã lưu cấu hình: {path}";
            LoadNewsList(Math.Max(0, _editingNewsIndex));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không thể lưu cấu hình", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportAsset(TextBox target, string section, string fileName)
    {
        using var picker = ImagePicker();
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            string relative = CopyAsset(picker.FileName, section, fileName);
            target.Text = relative;
            _status.Text = $"Đã import {Path.GetFileName(picker.FileName)}. Bấm LƯU CẤU HÌNH để lưu đường dẫn.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không thể import ảnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportNewsImage(string section, string fileName)
    {
        using var picker = ImagePicker();
        if (picker.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _newsImagePath.Text = CopyAsset(picker.FileName, section, fileName);
            _status.Text = $"Đã import poster {Path.GetFileName(picker.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không thể import poster", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private OpenFileDialog ImagePicker() => new()
    {
        Title = "Chọn ảnh",
        Filter = "Ảnh PNG/JPEG/BMP|*.png;*.jpg;*.jpeg;*.bmp|Tất cả file|*.*",
        CheckFileExists = true,
        Multiselect = false
    };

    private string CopyAsset(string source, string section, string fileName)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        string safeName = fileName + extension;
        string relativeDirectory = Path.Combine("Assets", "Configurator", section);
        string absoluteDirectory = Path.Combine(_targetFolder.Text, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);
        string destination = Path.Combine(absoluteDirectory, safeName);
        if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            File.Copy(source, destination, overwrite: true);
        return Path.Combine(relativeDirectory, safeName).Replace('\\', '/');
    }

    private void LoadNewsList(int preferredIndex = 0)
    {
        _loadingNews = true;
        _newsList.Items.Clear();
        JsonArray? posts = GetPosts(create: false);
        if (posts is not null)
        {
            foreach (JsonNode? post in posts)
                _newsList.Items.Add(post is JsonObject obj ? Get(obj, "Title") : "(bài viết)");
        }
        if (_newsList.Items.Count > 0)
            _newsList.SelectedIndex = Math.Clamp(preferredIndex, 0, _newsList.Items.Count - 1);
        else
            ClearNewsEditor();
        _loadingNews = false;
        _editingNewsIndex = -1;
        LoadSelectedNews();
    }

    private void LoadSelectedNews()
    {
        JsonArray? posts = GetPosts(create: false);
        int index = _newsList.SelectedIndex;
        if (posts is null || index < 0 || index >= posts.Count || posts[index] is not JsonObject post)
        {
            _editingNewsIndex = -1;
            ClearNewsEditor();
            return;
        }

        _loadingNews = true;
        _editingNewsIndex = index;
        _newsTitleEn.Text = Get(post, "Title");
        _newsTitleVn.Text = Get(post, "TitleVn");
        _newsSummaryEn.Text = Get(post, "Summary");
        _newsSummaryVn.Text = Get(post, "SummaryVn");
        _newsDate.Text = Get(post, "PublishedAt");
        _newsUrl.Text = Get(post, "ArticleUrl");
        _newsImagePath.Text = Get(post, "Image");
        _loadingNews = false;
    }

    private void AddNewsPost()
    {
        SaveCurrentNewsToDocument();
        JsonArray posts = GetPosts(create: true)!;
        posts.Add(new JsonObject
        {
            ["Title"] = "New update",
            ["TitleVn"] = "Cập nhật mới",
            ["Summary"] = "",
            ["SummaryVn"] = "",
            ["Image"] = "",
            ["PublishedAt"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["ArticleUrl"] = ""
        });
        LoadNewsList(posts.Count - 1);
        _newsTitleEn.Focus();
    }

    private void RemoveNewsPost()
    {
        JsonArray? posts = GetPosts(create: false);
        int index = _editingNewsIndex;
        if (posts is null || index < 0 || index >= posts.Count)
            return;

        posts.RemoveAt(index);
        LoadNewsList(Math.Max(0, index));
    }

    private void SaveCurrentNewsToDocument()
    {
        JsonArray? posts = GetPosts(create: false);
        int index = _editingNewsIndex;
        if (posts is null || index < 0 || index >= posts.Count || posts[index] is not JsonObject post)
            return;

        post["Title"] = _newsTitleEn.Text.Trim();
        post["TitleVn"] = _newsTitleVn.Text.Trim();
        post["Summary"] = _newsSummaryEn.Text.Trim();
        post["SummaryVn"] = _newsSummaryVn.Text.Trim();
        post["PublishedAt"] = _newsDate.Text.Trim();
        post["ArticleUrl"] = _newsUrl.Text.Trim();
        post["Image"] = _newsImagePath.Text.Trim();
    }

    private void ConfirmCurrentNews()
    {
        int index = _editingNewsIndex;
        SaveCurrentNewsToDocument();
        JsonArray? posts = GetPosts(create: false);
        if (posts is null || index < 0 || index >= posts.Count)
            return;

        _loadingNews = true;
        _newsList.Items[index] = string.IsNullOrWhiteSpace(_newsTitleEn.Text)
            ? "(bài viết mới)"
            : _newsTitleEn.Text.Trim();
        _newsList.SelectedIndex = index;
        _loadingNews = false;
        _status.Text = "Đã xác nhận nội dung bài viết. Bấm LƯU CẤU HÌNH để ghi file.";
    }

    private static string? FindNearbyLauncherFolder()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int depth = 0; current is not null && depth < 10; depth++, current = current.Parent)
        {
            string releaseLauncher = Path.Combine(current.FullName, "Launcher", "bin", "Release", "net8.0-windows");
            if (File.Exists(Path.Combine(releaseLauncher, "appsettings.json")))
                return releaseLauncher;

            string launcherFolder = Path.Combine(current.FullName, "Launcher");
            if (File.Exists(Path.Combine(launcherFolder, "appsettings.json")))
                return launcherFolder;

            if (File.Exists(Path.Combine(current.FullName, "appsettings.json")))
                return current.FullName;
        }
        return null;
    }

    private void ClearNewsEditor()
    {
        _newsTitleEn.Clear();
        _newsTitleVn.Clear();
        _newsSummaryEn.Clear();
        _newsSummaryVn.Clear();
        _newsDate.Clear();
        _newsUrl.Clear();
        _newsImagePath.Clear();
    }

    private JsonArray? GetPosts(bool create)
    {
        if (_document is null)
            return null;
        JsonObject launcher = GetObject(_document, "Launcher", create);
        JsonObject news = GetObject(launcher, "News", create);
        if (news["Posts"] is JsonArray posts)
            return posts;
        if (!create)
            return null;
        posts = new JsonArray();
        news["Posts"] = posts;
        return posts;
    }

    private string ReadPath(JsonObject? root, string dottedPath)
    {
        JsonNode? current = root;
        foreach (string part in dottedPath.Split('.'))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(part, out current))
                return "";
        }
        return current?.GetValue<string>() ?? "";
    }

    private static bool HasPath(JsonObject? root, string dottedPath)
    {
        JsonNode? current = root;
        foreach (string part in dottedPath.Split('.'))
        {
            if (current is not JsonObject obj || !obj.TryGetPropertyValue(part, out current))
                return false;
        }
        return true;
    }

    private void WritePath(JsonObject root, string dottedPath, string value)
    {
        string[] parts = dottedPath.Split('.');
        JsonObject current = root;
        for (int index = 0; index < parts.Length - 1; index++)
            current = GetObject(current, parts[index], create: true);
        current[parts[^1]] = value;
    }

    private static JsonObject GetObject(JsonObject parent, string key, bool create)
    {
        if (parent[key] is JsonObject existing)
            return existing;
        if (!create)
            return new JsonObject();
        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static string Get(JsonObject obj, string key) => obj[key]?.GetValue<string>() ?? "";

    private static Label MakeCaption(string text, Point location) => new()
    {
        Text = text,
        Location = location,
        Size = new Size(300, 28),
        ForeColor = Color.Gainsboro,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void StyleTextBox(TextBox input)
    {
        input.BackColor = Color.FromArgb(42, 42, 48);
        input.ForeColor = Color.White;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = new Font("Segoe UI", 9.5F);
    }

    private static Button MakeButton(string text, bool outlined)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = outlined ? SurfaceColor : AccentColor,
            ForeColor = outlined ? Color.White : Color.FromArgb(25, 19, 10),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = outlined ? 1 : 0;
        button.FlatAppearance.BorderColor = Color.FromArgb(130, 130, 135);
        return button;
    }
}
