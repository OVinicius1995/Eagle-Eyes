# Instalação e atualizações

## Gerar o instalador

Pré-requisitos de desenvolvimento: SDK estável fixado em global.json e Inno Setup (compilador ISCC.exe). O usuário final não precisa instalar o runtime .NET: o pacote win-x64 é self-contained.

```powershell
powershell -File scripts/Prepare-Installer.ps1 -CompilerPath 'C:\caminho\Inno Setup\ISCC.exe'
```

Saída: `artifacts/installer/1.2.1/EagleEyes-1.2.1-win-x64-Setup.exe` e `.sha256`. Versão vem do projeto App. Cada build usa pasta de publicação nova; os arquivos são selecionados dessa pasta, nunca da raiz. Guia privado, vídeos, PDB e FFmpeg são rejeitados. Runtime e seus arquivos LICENSE/ThirdPartyNotices são preservados.

Instalação por usuário, sem exigir administrador, em `%LOCALAPPDATA%\Programs\Eagle Eyes`. Atalho no menu Iniciar, atalho na área de trabalho opcional, ícone oficial e entrada de desinstalação. AppId fixo identifica a mesma instalação nas próximas versões. Não escolher a pasta de uma cópia portátil existente: instalar em pasta própria.

O assistente baixa o FFmpeg 9.0.2 diretamente da release do distribuidor, confere o SHA256 e instala o pacote em engine. Conecte-se à internet durante a instalação e as atualizações. Ao terminar, abra o Eagle Eyes e escolha a área de gravação. A gravação funciona offline.

## Atualizar

Configurações > Atualizações permite consulta manual e habilitar/desabilitar aviso automático. O aplicativo consulta uma vez por execução, após 15 segundos, se a opção estava habilitada ao abrir. Mudanças dessa opção afetam a próxima execução; uma resposta pendente não é mostrada se a opção tiver sido desabilitada.

A API HTTPS `/repos/OVinicius1995/Eagle-Eyes/releases/latest` fornece somente a indicação de versão. Prévias/rascunhos são rejeitados. Versões são comparadas numericamente, não por texto. Timeout total de 15 segundos, resposta limitada a 1 MiB, sem seguir redirecionamentos. Falhas automáticas ficam nos logs; a consulta manual explica como tentar novamente. Sem login, token ou envio de dados das gravações.

O aviso aguarda aplicativo ocioso/visível, sem operação ativa ou janela modal. O usuário pode ignorar ou abrir a página oficial, baixar o instalador, conferir SHA256 publicado, salvar o trabalho e SAIR do Eagle Eyes antes de instalar. Não existe download/execução silenciosa nem verificação automática de assinatura do pacote nesta etapa. O destino é construído localmente para o repositório oficial; html_url fornecida pelo servidor não é executada.

Instalador bloqueia instalação/desinstalação se o mutex da aplicação estiver ativo, inclusive em modo silencioso; CloseApplications/RestartApplications estão desativados. Não encerrar processos à força. O instalador substitui arquivos do programa; arquivos externos de FFmpeg, preferências em `%LOCALAPPDATA%\EagleEyes`, logs e vídeos não pertencem à lista de exclusão. A desinstalação preserva os dados do usuário e arquivos adicionados manualmente.

## Publicar uma atualização futuramente

1. Alterar Version/AssemblyVersion/FileVersion, atualizar ROADMAP/notas e executar build/testes.
2. Gerar instalador + SHA256 e validar instalação/atualização/desinstalação isoladas.
3. Somente com autorização para publicar, criar release estável no repositório oficial com tag `vX.Y.Z`, anexar Setup.exe, SHA256 e instruções de FFmpeg. O serviço existente de publicação de ZIP não anexa o instalador automaticamente; anexá-lo explicitamente e conferir o download antes de anunciar.
4. As versões com esse verificador passam a indicar a release mais nova. A versão pública 1.0.0 não contém o verificador: sua primeira migração é manual.

Não há certificado de assinatura do Eagle Eyes configurado: SmartScreen pode apresentar aviso. SHA256 detecta corrupção comparando com um valor confiável; não equivale a assinatura de autoria. Não declarar atualização totalmente automática ou transação com rollback implementadas. Futuro: download validado, assinatura de código e aplicação supervisionada integrada, após prova real com duas versões publicadas.

Referências: [Inno Setup](https://jrsoftware.org/isinfo.php), [execução sem administrador](https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm), [proteção por mutex](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm), [releases/latest](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).
