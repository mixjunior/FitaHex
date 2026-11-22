# FitaHex

[Português (Brasil)](README.pt-BR.md)

FitaHex is a portable Windows game hub. It brings together launcher games, standalone executables, and ROMs in a local library. Keep favorites, group games into collections, mark games as completed, and check your playtime and history.

## Getting started

1. Keep the executable in a folder you can write to and open it. On the first run, choose whether to start it with Windows or create a desktop shortcut. You can skip the three-step tour and revisit it under **Configurações → Biblioteca → Ver mini tour**.
2. Click **Atualizar jogos** (Update games). The summary shows installed and available games per launcher, plus covers and playtime updated during the scan. Reopen it through **Ver resumo** (View summary) in the status bar.
3. Select **Adicionar** (Add) to add a standalone game or choose an emulator and its ROM folder. FitaHex scans subfolders. Choose **Jogar** (Play) to launch the selected game.
4. The window close button sends FitaHex to the Windows notification area. Click its icon to reopen it, or right-click the icon and select **Encerrar FitaHex** to quit.

## Launchers and imports

| Platform | Library and playtime |
| --- | --- |
| Steam | Detects local installations. Available games and playtime depend on the local Steam cache. |
| Epic Games | Detects installed games from local manifests. Historical playtime depends on compatible cached data from GOG Galaxy. |
| Xbox / Microsoft Store | Detects games registered in Windows. Playtime depends on compatible cached data from Playnite or GOG Galaxy. |
| GOG | Detects installed games and reads available games and playtime from the local GOG Galaxy database when present. |
| Riot Games | Detects local installations and lists supported games available through Riot Client. The hub can track new sessions when it identifies the game process. Historical account totals are not imported directly. |
| EA app and Battle.net | Available games are imported when they appear in the GOG Galaxy cache. Direct account library sync is not available. |
| Ubisoft Connect | Available as a platform filter and Social profile. Playtime can be picked up from compatible GOG Galaxy records. |

The original launcher handles installation. Covers depend on availability. WebP, AVIF, and HEIF may require the corresponding Windows image codecs. Discord Rich Presence requires the desktop Discord client and activity sharing enabled. The Social panel requires the Microsoft Edge WebView2 Runtime.

## Build from source

On 64-bit Windows with .NET Framework 4.8, open PowerShell in the repository and run:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

The script downloads the pinned WebView2 SDK from NuGet and builds **dist/FitaHex.exe**. The **-Test** option runs the automated checks in an isolated directory. To use a local SDK, pass **-WebView2SdkPath** with the path to the extracted package.

## Local data

The library, preferences, artwork cache, and local web sessions are stored in **%LOCALAPPDATA%\FitaHex** for the current Windows user. Open the folder via **Configurações → Backup → Abrir pasta de dados** (Settings → Backup → Open data folder). You can move the executable without moving your data.

To migrate from an older version, close it from the notification area and open the new executable once next to the existing **NexusData** folder. If the new location has no library yet, FitaHex copies the library and supported data and leaves the original intact. For a move to another PC, export a backup in **Configurações → Backup**.

When you enable startup in **Configurações → Windows** (Settings → Windows), the startup shortcut points to a managed copy under **%LOCALAPPDATA%\FitaHex\Startup** so it keeps working if you move or rename the downloaded executable. Opening an updated version refreshes that copy.

Launcher names and logos belong to their respective owners.
