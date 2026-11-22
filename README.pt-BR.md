# FitaHex

[English](README.md)

O FitaHex é um hub portátil de jogos para Windows. Ele reúne jogos de launchers, executáveis avulsos e ROMs em uma biblioteca local. Você pode favoritar jogos, organizá-los em coleções, marcar os que zerou e consultar o tempo jogado e o histórico.

## Primeiros passos

1. Coloque o executável em uma pasta com permissão de escrita e abra o programa. Na primeira execução, escolha se ele deve iniciar junto com o Windows ou criar um atalho na Área de Trabalho. O mini tour tem três passos e pode ser pulado ou revisto em **Configurações → Biblioteca → Ver mini tour**.
2. Clique em **Atualizar jogos**. O resumo mostra os jogos instalados e disponíveis por launcher, além das capas e dos tempos atualizados nessa consulta. Para reabrir o resultado, use **Ver resumo** na barra inferior.
3. Use **Adicionar** para cadastrar um jogo avulso ou escolher um emulador e sua pasta de ROMs. O FitaHex verifica as subpastas. Depois, selecione o jogo e clique em **Jogar**.
4. O **X** recolhe o FitaHex para a bandeja do Windows. Clique no ícone para abrir a janela novamente. Para sair, clique com o botão direito no ícone e escolha **Encerrar FitaHex**.

## Launchers e importação

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

## Compilar o código

No Windows de 64 bits com .NET Framework 4.8, abra o PowerShell na pasta do repositório e execute:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test

O script baixa a versão fixada do SDK WebView2 pelo NuGet e gera **dist/FitaHex.exe**. A opção **-Test** executa os testes em uma pasta isolada. Para usar o SDK local, passe **-WebView2SdkPath** com o caminho do pacote extraído.

## Dados locais

A biblioteca, as preferências, o cache de imagens e as sessões web ficam em **%LOCALAPPDATA%\FitaHex**, na conta atual do Windows. Abra a pasta em **Configurações → Backup → Abrir pasta de dados**. Você pode mover o executável sem mover seus dados.

Para migrar de uma versão antiga, encerre-a pela bandeja do Windows e abra a versão nova uma vez ao lado da pasta **NexusData** existente. Se o novo local ainda não tiver uma biblioteca, o FitaHex copia a biblioteca e os dados compatíveis, preservando a pasta original. Para levar sua biblioteca a outro PC, exporte um backup em **Configurações → Backup**.

Ao ativar a inicialização em **Configurações → Windows**, o atalho usa uma cópia em **%LOCALAPPDATA%\FitaHex\Startup**. Assim, ele continua funcionando se você mover ou renomear o executável baixado. Ao abrir uma versão atualizada, o FitaHex atualiza essa cópia.

Os nomes e logotipos dos launchers pertencem aos respectivos titulares.
