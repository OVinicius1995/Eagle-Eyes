# Eagle Eyes - regras e arquitetura

Aplicativo Windows em C#/.NET 11 estável, WPF e FFmpeg externo. Não usar SDK preview. Interface em português com vetores próprios. Sem áudio, captura de imagem, webcam, editor, login ou nuvem.

## Componentes e contratos
- `src/EagleEyes.Core/Capture.cs`: `CaptureArea(X,Y,Width,Height)` em pixels físicos globais. `RecordingSession(ffmpegPath,ffprobePath)` oferece `StartAsync(area,outputDirectory)`, `PauseAsync()`, `ResumeAsync()` e `StopAsync(): Task<string>`, além de `State`, `RecordedDuration`, `RecoveryDirectory` e `IDisposable`.
- Core serializa comandos, captura a 15 fps com cursor e sem áudio. Pausar fecha o trecho; retomar cria outro. Finalização concatena, valida com FFprobe e publica nome único sem sobrescrita.
- `src/EagleEyes.App/`: WPF, monitores/DPI, seleção dentro de um monitor, contagem regressiva, atalhos, encaixe nas bordas e persistência. A UI não executa FFmpeg diretamente nem bloqueia o Dispatcher.
- `tests/` e `docs/QA.md`: harness e evidências. Separar testes automáticos de validação visual e de testes informados pelo usuário.

## Responsabilidades
Antes de delegar, definir contratos e propriedade de arquivos. Integração: raiz, documentação e scripts. Captura: Core. Desktop: App. QA: tests e docs/QA.md. Coordenar alterações nos contratos; transferir explicitamente a propriedade antes de editar arquivos de outro agente. A implementação inicial foi integrada e encerrada; manutenção e publicação estão sob responsabilidade do líder.

## Robustez
Preservar temporários após falhas e nunca apagá-los antes de validar a saída. Nesta versão permanecem inclusive após sucesso: limpeza manual após conferir o MP4. Não prometer recuperar quadros ausentes. Rejeitar comandos inválidos, simultâneos e início duplicado. Considerar DPI misto e coordenadas negativas; registrar os cenários realmente testados.
A barra é ocultada por padrão durante captura. Confirmar registro dos atalhos antes de ocultar e fechar o trecho antes de reaparecer. O retorno da API WDA não garante exclusão universal. Durante captura o relógio é uma estimativa ativa, ajustada pela duração dos arquivos na pausa/finalização.

## Verificação e Git
Inspecionar arquivos e estado local/remoto antes de alterações. Não reescrever histórico nem fazer force push. Não versionar FFmpeg, vídeos, temporários ou builds. Atualizar ROADMAP e documentação em cada marco, sem marcar cenários específicos como testados apenas por aprovação geral.
`powershell -File scripts/Build.ps1 -Live` compila Release, testa e publica localmente. `tests/UI-Flow.ps1` exige desktop interativo e app fechado; move o mouse, envia atalhos e restaura configurações. `tests/EagleEyes.AffinityProof` testa pixels com WDA; `--transparent` usa janela transparente. Flags internas `--smoke-test` e `--proof-recording` não persistem preferências.

## Releases
O usuário autorizou publicar o release 1 após informar que realizou os testes. Usar tag `v1.0.0`, preservar commits existentes e publicar sem force push. O pacote é Windows x64 dependente do .NET Desktop Runtime 11, com FFmpeg externo. Não incluir a distribuição FFmpeg fornecida localmente sem confirmar sua proveniência e obrigações de distribuição. Documentar dependências e gerar SHA256 do ZIP. `scripts/Prepare-Release.ps1` prepara o pacote reproduzível.

## Etapa UX 1.1 em andamento
Branch local: feature/ux-1.1. Preservar v1.0.0 e não publicar novo release sem pedido. A regra anterior de esconder a barra durante captura é substituída neste escopo: barra horizontal permanece visível, tentando WDA; testar exclusão real com gdigrab, registrar limitações sem declarar sucesso só pela API.

Contratos definidos antes de delegar:
- Core: adicionar `Task<DiscardResult> DiscardAsync()` e `DiscardResult(bool Complete, IReadOnlyList<string> RemainingPaths)`. Encerrar processo antes de excluir somente arquivos conhecidos da sessão corrente; não gerar saída final no descarte nem tocar vídeos anteriores/sessões vizinhas. Preservar e relatar falhas de exclusão. Operação serializada.
- `RegionSelection.SelectAsync()` permanece `Task<CaptureArea?>`; Espaço seleciona o monitor sob o ponteiro. Consumir KeyDown/KeyUp e concluir no KeyUp para evitar acionamento da próxima janela; Esc cancela. Nenhuma captura nessa etapa.
- `PreparationWindow(CaptureArea area) : Window`, `Task<PreparationAction> NextActionAsync()`, `void HideAll()`, Close fecha também o destaque; enum PreparationAction { Start, Reselect, Cancel }. NextActionAsync mostra a barra/destaque e habilita controles; após resposta bloqueia ações duplicadas. WDA nas janelas; preferir barra fora da região e limitar à área útil.
- `CountdownWindow.RunAsync(Native.Monitor monitor): Task<bool>`; true depois de 3,2,1 e fechamento completo; false com Esc. Sem iniciar Core dentro dessa janela. Caller pode voltar à preparação. Anel animado verde e painel central.
- `WindowDragBehavior.Attach(Window window, Action? afterDrag = null)` inicia arraste somente em área não interativa e após limiar de movimento, sem impedir botões/campos/menus. `Native.ClampToWorkArea(Window)` mantém acesso após mover; `Native.TryExcludeFromCapture(Window): bool` tenta WDA.

Arquivos exclusivos nesta etapa:
- Integração/líder: MainWindow.cs, SettingsWindow.cs, Settings.cs, App.cs, mensagens/logs em novos arquivos, raiz/docs exceto QA e scripts.
- Agente Desktop: RegionSelection.cs, Native.cs, novos PreparationWindow.cs, CountdownWindow.cs e WindowDragBehavior.cs.
- Agente Core: src/EagleEyes.Core/** e tests/EagleEyes.Tests/Program.cs.
- Agente QA: tests/UI-Flow.ps1, novos testes/UI/helpers sob tests exceto EagleEyes.Tests/Program.cs, docs/QA.md. Não alterar fontes de outros responsáveis; reportar correções ao dono.

Checkpoint: atualizar ROADMAP em cada etapa com concluído, testes reais, pendências e próximo passo exato. Commits locais frequentes sem force push. Saldo de uso não é visível ao agente; ao pedido de pausa, salvar o estado e parar.