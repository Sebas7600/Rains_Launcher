
$ErrorActionPreference = "Stop"

Write-Host "== MyGameLauncherTest setup =="

dotnet --version | Out-Host

New-Item -ItemType Directory -Force .\TestServer\files | Out-Null

dotnet publish .\TestGame\TestGame.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\TestServer\files

Set-Content .\TestServer\files\version.txt "1.0.0"

dotnet run --project .\ManifestTool\ManifestTool.csproj -- `
  .\TestServer\files `
  1.0.0 `
  TestGame.exe

Write-Host ""
Write-Host "Done. Start server with:"
Write-Host "  python -m http.server 8080 --directory .\TestServer"
Write-Host ""
Write-Host "Then run:"
Write-Host "  .\Launcher\bin\Release\net8.0-windows\Launcher.exe"
