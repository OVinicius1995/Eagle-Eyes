# Instalação e atualizações

## Gerar o instalador

Pré-requisitos de desenvolvimento: SDK estável fixado em global.json e Inno Setup (compilador ISCC.exe). O usuário final não precisa instalar o runtime .NET: o pacote win-x64 é self-contained.

```powershell
powershell -File scripts/Prepare-Installer.ps1 -CompilerPath 'C:\caminho\Inno Setup\ISCC.exe'
```

Saída: `artifacts/installer/1.3.0/EagleEyes-1.3.0-win-x64-Setup.exe` e `.sha256`. Versão vem do projeto App. Cada build usa pasta de publicação nova; os arquivos são selecionados dessa pasta, nunca da raiz. Guia privado, vídeos, PDB e FFmpeg são rejeitados. Runtime e seus arquivos LICENSE/ThirdPartyNotices são preservados.

Instalação por usuário, sem exigir administrador, em `%LOCALAPPDATA%\Programs\Eagle Eyes`. Atalho no menu Iniciar, atalho na área de trabalho opcional, ícone oficial e entrada de desinstalação. AppId fixo identifica a mesma instalação nas próximas versões. Não escolher a pasta de uma cópia portátil existente: instalar em pasta própria.

O instalador 1.4.1 verifica localmente o SHA-256 de ffmpeg.exe e ffprobe.exe do motor gerenciado na pasta engine. Se ambos correspondem ao pacote esperado, reutiliza o motor, inclusive em atualizações da versão 1.4.0. Quando falta um componente, ou a verificação falha, baixa o pacote FFmpeg 9.0.2 do distribuidor e confere o SHA-256 do ZIP antes de extraí-lo. Esse download requer internet. Ao terminar, abra o Eagle Eyes e escolha a área de gravação. A gravação funciona offline.

## Atualizar

Configurações > Atualizações permite consulta manual e habilitar/desabilitar aviso automático. O aplicativo consulta uma vez por execução, após 15 segundos, se a opção estava habilitada ao abrir. Mudanças dessa opção afetam a próxima execução; uma resposta pendente não é mostrada se a opção tiver sido desabilitada.

A partir da 1.3.0, clique em **Atualizar** no aviso. O Eagle Eyes baixa o Setup x64 da release estável do repositório oficial, confere o tamanho e o SHA-256 informado pelo GitHub e mantém o arquivo protegido contra escrita até iniciar o instalador. É possível cancelar durante o download. Se a verificação falhar, o aplicativo continua aberto e permite tentar novamente.

O aviso automático espera a seleção, preparação, contagem, gravação, pausa e finalização terminarem. Durante o download, a barra bloqueia o início de outra sessão. Depois da verificação, o aplicativo fecha normalmente, libera a bandeja e o mutex e abre o instalador com progresso visível. Ao terminar, abra o Eagle Eyes pelo menu Iniciar. No formato portátil, esse fluxo abre o instalador por usuário.

A versão 1.2.2 já distribuída ainda abre a página da release. A primeira instalação da 1.3.0 requer o instalador. As atualizações seguintes podem partir do botão integrado.

O instalador bloqueia instalação/desinstalação se o mutex da aplicação estiver ativo, inclusive em modo silencioso; CloseApplications/RestartApplications estão desativados. Não encerrar processos à força. O instalador substitui arquivos do programa; arquivos externos de FFmpeg, preferências em `%LOCALAPPDATA%\EagleEyes`, logs e vídeos não pertencem à lista de exclusão. A desinstalação preserva os dados do usuário e arquivos adicionados manualmente.

## Publicar uma atualização futuramente

1. Alterar Version/AssemblyVersion/FileVersion, atualizar ROADMAP/notas e executar build/testes.
2. Gerar instalador + SHA256 e validar instalação/atualização/desinstalação isoladas.
3. Somente com autorização para publicar, criar release estável no repositório oficial com tag `vX.Y.Z`, anexar Setup.exe, SHA256 e instruções de FFmpeg. O serviço existente de publicação de ZIP não anexa o instalador automaticamente; anexá-lo explicitamente e conferir o download antes de anunciar.
4. As versões com esse verificador passam a indicar a release mais nova. A versão pública 1.0.0 não contém o verificador: sua primeira migração é manual.

Não há certificado de assinatura do Eagle Eyes configurado: SmartScreen pode apresentar aviso. SHA256 detecta corrupção comparando com um valor confiável; não equivale a assinatura de autoria. O instalador exibe erros de instalação. A retomada de uma instalação com erro exige nova tentativa.

Referências: [Inno Setup](https://jrsoftware.org/isinfo.php), [execução sem administrador](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm), [proteção por mutex](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm), [releases/latest](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
