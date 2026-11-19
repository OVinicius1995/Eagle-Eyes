# Eagle Eyes

Gravador de tela Windows para evidências de testes de software. Release público estável: **1.0.0**. Esta branch contém as melhorias **UX 1.1 implementadas e validadas localmente**, em C#/.NET 11, WPF e FFmpeg externo; ainda sem nova publicação.

## Instalar e abrir
Baixe o ZIP Windows x64 na [página de releases](https://github.com/OVinicius1995/Eagle-Eyes/releases), extraia a pasta inteira e execute `EagleEyes.App.exe`.

Dependências:
- Windows x64 com .NET Desktop Runtime 11. O pacote não inclui o runtime: https://dotnet.microsoft.com/download/dotnet/11.0
- Para a versão portátil, `ffmpeg.exe` e `ffprobe.exe`, com gdigrab e libx264. Coloque-os juntos na subpasta `ffmpeg` ao lado do executável. Preserve os avisos e licenças da distribuição obtida. Os binários FFmpeg não acompanham este release.

Ao executar pelo repositório, o app também encontra `components/ffmpeg-master-latest-win64-gpl/bin`. Veja [dependências e proveniência](docs/DEPENDENCIES.md).

## Gravar nesta versão local
A engrenagem escolhe pasta de saída, monitor e modo de gravação. Os componentes de vídeo são localizados automaticamente nas pastas padrão. O botão de vídeo abre a seleção: arraste uma região ou pressione Espaço para usar somente o monitor sob o ponteiro. Esc cancela e retorna à barra principal. A seleção não inicia captura.

A barra de preparação permanece visível junto da área destacada. Use Gravar, Selecionar novamente ou Cancelar. Ao iniciar, a contagem central 3, 2, 1 aparece no monitor escolhido; Esc cancela a contagem e volta à área preparada. O painel e o destaque são fechados antes de iniciar FFmpeg.

Durante a gravação, uma barra horizontal permanece visível com estado, tempo efetivamente gravado, pausar/retomar, parar e salvar, e descartar. Os atalhos Ctrl+Shift+P e Ctrl+Shift+F continuam disponíveis quando registrados. A barra tenta se excluir por WDA; o resultado precisa ser validado por ambiente. Prefira posicioná-la fora da região quando houver espaço.

Parar e salvar funciona gravando ou pausado, sem confirmação adicional. Após validar o MP4, o app informa sucesso e oferece Abrir pasta. Descartar abre confirmação após pausar; cancelar restaura o estado anterior. Confirmar exclui somente arquivos conhecidos da sessão atual, sem gerar MP4 final. Arquivos desconhecidos ou impossíveis de excluir são preservados e informados.

Arraste por qualquer área não interativa das barras, inclusive o cronômetro. Botões/campos não iniciam arraste e existe um limiar de movimento. A barra principal mantém encaixe nas quatro bordas e recolhimento; a barra da sessão permanece horizontal e acessível.

O vídeo usa H.264 a 15 fps, cursor e nenhuma faixa de áudio. As pausas viram trechos separados, reunidos em MP4 único sem o intervalo pausado. Depois de validar e salvar o MP4, o aplicativo limpa os temporários daquela gravação. Se a finalização falhar, a pasta é preservada para recuperação. Quadros ausentes não são recuperáveis.

Mensagens principais são breves; Ver detalhes e logs em `%LOCALAPPDATA%\EagleEyes\logs` guardam informações técnicas. Consulte o checkpoint UX 1.1 no ROADMAP antes de retomar uma sessão de desenvolvimento.
## Compilar e verificar
Com SDK .NET 11 estável instalado, na raiz:

```powershell
powershell -File scripts/Build.ps1
# Captura região real do desktop nos testes:
powershell -File scripts/Build.ps1 -Live
# Prepara ZIP e SHA256:
powershell -File scripts/Prepare-Release.ps1
# Publicar após commit e autenticação GitHub:
powershell -File scripts/Publish-Release.ps1
```

`Iniciar.cmd` abre a publicação local em `artifacts/eagle-eyes` ou compila/executa o projeto. O `global.json` fixa SDK 11.0.100 sem preview.

O primeiro marco foi validado automaticamente com duas pausas, sem áudio e decodificação completa. A UX 1.1 tem build e provas funcionais; o checkpoint no ROADMAP descreve a validação atual. Não publicar a UX 1.1 sem nova autorização. O usuário informou ter concluído seus testes e autorizou o release 1. Cenários específicos de múltiplos monitores e escalas continuam sem evidência individual documentada. Consulte [QA](docs/QA.md), [ROADMAP](ROADMAP.md), [AGENTS](AGENTS.md) e [notas da versão](docs/releases/v1.0.0.md).

## Marca e configurações atuais
A engrenagem e o X são vetores compactos preenchidos; os demais controles usam a mesma família visual. Configurações tem Salvar/Cancelar e não expõe caminho de FFmpeg. Na distribuição portátil, o app exige ffmpeg.exe e ffprobe.exe juntos em `ffmpeg` ao lado do aplicativo; no repositório também aceita o padrão `components/ffmpeg-master-latest-win64-gpl/bin` nos ancestrais. PATH, diretório atual e localização antiga salva são ignorados. Se faltar o par, ao tentar gravar aparece um alerta com os arquivos necessários e onde colocá-los.

O símbolo oficial foi preservado, com remoção local do branco: [PNG transparente](Assets/eagle-eyes.png) e [ICO multirresolução](Assets/eagle-eyes.ico). O ícone está no EXE, nas janelas e na bandeja. Duplo clique na bandeja mostra os controles; o menu oferece Mostrar controles e Sair, com descarte confirmado durante gravação. O Windows pode colocar o ícone no painel de ícones ocultos.

Proveniência e conversão em [docs/BRANDING.md](docs/BRANDING.md). Para repetir apenas os testes desta etapa:

```powershell
dotnet run --project tests/EagleEyes.BrandTests -c Release -- --exe artifacts/eagle-eyes/EagleEyes.App.exe
powershell -File tests/UI-Branding.ps1 -RequireBrandingReport
powershell -File tests/UI-MissingComponents.ps1
```

Os testes de UI exigem aplicativo fechado e restauram preferências quando as alteram. Nenhum instalador/gerador de atalhos faz parte do projeto nesta versão.

## Instalador e aviso de versões

Veja [Instalação e atualizações](docs/INSTALLATION.md). O instalador local inclui o runtime .NET, cria atalhos e preserva dados. A partir da versão 1.2.1, o assistente baixa e instala o FFmpeg automaticamente. A conexão é necessária durante a instalação. Na 1.3.0, o botão Atualizar baixa e verifica o instalador e fecha o aplicativo com segurança para iniciar a instalação. O aviso aguarda o término da gravação. Esta versão ainda não foi publicada.
