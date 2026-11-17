# Eagle Eyes â€” regras e arquitetura
Aplicativo Windows prÃ³prio em C#/.NET 11 estÃ¡vel, WPF e FFmpeg externo. NÃ£o usar preview. Interface em portuguÃªs, vetores prÃ³prios; sem Ã¡udio, imagem, webcam, editor, login ou nuvem.

## Componentes e contratos
- `src/EagleEyes.Core/Capture.cs`: CaptureArea(X,Y,Width,Height) em pixels fÃ­sicos globais. RecordingSession(ffmpegPath,ffprobePath): StartAsync(area,outputDirectory), PauseAsync(), ResumeAsync(), StopAsync(): Task<string>; State, RecordedDuration, RecoveryDirectory; IDisposable.
- Core serializa operaÃ§Ãµes e captura segmentos a 15 fps, cursor incluÃ­do, sem entrada de Ã¡udio. Pausa fecha segmento; retomada abre outro. FinalizaÃ§Ã£o concatena, verifica vÃ­deo/ausÃªncia de Ã¡udio e sÃ³ entÃ£o publica MP4 Ãºnico sem sobrescrever.
- `src/EagleEyes.App/`: WPF, monitores/DPI, seleÃ§Ã£o em um monitor, contagem regressiva, atalhos, docking e persistÃªncia. NÃ£o executar FFmpeg diretamente da UI; nÃ£o bloquear Dispatcher.
- `tests/` e `docs/QA.md`: harness e evidÃªncias. Distinguir verificaÃ§Ãµes automÃ¡ticas de validaÃ§Ã£o visual.

## Propriedade dos arquivos nesta implementaÃ§Ã£o
IntegraÃ§Ã£o (lÃ­der): raiz, README, ROADMAP, AGENTS, docs exceto QA, scripts. Captura: Core. Desktop: App. QA: tests e docs/QA.md. Coordenar contratos antes de alterÃ¡-los; nÃ£o editar arquivos de outro agente sem transferÃªncia explÃ­cita.

## Robustez e fluxo
Preservar temporÃ¡rios apÃ³s falha. Nunca apagar antes de verificar saÃ­da final. NÃ£o prometer recuperar quadros ausentes. Comandos invÃ¡lidos/duplicados devem ser rejeitados. Coordenadas negativas e DPI misto sÃ£o requisitos. Tentar excluir controles da captura; documentar o resultado e o comportamento alternativo.
Inspecionar arquivos e Git antes de mudanÃ§as. NÃ£o reescrever histÃ³rico, nÃ£o force push. Preparar commits locais; publicaÃ§Ã£o sÃ³ apÃ³s validaÃ§Ã£o/autorizaÃ§Ã£o. NÃ£o versionar binÃ¡rios FFmpeg, vÃ­deos, temporÃ¡rios ou saÃ­das de build.
Atualizar ROADMAP e documentaÃ§Ã£o a cada marco, registrando pendÃªncias reais. Testar captura/pausas antes de expandir UI. NÃ£o marcar requisito como validado sem evidÃªncia.
## Validação e entrega local
`powershell -File scripts/Build.ps1 -Live` compila a solução Release, roda o harness e publica em artifacts/eagle-eyes. `tests/UI-Flow.ps1` testa UI real com mouse/atalhos e restaura configurações; exige sessão Windows interativa e app fechado. `tests/EagleEyes.AffinityProof` testa pixels antes/depois de WDA e ocultação; --transparent usa janela WPF transparente. Flags internas --smoke-test e --proof-recording não persistem preferências, não são comandos de uso normal.

Temporários .eagle-eyes-session-* são mantidos inclusive após sucesso; limpeza manual somente após conferir a saída. Durante captura a duração é estimativa ativa; pausa/finalização ajustam pelo FFprobe. Desktop oculta por padrão, valida registro dos atalhos antes de fazê-lo e fecha o trecho antes de reaparecer. Não confundir sucesso WDA em janela de teste com garantia universal para a barra.
