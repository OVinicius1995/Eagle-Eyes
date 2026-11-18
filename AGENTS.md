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
Na release 1.0 a barra era ocultada durante captura. Na UX 1.1 a barra permanece visível e tenta WDA; tooltips têm HWND próprio e usam CaptureSafeToolTip para excluir o popup antes de pintar. Testar pixels/MP4, pois retorno da API não garante exclusão universal. O relógio usa progresso codificado FFmpeg e duração FFprobe dos trechos fechados.

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
## Histórico: checkpoint f63e794 interrompido por limite de uso
A UX1.1 está integrada localmente e tem regressão aprovada, mas ainda há tooltip WPF capturado fora da janela principal excluída por WDA. MainWindow.IconButton, duração e Window.ToolTip usam strings que geram popups separados. Corrigir e provar exclusão do popup antes de declarar entrega concluída. O próximo passo detalhado, evidências e o motivo da interrupção estão na seção final de ROADMAP.md. Usuário pediu checkpoint e interrupção quando o uso estiver próximo do limite; o agente QA informou limite explicitamente. Não presumir que o reset ocorreu apenas porque houve nova mensagem: conferir disponibilidade e continuar sequencialmente se necessário, preservando as responsabilidades já documentadas.

## Retomada após restauração do limite
Usuário confirmou restauração e autorizou continuar. Líder implementou CaptureSafeToolTip.cs e integração de tooltips explícitos na MainWindow. QA mantém propriedade dos roteiros e docs/QA.md, executando provas focadas de popup real. Checkpoint anterior f63e794 preservado; consultar seção atual de ROADMAP antes de retomar. Nenhum novo release autorizado.

## Estado atual da UX 1.1
Escopo implementado e validado no ambiente disponível. A correção dos tooltips (f7738fe) passou em controle positivo e exclusão padrão de três popups reais, gdigrab e MP4. Core 68 asserções, fluxo funcional, arraste e cliques repetidos aprovados; evidências e pendências de hardware estão no ROADMAP/docs/QA. Executável local artifacts/eagle-eyes/EagleEyes.App.exe versão 1.1.0.0. Nenhum novo release autorizado. Não retomar o bug de tooltip como pendente: o histórico acima descreve o checkpoint antigo; consultar sempre o estado atual do ROADMAP.

## Conclusão da prova dos tooltips
Prova final robusta: controle positivo d0046dee2ea842528ee381066dbc3919 com três textos legíveis; WDA 32598b59835042ada901b83052bff127 com três popups ausentes do gdigrab/MP4. Ensaios antigos de MP4 em 8/13 s foram retirados por erro de sincronização do teste, substituídos por tempo da sessão/hover estabilizado e checagem de pixels brancos no controle. RepeatedActions b8e320c380b84543992cc9ae1cd7dab8 aprovou cliques duplicados em Gravar e salvar. Não há implementação pendente no escopo; ver ROADMAP para validações de ambiente e próximos pedidos.

## Iconografia, configurações e marca oficial - etapa atual
Preservar o fluxo UX1.1 e seus testes aprovados. Não publicar release/push. Skill frontend-design não encontrada; revisão será feita com desenho vetorial WPF e ferramentas locais.
- Fonte aprovada encontrada na raiz como `Emblema de olhos de águia azul-ardósia.png`; preservar bytes. Gerar cópia canônica eagle-eyes-original.png e Assets/eagle-eyes.png + ICO verdadeiro 16/20/24/32/48/64/128/256. Sem redesenho/recoloração e sem conversores externos. Transparência inclui interior dos olhos, antialias sem halo; símbolo centralizado a ~85% da largura quadrada.
- Líder: Assets/**, scripts de transformação local, Branding.cs, TrayIcon.cs, App.cs, csproj, documentação raiz/README/ROADMAP e integração de recursos. Não alterar Core.
- Agente UI/UX: MainWindow.cs (somente iconografia, mensagens de dependência e métodos de integração abaixo), SettingsWindow.cs, Settings.cs, novos FunctionalIcons.cs e FFmpegLocation.cs. Não mudar os fluxos de captura/pausa/contagem/descarte validados.
- Agente QA: novos testes/scripts de marca/configurações e docs/QA.md. Não modificar UI/Core; reportar ao dono. Roteiros UX existentes não precisam ser reexecutados integralmente sem mudança relevante.
Contratos: FunctionalIcons.Create(FunctionalIcon kind, double size) retorna FrameworkElement vetorial, cor #B7B9CD. Engrenagem preenchida de oito dentes curtos arredondados e furo transparente, visual 14x14 em alvo maior; X e demais controles consistentes, sem emoji/imagem raster funcional. Preservar CaptureSafeToolTip.
FFmpegLocation.Resolve(string name, string? applicationDirectory=null): caminho padrão app/ffmpeg com ambos executáveis, ou padrão de desenvolvimento components/ffmpeg-master-latest-win64-gpl/bin nos ancestrais. Não ler PATH, CWD, configuração antiga ou campo editável. Override applicationDirectory é apenas seam de teste local; não expor na UI. Resolver ambos da mesma pasta; alertar ausência de componentes com próximo passo claro.
MainWindow oferece internal void ShowControls() (não abrir principal sobre seleção/contagem em andamento) e internal Task ExitFromTrayAsync() (durante sessão usar descarte confirmado/restaurar ao cancelar antes de sair; nunca encerrar silenciosamente). Branding.Apply(Window) será fornecido pelo líder, e aplicado globalmente às janelas. Ícone da bandeja terá Mostrar controles/Sair funcionais e descarte protegido.
Salvar checkpoints no ROADMAP e commits locais. Próximos testes: pixels/ICO verdadeiro/alpha/tamanhos, comparativos claro/escuro, recursos EXE/janelas/bandeja, configurações sem campo FFmpeg e resolução/alerta, smoke UI/build.

## Retomada da marca após pausa
Líder assume os arquivos de QA para conclusão sequencial, pois o agente ficou indisponível. Código integrado compila; testes de marca 88PASS já salvos. Não regenerar/recolorir assets aprovados. Ver seção final de ROADMAP para os poucos passos restantes. Marca aplicada globalmente por Branding.Initialize/Apply e TrayIcon nativo; diagnóstico --ux-test --branding-report <json> registra Shell e recurso. Nenhuma publicação nova autorizada.

## Estado final da marca e configurações
Etapa concluída, testes e executável local atualizados. PNG/ICO oficiais preservados, ícones funcionais separados, FFmpeg somente pastas padrão e alerta de ausência. Não retomar como pendente a correção do TextBox: foi corrigida e UIA repetido. Próximo pedido deve partir do checkpoint final em ROADMAP; não publicar sem autorização. Core não alterado. O líder assumiu o QA indisponível e todos os arquivos podem ser mantidos pelo líder na próxima etapa.

## Tema compartilhado e guia privado
UserMessages e SettingsWindow usam VisualIdentity/Design/EagleEyes.Theme.xaml para impedir divergência de botões. A guia pessoal docs/IDENTIDADE-VISUAL.md é PRIVADA, ignorada e nunca rastreada. Não usar git add -f, copiar seu conteúdo para arquivos públicos, anexar a release ou incluí-la no pacote sem autorização explícita. O teste de design não deve falhar somente porque esse arquivo privado não existe em outra cópia. Assets de marca anteriores continuam rastreados; não publicar nada sem novo pedido e esclarecimento de eventual alcance adicional de privacidade. UI componente21checks e build Release passaram; ver último checkpoint do ROADMAP.
