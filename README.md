# FitaHex

[English](#english) · [Português (Brasil)](#portugues-brasil)

## English

FitaHex is a portable Windows game hub. It brings together launcher games, standalone executables, and ROMs in a local library. Keep favorites, group games into collections, mark games as completed, and check your playtime and history.

### Interface language

FitaHex is available in English and Brazilian Portuguese. By default, it starts in Portuguese when Windows uses Portuguese, and in English for other Windows display languages. To change the language, go to **Settings → Appearance → Language**, choose **Windows language**, **Português (Brasil)**, or **English**, and save. The app reopens in the selected language and keeps your library. Launcher pages in Social use the language set by each service.

### Getting started

1. Keep the executable in a folder you can write to and open it. On the first run, choose whether to start it with Windows or create a desktop shortcut. You can skip the three-step tour and revisit it under **Settings → Library → View quick tour**.
2. Click **Update games**. The summary shows installed and available games per launcher, plus covers and playtime updated during the scan. Reopen it through **View summary** in the status bar.
3. Select **Add** to add a standalone game or choose an emulator and its ROM folder. FitaHex scans subfolders. Choose **Play** to launch the selected game.
4. The window close button sends FitaHex to the Windows notification area. Click its icon to reopen it, or right-click the icon and select **Exit FitaHex** to quit.

### Launchers and imports

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

### Build from source

On 64-bit Windows with .NET Framework 4.8, open PowerShell in the repository and run:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

The script downloads the pinned WebView2 SDK from NuGet and builds **dist/FitaHex.exe**. The **-Test** option runs the automated checks in an isolated directory. To use a local SDK, pass **-WebView2SdkPath** with the path to the extracted package.

### Local data

The library, preferences, artwork cache, and local web sessions are stored in **%LOCALAPPDATA%\FitaHex** for the current Windows user. Open the folder via **Settings → Backup → Open data folder**. You can move the executable without moving your data.

To migrate from an older version, close it from the notification area and open the new executable once next to the existing **NexusData** folder. If the new location has no library yet, FitaHex copies the library and supported data and leaves the original intact. For a move to another PC, export a backup in **Settings → Backup**.

When you enable startup in **Settings → Windows**, the startup shortcut points to a managed copy under **%LOCALAPPDATA%\FitaHex\Startup** so it keeps working if you move or rename the downloaded executable. Opening an updated version refreshes that copy.

Launcher names and logos belong to their respective owners.

<a id="portugues-brasil"></a>

## Português (Brasil)

O FitaHex é um hub portátil de jogos para Windows. Ele reúne jogos de launchers, executáveis avulsos e ROMs em uma biblioteca local. Você pode favoritar jogos, organizá-los em coleções, marcar os que zerou e consultar o tempo jogado e o histórico.

### Idioma da interface

O FitaHex está disponível em português do Brasil e inglês. Por padrão, ele abre em português quando esse é o idioma do Windows, e em inglês nos demais casos. Para mudar, abra **Configurações → Aparência → Idioma**, escolha **Idioma do Windows**, **Português (Brasil)** ou **English** e salve. A janela reabre no idioma escolhido com a mesma biblioteca. As páginas dos launchers na área Social seguem o idioma de cada serviço.

### Primeiros passos

1. Coloque o executável em uma pasta com permissão de escrita e abra o programa. Na primeira execução, escolha se ele deve iniciar junto com o Windows ou criar um atalho na Área de Trabalho. O mini tour tem três passos e pode ser pulado ou revisto em **Configurações → Biblioteca → Ver mini tour**.
2. Clique em **Atualizar jogos**. O resumo mostra os jogos instalados e disponíveis por launcher, além das capas e dos tempos atualizados nessa consulta. Para reabrir o resultado, use **Ver resumo** na barra inferior.
3. Use **Adicionar** para cadastrar um jogo avulso ou escolher um emulador e sua pasta de ROMs. O FitaHex verifica as subpastas. Depois, selecione o jogo e clique em **Jogar**.
4. O **X** recolhe o FitaHex para a bandeja do Windows. Clique no ícone para abrir a janela novamente. Para sair, clique com o botão direito no ícone e escolha **Encerrar FitaHex**.

### Launchers e importação

| Plataforma | Biblioteca e tempo jogado |
| --- | --- |
| Steam | Detecta instalações locais. Os jogos disponíveis e o tempo jogado dependem do cache local da Steam. |
| Epic Games | Detecta instalações pelos manifestos locais. O tempo anterior depende de registros compatíveis no cache do GOG Galaxy. |
| Xbox / Microsoft Store | Detecta jogos registrados no Windows. O tempo depende de registros compatíveis no cache do Playnite ou do GOG Galaxy. |
| GOG | Detecta jogos instalados e lê jogos disponíveis e tempo jogado do banco de dados local do GOG Galaxy quando ele existe. |
| Riot Games | Detecta instalações e lista jogos compatíveis disponíveis pelo Riot Client. O hub consegue acompanhar novas sessões quando identifica o processo do jogo. O total anterior da conta não é importado diretamente. |
| EA app e Battle.net | Os jogos disponíveis são importados quando constam no cache do GOG Galaxy. A sincronização direta da biblioteca da conta ainda não está disponível. |
| Ubisoft Connect | Disponível como filtro de plataforma e perfil na área Social. O tempo jogado pode ser lido de registros compatíveis no GOG Galaxy. |

A instalação é feita pelo launcher de origem. A disponibilidade de capas varia por jogo. WebP, AVIF e HEIF podem exigir os codecs de imagem do Windows. A presença no Discord depende do cliente desktop e do compartilhamento de atividade ativado. A área Social requer o Microsoft Edge WebView2 Runtime.

### Compilar o código

No Windows de 64 bits com .NET Framework 4.8, abra o PowerShell na pasta do repositório e execute:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

O script baixa a versão fixada do SDK WebView2 pelo NuGet e gera **dist/FitaHex.exe**. A opção **-Test** executa os testes em uma pasta isolada. Para usar o SDK local, passe **-WebView2SdkPath** com o caminho do pacote extraído.

### Dados locais

A biblioteca, as preferências, o cache de imagens e as sessões web ficam em **%LOCALAPPDATA%\FitaHex**, na conta atual do Windows. Abra a pasta em **Configurações → Backup → Abrir pasta de dados**. Você pode mover o executável sem mover seus dados.

Para migrar de uma versão antiga, encerre-a pela bandeja do Windows e abra a versão nova uma vez ao lado da pasta **NexusData** existente. Se o novo local ainda não tiver uma biblioteca, o FitaHex copia a biblioteca e os dados compatíveis, preservando a pasta original. Para levar sua biblioteca a outro PC, exporte um backup em **Configurações → Backup**.

Ao ativar a inicialização em **Configurações → Windows**, o atalho usa uma cópia em **%LOCALAPPDATA%\FitaHex\Startup**. Assim, ele continua funcionando se você mover ou renomear o executável baixado. Ao abrir uma versão atualizada, o FitaHex atualiza essa cópia.

Os nomes e logotipos dos launchers pertencem aos respectivos titulares.
