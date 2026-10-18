# MyGameLauncherTest

Bộ test WinForms Launcher cho game Unity.

## Thành phần

- `Launcher`: WinForms launcher
- `TestGame`: chương trình giả lập game để test nút PLAY
- `ManifestTool`: tạo `manifest.json` và SHA-256 cho thư mục file game
- `AuthServer`: API xác thực thử nghiệm local, dùng seed user riêng

## Yêu cầu

- Windows 10/11
- Visual Studio 2022 hoặc .NET 8 SDK
- Workload: .NET desktop development

## 1. Build Launcher + TestGame

Mở `MyGameLauncherTest.sln`, sau đó Build Solution.

Hoặc dùng terminal:

```powershell
dotnet build -c Release
```

## 2. Publish TestGame

Tạo bản publish self-contained single-file để launcher có thể chạy độc lập:

```powershell
dotnet publish .\TestGame\TestGame.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\TestServer\files
```

Sau lệnh trên, `TestServer/files` sẽ chứa `TestGame.exe`.

## 3. Tạo version.txt

```powershell
Set-Content .\TestServer\files\version.txt "1.0.0"
```

## 4. Build ManifestTool

```powershell
dotnet build .\ManifestTool\ManifestTool.csproj -c Release
```

Sau đó tạo manifest:

```powershell
dotnet run --project .\ManifestTool\ManifestTool.csproj -- `
  .\TestServer\files `
  1.0.0 `
  TestGame.exe
```

Lúc này phải có:

```text
TestServer\
├── manifest.json
└── files\
    ├── TestGame.exe
    └── version.txt
```

## 5. Chạy local update server

Mở PowerShell ở thư mục `TestServer`:

```powershell
python -m http.server 8080
```

Kiểm tra trình duyệt:

```text
http://127.0.0.1:8080/manifest.json
```

## 6. Chạy Launcher

Sau khi build:

```text
Launcher\bin\Release\net8.0-windows\
```

Chạy:

```text
Launcher.exe
```

Launcher sẽ:

1. Tải manifest.
2. Kiểm tra từng file trong thư mục `Game`.
3. File thiếu/sai SHA-256 sẽ được tải từ server.
4. Verify SHA-256.
5. Hiện nút `PLAY GAME`.
6. Chạy `TestGame.exe`.

## 7. Test update

Lần đầu:

```text
Version 1.0.0
```

Sau đó sửa:

```powershell
Set-Content .\TestServer\files\version.txt "1.0.1"
```

và chạy lại:

```powershell
dotnet run --project .\ManifestTool\ManifestTool.csproj -- `
  .\TestServer\files `
  1.0.1 `
  TestGame.exe
```

Nếu `version.txt` thay đổi kích thước hoặc SHA-256, launcher sẽ tải lại file đó.

## 8. Dùng với Unity

Khi chuyển sang Unity:

- Thay `TestGame.exe` bằng `MyGame.exe`.
- Thay `TestServer/files` bằng thư mục build Unity.
- Giữ nguyên manifest/update flow.
- `Launcher/appsettings.json` đổi `GameExe` thành tên exe Unity.

Ví dụ:

```json
{
  "Launcher": {
    "BaseUrl": "https://cdn.example.com/mygame/",
    "ManifestPath": "manifest.json",
    "GameDirectory": "Game",
    "GameExe": "MyGame.exe",
    "DownloadPath": "files",
    "RequestTimeoutSeconds": 60,
    "CloseAfterGameStarts": true
  }
}
```

## 9. Cấu hình giao diện và tài khoản

File cấu hình trung tâm là `Launcher/appsettings.json` (khi chạy, launcher đọc file `appsettings.json` cạnh `Launcher.exe`). Cấu hình được chia theo nhóm để thay ảnh, nội dung và URL mà không sửa mã nguồn:

