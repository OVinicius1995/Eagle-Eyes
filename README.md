# Eagle Eyes

Gravador de tela Windows para evidências de testes de software. Primeira versão: **1.0.0**, em C#/.NET 11, WPF e FFmpeg externo.

## Instalar e abrir
Baixe o ZIP Windows x64 na [página de releases](https://github.com/OVinicius1995/Eagle-Eyes/releases), extraia a pasta inteira e execute `EagleEyes.App.exe`.

Dependências:
- Windows x64 com .NET Desktop Runtime 11. O pacote não inclui o runtime: https://dotnet.microsoft.com/download/dotnet/11.0
- `ffmpeg.exe` e `ffprobe.exe`, com gdigrab e libx264. Coloque-os na subpasta `ffmpeg` ao lado do executável, ou selecione sua pasta nas configurações. Preserve os avisos e licenças da distribuição obtida. Os binários FFmpeg não acompanham este release.

Ao executar pelo repositório, o app também encontra `components/ffmpeg-master-latest-win64-gpl/bin`. Veja [dependências e proveniência](docs/DEPENDENCIES.md).

## Gravar
A engrenagem permite escolher pasta de saída, monitor, região ou monitor inteiro, FFmpeg e fixação. Clique no botão de vídeo, arraste uma região dentro de um único monitor (Esc cancela), confirme o aviso e aguarde a contagem de 3 segundos.

Por padrão a barra some durante a gravação. `Ctrl+Shift+P` pausa e a traz de volta; o mesmo atalho retoma. `Ctrl+Shift+F` finaliza. A barra só reaparece após encerrar o trecho para evitar capturar seus controles. Atalhos em conflito impedem esse modo até liberar as teclas e reiniciar. Se desativar a ocultação, confira no vídeo se a exclusão por API funcionou no seu ambiente.

Arraste pela alça de linhas para mover e encaixar na borda mais próxima. A seta recolhe/expande. “Manter expandida” impede recolhimento automático, mas permite arrastar.

O vídeo inclui cursor, usa 15 fps e nunca captura áudio. Pausas viram trechos separados, reunidos em um único MP4 sem o tempo pausado. Nomes únicos evitam sobrescrita; o Explorer seleciona a saída ao concluir. Sem editor, captura de imagem, webcam, login ou nuvem.

As pastas `.eagle-eyes-session-*` na pasta de saída guardam trechos e metadados, inclusive após sucesso. Remova-as manualmente somente após conferir o MP4. Em erro, preserve-as para diagnóstico; quadros ausentes não são recuperáveis. O relógio durante o trecho é uma estimativa ativa, ajustada pela duração efetiva na pausa/finalização.

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

O primeiro marco foi validado automaticamente com duas pausas, sem áudio e decodificação completa. O usuário informou ter concluído seus testes e autorizou o release 1. Cenários específicos de múltiplos monitores e escalas continuam sem evidência individual documentada. Consulte [QA](docs/QA.md), [ROADMAP](ROADMAP.md), [AGENTS](AGENTS.md) e [notas da versão](docs/releases/v1.0.0.md).
