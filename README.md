# Eagle Eyes

Gravador de tela Windows para evidÃªncias de testes. WPF, C#/.NET 11 estÃ¡vel e FFmpeg externo. Primeira versÃ£o em implementaÃ§Ã£o; consulte [ROADMAP.md](ROADMAP.md) e [docs/QA.md](docs/QA.md) para o estado validado.

## Compilar
Instale SDK .NET 11 estÃ¡vel e use Windows x64. Na raiz:

```powershell
dotnet build src/EagleEyes.App/EagleEyes.App.csproj -c Release
dotnet run --project tests/EagleEyes.Tests -c Release
```

FFmpeg e ffprobe devem estar em `components/ffmpeg-master-latest-win64-gpl/bin` sob a raiz do projeto, ou na localizaÃ§Ã£o configurada pelo aplicativo. Esta cÃ³pia foi fornecida localmente pelo usuÃ¡rio e nÃ£o Ã© versionada. Veja [dependÃªncias](docs/DEPENDENCIES.md).

## Escopo
RegiÃ£o dentro de um monitor ou monitor inteiro, cursor, 15 fps, contagem regressiva, pausas por segmentos, MP4 Ãºnico sem Ã¡udio. Barra flutuante escura com configuraÃ§Ãµes, gravaÃ§Ã£o e recolhimento. Sem captura de imagem, webcam, editor, login ou nuvem.

A validaÃ§Ã£o visual em escalas 100/125/150%, reproduÃ§Ã£o no player e legibilidade nÃ£o Ã© substituÃ­da pelo build. TemporÃ¡rios recuperÃ¡veis sÃ£o preservados em erros; nÃ£o existe promessa de recuperar quadros ausentes.

Arquitetura e regras em [AGENTS.md](AGENTS.md); decisÃµes em [docs/PLAN.md](docs/PLAN.md).
## Uso
Execute `Iniciar.cmd` apÃ³s compilar. A engrenagem escolhe pasta, monitor, regiÃ£o/monitor inteiro, FFmpeg e fixaÃ§Ã£o. O botÃ£o de vÃ­deo abre a seleÃ§Ã£o por arraste (Esc cancela); confirme o aviso e aguarde3 segundos.

Por padrÃ£o a barra some enquanto grava: `Ctrl+Shift+P` pausa e a traz de volta; o mesmo atalho retoma. `Ctrl+Shift+F` finaliza. A barra somente reaparece quando o Ãºltimo trecho jÃ¡ foi encerrado, evitando capturar seus controles. Atalhos em conflito impedem esse modo atÃ© liberar as teclas/reiniciar. Se optar por deixar a barra visÃ­vel, a exclusÃ£o por API depende do ambiente e precisa ser conferida no vÃ­deo.

Arraste pela pequena alÃ§a de linhas para mover; ao soltar, a barra encaixa na borda mais prÃ³xima. A seta recolhe/expande. A fixaÃ§Ã£o impede recolhimento automÃ¡tico, mantendo o arraste disponÃ­vel.

A saÃ­da usa nomes EagleEyes-data-hora com sufixos para nÃ£o sobrescrever. O Explorer seleciona o MP4 ao concluir. As pastas `.eagle-eyes-session-*` na pasta de saÃ­da guardam trechos e metadados, inclusive apÃ³s sucesso. Remova-as manualmente apenas depois de conferir o MP4; em erro preserve-as para diagnÃ³stico. O relÃ³gio durante o trecho Ã© estimado pelo tempo ativo e Ã© ajustado Ã  duraÃ§Ã£o efetiva do arquivo ao pausar/finalizar.

Para build/testes/publicaÃ§Ã£o local: `powershell -File scripts/Build.ps1`; acrescente `-Live` para capturar uma regiÃ£o real do desktop durante os testes. O executÃ¡vel gerado fica em `artifacts/eagle-eyes/EagleEyes.App.exe` e requer Windows Desktop Runtime.NET 11. Components permanece externo ao pacote.