- `Branding.Logo`: đường dẫn ảnh logo thương hiệu (nên dùng PNG có cả biểu tượng và tên game). Launcher hiển thị ảnh này thay cho tên game dạng chữ. Nếu config cũ còn `Branding.GameName`, launcher không dùng trường đó nữa và Configurator sẽ xóa trường cũ khi lưu.
- `NavigationOrder`: thứ tự Play, News, Purchase trên header. Sign in là action riêng trên header như ảnh tham khảo.
- `Language`: chỉ bật EN và VN; `Default` chọn ngôn ngữ mở đầu.
- `Play`: ảnh nền, chữ nút `PlayButtonText`/`PlayButtonTextVn` và yêu cầu đăng nhập. Chưa đăng nhập → Sign in; đã đăng nhập → Play. Nút Play gọi luồng kiểm tra/cập nhật manifest rồi chạy `GameExe`.
- `News.Posts`: danh sách bài viết. Dùng `Title`/`Summary` cho EN và `TitleVn`/`SummaryVn` cho VN. Ảnh và logo dùng đường dẫn tương đối từ thư mục chứa `Launcher.exe`; `ArticleUrl` mở bài viết đầy đủ.
- `Purchase.Url`: website mua hàng; có thể mở bằng trình duyệt hệ thống.
- `SignIn`: ảnh panel trái và nền toàn cửa sổ, heading/mô tả (có trường VN riêng), loại tài khoản `AccountType`/`AccountTypeVn`, cùng link đăng ký.
- `SignIn.Api`: địa chỉ API cùng endpoint login, lấy hồ sơ user và logout. Không đặt API key bí mật hoặc mật khẩu trong file này.
- Các mục `BaseUrl`, `ManifestPath`, `GameDirectory`, `GameExe` và `DownloadPath` hiện cấu hình hệ thống cập nhật/chạy game.

Đặt các file ảnh theo đúng ví dụ, hoặc sửa đường dẫn trong JSON:

```text
Launcher/
├── Launcher.exe
├── appsettings.json
└── Assets/
    ├── launcher-logo.png
    ├── play-background.jpg
    ├── sign-in-background.jpg
    └── News/
        └── welcome.jpg
```

### Dùng LauncherConfigurator.exe để cập nhật giao diện

Đã có công cụ giao diện để thay logo, wallpaper, ảnh poster, nội dung News, chữ trên trang Play/Sign in và các URL mà không phải tự sửa JSON:

```text
LauncherConfigurator\publish\win-x64\LauncherConfigurator.exe
```

1. Mở `LauncherConfigurator.exe`. Nếu tìm thấy bản Release gần đó, công cụ sẽ nạp `Launcher\bin\Release\net8.0-windows` tự động. Nếu chưa có, bấm **Chọn thư mục** và chọn đúng thư mục có `Launcher.exe` cùng `appsettings.json`.
2. Trong **Thương hiệu & giao diện**, cập nhật tên game, logo, wallpaper mặc định/toàn cửa sổ cho từng tab, chữ nút Play, URL Purchase/đăng ký và đường dẫn máy chủ cập nhật/game.
3. Trong **Bài viết News**, chọn bài để sửa hoặc bấm **+ Thêm**. Có thể nhập title/summary EN và VN, ngày, link bài viết, rồi dùng **Import poster...** để chép ảnh poster vào thư mục Assets của launcher.
4. Trong **Sign in & nội dung**, chỉnh heading/mô tả và các endpoint API. Công cụ không lưu username, mật khẩu hay API secret.
5. Bấm **LƯU CẤU HÌNH**, đóng rồi mở lại launcher. Trước khi thay file, công cụ tạo bản sao `appsettings.json.<timestamp>.bak` cạnh cấu hình.

Ảnh được chép vào `Assets/Configurator/...` trong đúng thư mục launcher đã chọn; JSON chỉ giữ đường dẫn tương đối. Các trường ảnh trong tab Thương hiệu và giao diện có nút **Chọn ảnh...**, còn News có **Import poster...**. Nếu sửa thư mục `Launcher\bin\Release\net8.0-windows`, thay đổi dùng được ngay khi chạy launcher từ đó. Nếu chọn thư mục mã nguồn `Launcher`, hãy build lại launcher để các asset và cấu hình được chép sang thư mục Release.

