param(
    [string]$WebView2SdkPath,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw 'Instale o .NET Framework 4.8 no Windows de 64 bits.'
}

$version = '1.0.2365.46'
if (!$WebView2SdkPath) {
    $cache = Join-Path $PSScriptRoot '.packages'
    $WebView2SdkPath = Join-Path $cache "microsoft.web.webview2.$version"
    if (!(Test-Path -LiteralPath (Join-Path $WebView2SdkPath 'lib\net45\Microsoft.Web.WebView2.Core.dll'))) {
        New-Item -ItemType Directory -Path $cache -Force | Out-Null
        $archive = Join-Path $cache "webview2-$version.zip"
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $url = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg"
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
        Expand-Archive -LiteralPath $archive -DestinationPath $WebView2SdkPath -Force
    }
}

$managed = Join-Path $WebView2SdkPath 'lib\net45'
$loader = Join-Path $WebView2SdkPath 'runtimes\win-x64\native\WebView2Loader.dll'
if (!(Test-Path -LiteralPath $managed)) {
    $managed = $WebView2SdkPath
    $loader = Join-Path $WebView2SdkPath 'WebView2Loader.dll'
}

$core = Join-Path $managed 'Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $managed 'Microsoft.Web.WebView2.WinForms.dll'
$resources = foreach ($library in @($core, $forms, $loader)) {
    if (!(Test-Path -LiteralPath $library)) { throw "Arquivo do SDK ausente - $library" }
    $fullPath = (Resolve-Path -LiteralPath $library).Path
    "/resource:$fullPath,Nexus.WebView.$([IO.Path]::GetFileName($fullPath))"
}

$source = Join-Path $PSScriptRoot 'src'
$resources += @(Get-ChildItem -LiteralPath (Join-Path $source 'marks') -Filter '*.png' |
    ForEach-Object { "/resource:$($_.FullName),Nexus.Marks.$($_.Name)" })
$references = @(
    '/r:System.Web.Extensions.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
    '/r:System.Xml.Linq.dll', '/r:System.Xaml.dll', '/r:Microsoft.CSharp.dll',
    "/r:$framework\WPF\PresentationCore.dll", "/r:$framework\WPF\WindowsBase.dll",
    "/r:$((Resolve-Path -LiteralPath $core).Path)", "/r:$((Resolve-Path -LiteralPath $forms).Path)"
)
$files = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File | ForEach-Object FullName)
$dist = (New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'dist') -Force).FullName
$output = $dist
if ($Test) {
    $output = (New-Item -ItemType Directory -Path (Join-Path $dist ('qa-' + [guid]::NewGuid().ToString('N')))).FullName
}
$exe = Join-Path $output 'FitaHex.exe'
$icon = Join-Path $source 'FitaHex.ico'
& $compiler /nologo /target:winexe /platform:x64 /main:Program /optimize+ "/win32icon:$icon" "/out:$exe" @references @resources @files
if ($LASTEXITCODE -ne 0) { throw "Falha na compilação (código $LASTEXITCODE)." }

if ($Test) {
    $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    $report = Join-Path $output 'FitaHex\self-test.txt'
    if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
    if ($process.ExitCode -ne 0) { throw "Falha nos testes (código $($process.ExitCode))." }
    Copy-Item -LiteralPath $exe -Destination (Join-Path $dist 'FitaHex.exe') -Force
}
Write-Output (Join-Path $dist 'FitaHex.exe')