Trong **Bài viết News**, bấm **+ Thêm** để tạo bài, nhập nội dung rồi bấm **XÁC NHẬN BÀI VIẾT** để ghi nội dung vào bài đang chọn. Chọn một bài khác để sửa bài đó; **LƯU CẤU HÌNH** sẽ lưu danh sách bài riêng biệt và giữ bài đang chọn. Công cụ tạo bản sao lưu cấu hình trước khi lưu.

Wallpaper được vẽ phủ toàn bộ bề mặt cửa sổ launcher theo chế độ **cover** (có thể cắt mép ảnh để lấp đầy khung). Mỗi tab có thể có wallpaper riêng; nếu ô nền tab để trống, launcher dùng nền mặc định `Branding.BackgroundImage`. Header/footer trong suốt; ảnh Sign in và poster News không có lớp filter đen. Nút Play/News/Purchase trên header dùng Quicksand đậm, cỡ 20 pt, và chiều rộng tự đo theo nhãn. Khung Sign in dùng hàng gọn hơn để giữ đủ nút và link ở các kích thước hỗ trợ.

Ảnh minh họa bên trái của Sign in được hiển thị trong khung bằng khoảng 2/3 chiều rộng và chiều cao panel, căn giữa và không crop ảnh. Khi trang Sign in mở, ảnh tự chuyển động lên 0,5 cm trong 1 giây rồi trở về vị trí ban đầu trong 1 giây; chuyển động dừng khi rời trang.

Logo chỉ xuất hiện trong vùng riêng ở Header và được co theo chế độ Zoom để giữ trọn ảnh. Trang Play không lặp lại logo; phiên bản máy chủ nằm dưới các nút kiểm tra/sửa. Khi đổi trang, một lớp Wipe tối quét qua vùng nội dung: phủ trang cũ, đổi trang khi đã phủ kín rồi mở trang mới theo cùng hướng trong tổng cộng 1 giây. Header/footer giữ nguyên trong lúc chuyển cảnh và thao tác trong nội dung tạm khóa để tránh nhấn nhầm. Thanh tải dùng các block xanh neon có đầu laser; nút chính dùng nền tối cùng viền cyan/magenta. Các nút và điểm nhấn giao diện trước đây màu vàng dùng cùng bảng màu cyberpunk này.

Trang Sign in chia thêm chiều rộng cho form; nhãn và ô Username/Password dùng cỡ 17 pt, điều khoản và liên kết tạo tài khoản dùng cỡ 16 pt. Nút hiện mật khẩu dùng nhãn SHOW/HIDE để dễ nhận biết và không bị lỗi glyph.

Trong **Thương hiệu & giao diện**, dùng nút import ở trường **Logo có tên game (ảnh PNG)** để thay ảnh thương hiệu. Trong **Sign in & nội dung**, chỉnh `Loại tài khoản — English/Tiếng Việt` để thay dòng như “FROZEVERSE ACCOUNT”.

Launcher dùng font **Quicksand** từ file local `Assets/Fonts/Quicksand[wght].ttf`; `Assets/Fonts/OFL.txt` đi kèm giấy phép Open Font License. File font được chép cùng bản build, nên máy người chơi không cần cài Quicksand riêng.

Có thể chạy smoke check giao diện và lưu News trên bản sao tạm của cấu hình bằng PowerShell STA:

```powershell
pwsh -NoProfile -STA -File .\LauncherConfigurator\qa-smoke.ps1
```

Script kiểm tra các cỡ cửa sổ, thời gian Wipe, giới hạn và kiểu chữ Sign in, nút hiện mật khẩu, tiến độ cyberpunk, trạng thái nút sau đăng nhập giả lập, thao tác xác nhận/sửa/lưu/nạp lại nhiều bài News và tự xóa thư mục tạm sau khi kiểm tra đường dẫn an toàn.

### Hợp đồng API đăng nhập

Launcher gửi `POST {BaseUrl}{LoginPath}` với thuộc tính username theo `LoginIdentifierProperty` và thuộc tính `password`. API trả JSON có token top-level theo `TokenResponseProperty` (mặc định `accessToken`). Launcher gọi `GET {BaseUrl}{UserProfilePath}` bằng `Authorization: Bearer <token>` để lấy hồ sơ. Khi đăng xuất, launcher gọi `POST {BaseUrl}{LogoutPath}` rồi xóa token cục bộ. Token được bảo vệ bằng Windows DPAPI; mật khẩu không được lưu ở launcher.

Launcher dựng header và các trang News, Play, Sign in từ cấu hình; tab News mở đầu tiên, EN/VN đổi nhãn giao diện, Purchase và Create account mở URL cấu hình. Cấu hình dev hiện trỏ tới API thử nghiệm local tại `http://127.0.0.1:8090/`.

### Tài khoản thử nghiệm Lucas

Project có API xác thực dev trong `AuthServer`; dữ liệu seed nằm tại `AuthServer/SeedData/users.seed.json`, tách khỏi `TestServer` để Python static server không phát file tài khoản. File chỉ chứa PBKDF2-HMAC-SHA256 hash, salt và thông tin hồ sơ; không chèn mật khẩu rõ vào `appsettings.json`.

Để chạy launcher local, mở hai cửa sổ terminal:

```powershell
# Cửa sổ 1: máy chủ manifest/file game
cd .\TestServer
py -m http.server 8080 --bind 127.0.0.1
```

```powershell
# Cửa sổ 2: từ thư mục gốc MyGameLauncherTest
dotnet run --project .\AuthServer\AuthServer.csproj
```

Giữ cả hai cửa sổ mở. API tài khoản chỉ bind loopback cổng `8090`; kiểm tra bằng `http://127.0.0.1:8090/health`. Đăng nhập launcher bằng username `Lucas` và mật khẩu demo bạn đã chọn. Đây là dữ liệu thử nghiệm với mật khẩu yếu, chỉ dùng local; không dùng cho tài khoản thật hay production.

Để thêm/sửa user, cập nhật mảng `Users` trong `AuthServer/SeedData/users.seed.json` bằng Username, DisplayName, Email, PasswordHashAlgorithm, PasswordIterations, PasswordSalt và PasswordHash. Tạo salt mới cho từng user và tính lại hash PBKDF2 từ mật khẩu dự kiến; không thêm trường `Password` dạng rõ. API đọc seed lúc khởi động nên cần restart `AuthServer` sau khi sửa file. OWASP hiện khuyến nghị PBKDF2-HMAC-SHA256 với 600.000 iterations khi chọn PBKDF2; xem [Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).

News hiện lấy danh sách tĩnh từ `News.Posts` trong `appsettings.json`; sửa bài hoặc poster thì cần cập nhật file cấu hình cạnh launcher. Muốn cập nhật bài viết từ server mà không phát hành lại file cấu hình, cần bổ sung news endpoint riêng.

### Luồng vận hành

1. Launcher mở News làm tab đầu tiên và hiển thị `News.Posts`.
2. Chọn Play: nếu chưa có session hợp lệ thì đưa người dùng tới Sign in; nếu đã đăng nhập thì hiện trang Play.
3. Sign in gửi username/mật khẩu tới API, lấy token và hồ sơ người dùng; launcher chỉ lưu token được Windows bảo vệ.
4. Play kiểm tra `manifest.json`, tải và xác minh file game rồi mới bật nút chạy.
5. Purchase mở URL đã cấu hình. Create account mở `SignIn.CreateAccountUrl`.

Để đóng gói Unity, đặt `GameExe` thành tên executable thật (thường `TênGame.exe`), publish toàn bộ thư mục build Unity lên máy chủ file/CDN, tạo manifest chứa executable và các data files, rồi trỏ `BaseUrl` tới thư mục phát hành. Không dùng `TestGame.exe` trong bản phát hành thật.

## Lưu ý production

Bộ này là MVP để test:

- Chưa có dịch vụ tài khoản production; `AuthServer` chỉ là API dev local.
- Chưa có launcher self-update.
- Chưa có delta patch/chunk patch.
- Chưa có digital signature cho manifest.
- Chưa có resumable download.
- `REPAIR` hiện tại xóa file manifest rồi tải lại, phù hợp để demo.

Khi chuyển production nên thêm HTTPS/CDN, ký manifest, resumable download, lock file, atomic replace và launcher updater riêng.
