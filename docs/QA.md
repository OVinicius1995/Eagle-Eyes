# QA do primeiro marco

## Executar

Na raiz, com SDK fixado pelo global.json:

```
dotnet run --project tests/EagleEyes.Tests
# Captura pixels reais da sessão Windows interativa:
dotnet run --project tests/EagleEyes.Tests -- --live --faults
```

O harness é um executável de testes sem pacotes externos. Falha com código diferente de zero se uma asserção não passar. Evidências ficam em `.test-artifacts/qa-<id>` e não entram no Git.

## Cobertura automatizável

- Sessão inicial, comandos fora de ordem, região sem largura e falta do executável FFmpeg.
- Modo `--live`: região 640x360 na origem do desktop, duas pausas, relógio congelado durante pausa, rejeição de início duplicado, finalização, leitura com ffprobe, exatamente um vídeo, ausência de áudio, dimensões e 15 fps, duração menor que o tempo de parede para excluir pausas.
- A ausência de áudio é verificada no arquivo final; o comando de captura também deve usar somente entrada de vídeo e `-an`.

## Validação visual obrigatória

Os testes de contêiner não demonstram legibilidade de texto, presença do cursor nem qualidade visual. Conferir manualmente no Windows:

1. Abrir editor com texto de 12 e 14 pontos; gravar região, pausar e retomar duas vezes; reproduzir o MP4 e conferir o salto das pausas, cursor e leitura do texto.
2. Repetir a barra em 100%, 125% e 150%; focar botões por teclado, tooltips e área clicável da silhueta.
3. Encaixar nas quatro bordas e cantos; recolher e clicar no aplicativo atrás da área que ficou vazia; expandir pela aba; testar fixação.
4. Arrastar entre monitores com escalas distintas e monitor à esquerda com coordenadas negativas; selecionar região dentro de cada monitor; testar monitor inteiro.
5. Desconectar o monitor salvo e reiniciar; confirmar barra visível em monitor existente.
6. Testar atalhos durante captura e pausa, conflito de atalhos com outro aplicativo, X durante gravação, configurações abertas e finalização lenta.
7. Colocar a barra dentro da área gravada e inspecionar o vídeo para exclusão dos controles. Sucesso da API de afinidade não comprova exclusão; o comportamento de ocultação deve ser a alternativa previsível.
8. Pasta sem permissão, FFmpeg inexistente/falho e finalização interrompida: erro legível, UI responsiva e temporários preservados; não prometer recuperação de segmentos inválidos.

## Estado dos resultados

Execução em 17/11/2026, Windows local, um monitor, 96 DPI (100%).

| Prova | Resultado e evidência local |
|---|---|
| Base FFmpeg sem Core/barra | PASS: `scripts/Proof-Capture.ps1`; `.test-artifacts/baseline-48c1b32609eb4101a4d694ff156c6456/proof.mp4`, 7,294 s de vídeo em 12,125 s de parede, duas pausas, um vídeo e zero áudio; decode completo aprovado. |
| Core integrado | PASS 27 asserções: `.test-artifacts/qa-1f4e4718092f42169e3e7a5728466acb/`. Região, duas pausas, relógio congelado, estados inválidos, ausência FFmpeg, saída que é arquivo, nomes únicos, primeiro arquivo intacto e reuso da sessão sem tempo/segmentos anteriores. Falha de ffprobe após a pausa preservou diretório e arquivos não vazios. |
| Fluxo real pela barra | PASS: `tests/UI-Flow.ps1` iniciou o aplicativo, invocou Gravar vídeo por UI Automation, arrastou o mouse de (120,120) a (760,480), confirmou aviso, aguardou contagem, enviou duas pausas/retomadas por Ctrl+Shift+P e finalizou por Ctrl+Shift+F. O botão Retomar foi observado em cada pausa. Fechou pelo botão após finalizar. Configurações prévias foram restauradas byte a byte. |
| MP4 do fluxo real | `.test-artifacts/ui-flow-eb89e85cedb942d2956096a2b3dae90d/EagleEyes-20261117-172339.mp4`: 640x360, 15/1 fps, 7,200 s, um vídeo, zero áudio, decode completo com FFmpeg exit 0. `flow.log` e `probe.json` guardam as evidências. Três segmentos persistidos. |
| Barra e docking | Smoke final `.test-artifacts/ui-smoke*.json/png`: quatro bordas × expandida/recolhida, oito estados com janela dentro da área útil, centro clicável, ponto externo à silhueta sem hit WPF, HWND criado e atalhos registrados. Validação automatizada em 96 DPI; imagens de amostra inspecionadas pelo agente de interface. Não comprova cliques externos reais em outros aplicativos nem DPI misto. |
| Exclusão dos controles: prova isolada | `tests/EagleEyes.AffinityProof`: janela magenta, captura gdigrab 64x64 antes/depois de WDA_EXCLUDEFROMCAPTURE e Hide. Tanto janela normal quanto AllowsTransparency=true: 4096 pixels magenta antes, zero depois da exclusão e zero após ocultar. API retornou true, erro 0. Artefatos `affinity-2a632590c6bc4bf3b34f769f58e6422f` e `affinity-dfe91d05a1f5449da67d98a0b046ee0c`. Demonstra esse ambiente e janela de teste; a barra usa ocultação por padrão para comportamento previsível em outros ambientes. |

Um frame em 1 s do vídeo Core inicial `qa-5a727ab2d3034b8a964ee3332ac4b880/frame.png` e do fluxo real `ui-flow-eb89e85cedb942d2956096a2b3dae90d/frame.png` foi inspecionado: desktop real e texto legível nessas amostras. Isso não certifica todos os tamanhos de texto ou escalas. Presença do cursor ainda precisa de inspeção específica.

## Repetir as provas de UI

```
powershell -ExecutionPolicy Bypass -File tests/UI-Flow.ps1
dotnet run --project tests/EagleEyes.AffinityProof
dotnet run --project tests/EagleEyes.AffinityProof -- --transparent
```

A automação real exige desktop interativo, nenhum Eagle Eyes aberto e espaço físico para a região (120,120)-(760,480). Ela move o ponteiro, envia atalhos globais, abre a pasta ao concluir e restaura as configurações do usuário em `finally`. Não executar enquanto outro aplicativo depender desses atalhos ou o usuário estiver manipulando o mouse.

Pendentes: monitor inteiro pelo fluxo real; várias telas e coordenadas negativas; escalas 125%/150% e DPI misto; desconexão de monitor; percepção de animação/arraste/foco e clique real através da área transparente; perda de permissão por ACL e interrupção externa de processo. Os testes de pasta que é arquivo e ffprobe removido comprovam caminhos de erro específicos, sem substituir todos os cenários de falha. Nenhum trecho não gravado corretamente é considerado recuperável.


## Publicação local final
O líder executou `scripts/Build.ps1 -Live`: build Release da solução com zero avisos/erros, harness básico8asserções e live24asserções (incluindo reuso). Publish em `artifacts/eagle-eyes` concluído. O executável publicado foi iniciado com --smoke-test:9relatórios (estado inicial mais8combinações borda/expansão), todos com janela criada, atalhos registrados, posição visível, canto transparente sem hit WPF e centro com hit. Evidências em `.test-artifacts/release-smoke*.json/png`. Este teste de hit WPF não substitui clique através da janela em outro aplicativo.

## Preparação do release 1.0.0
O usuário informou que realizou os testes e autorizou a publicação. A preparação repetiu build Release (zero avisos/erros) e as 8 verificações básicas; o núcleo e a interface não sofreram alterações funcionais. A versão explícita e os metadados são 1.0.0. Acentuação da documentação corrigida.
O aplicativo estava aberto e bloqueou a pasta habitual de publicação. O empacotamento passou a usar uma pasta isolada, sem encerrar o processo do usuário. ZIP inspecionado automaticamente: executável, DLLs, runtimeconfig, instruções e dependências presentes; sem FFmpeg, vídeos ou PDBs. SHA256 acompanha o pacote.
Publicação concluída: GitHub Release v1.0.0 estável e público, main e tag enviados sem force push. ZIP público baixado novamente em memória e comparado ao SHA256 local: PASS. Nenhum binário FFmpeg ou vídeo de teste foi distribuído.

## Checkpoint UX 1.1

Os resultados acima pertencem à versão inicial e não aprovam a nova experiência automaticamente. A etapa UX 1.1 usa preparação, contagem separada, barra horizontal visível durante gravação, mensagens próprias e descarte seguro.

Preparado `tests/UI-Flow.ps1` com cenários `Functional`, `ExclusionControl`, `Exclusion` e `All` (padrão). O roteiro faz backup das preferências, usa somente processos de teste e restaura as preferências no `finally`. Detecta uma instância já aberta e bloqueia antes de alterar configurações ou encerrar qualquer processo do usuário.

- Functional: Esc na seleção, Espaço sem vazamento para Gravar, cancelar preparação, selecionar novamente, seleção por mouse, clique com jitter de 2 pixels sem arraste, Esc durante contagem voltando à preparação; observar 3/2/1 e ausência de novos segmentos enquanto a contagem existe; pausar/retomar, relógio congelado, cancelar descarte preservando estado anterior, salvar com mensagem própria e descartar próxima gravação preservando o MP4 anterior.
- ExclusionControl: aplicativo real com `--ux-test --disable-capture-exclusion`, fundo magenta, gravação do monitor sob o ponteiro e captura gdigrab da barra visível; o controle positivo deve encontrar pixels da barra no vídeo.
- Exclusion: repetir sem desabilitar a exclusão; comparar pixels do fundo magenta no retângulo da barra real, tanto em captura gdigrab quanto no primeiro frame do MP4. Essa prova vai além do retorno da API. A ausência de segmentos durante 3/2/1 verifica que a contagem precede a captura, sem depender de WDA para esconder um erro de ordem.

O helper `tests/EagleEyes.AffinityProof --background --bounds x,y,w,h --ready arquivo.json` cria somente o fundo conhecido de teste. Os arquivos de evidência ficam em `.test-artifacts/ux11-ui-*` e não entram no Git.

Estado deste checkpoint: scripts preparados e sintaxe PowerShell válida; helper compila sem erros/avisos. Execução real aguardando integração e fechamento voluntário da instância do usuário PID 41228. Nenhum processo desse usuário foi encerrado pelo QA. Ainda não declarar os cenários UX 1.1 aprovados.

Checkpoint Core UX 1.1 informado pelo agente de captura: Release --live --faults PASS 60 asserções em .test-artifacts/qa-e6429fb82cfe49079ed50190e061c36e. Inclui descarte em gravação/pausa/falha, serialização, isolamento entre sessões, reparse points reais, falha de exclusão e limite de encerramento; relógio usa progresso out_time_us do FFmpeg. Este resultado não aprova a UI; testes visuais/reais continuam pendentes neste checkpoint.

## Resultado final de QA UX 1.1 — 17/11/2026

O bloqueio de instância foi resolvido pelo líder, fechando a versão antiga ociosa pelo próprio botão após aprovação. O QA não encerrou o processo do usuário à força. Os cenários abaixo foram executados contra o build Debug integrado 1.1.0 em desktop Windows interativo, um monitor 1920×1080, escala 100%. As preferências existentes foram restauradas depois de cada execução.

| Cenário real | Resultado e evidência |
|---|---|
| Functional | PASS em `.test-artifacts/ux11-ui-b2e708ac98b04a4caf913d91e062b445/Functional`: Esc na seleção inicial e na reseleção volta à principal; Espaço seleciona o monitor sob o ponteiro e não aciona Gravar por vazamento de tecla; cancelar preparação; seleção por mouse de 640×360; clique real com movimento de 2 pixels em Gravar; Esc na contagem retorna à preparação sem segmentos; 3/2/1 observados por UI Automation sem novos segmentos em disco; pausa com estado textual Pausado e duração congelada; duas pausas/retomadas por botões e atalhos; salvar com mensagem Seu vídeo foi salvo!, presença de Abrir pasta e fechamento por Concluir. MP4 640×360, 15 fps, um vídeo, zero áudio, decodificação completa aprovada. |
| Descarte pela UI | Na mesma execução Functional: cancelar a confirmação restaurou Pausado quando já estava pausado e Gravando quando estava gravando; confirmar descartou a segunda gravação, não gerou segundo MP4 e manteve os bytes do vídeo anterior intactos. A confirmação foi observada antes da ação destrutiva. |
| Exclusão — controle positivo | PASS em `.test-artifacts/ux11-ui-feeb35826c204e889bfaedf6ebcaacf4/ExclusionControl`: barra real visível com --ux-test --disable-capture-exclusion aparece no gdigrab e no primeiro frame do MP4, validando o caminho da prova. |
| Exclusão — comportamento normal | PASS em `.test-artifacts/ux11-ui-c064ee90ebad4430bc4e8550121f56b3/Exclusion`: mesmo retângulo da barra real visível contém o fundo magenta no gdigrab e no primeiro frame do MP4 com WDA padrão. A prova verifica pixels capturados, não apenas o retorno da API. Ambos os MP4s têm um vídeo, zero áudio, 15 fps e decode completo aprovado. |
| Contagem fora do vídeo | Nos dois cenários de exclusão, 3/2/1 foram observados e nenhum segmento novo apareceu até a contagem terminar. A região central de 300×300 pixels do primeiro frame manteve o fundo magenta, sem o painel da contagem. A combinação de ordem em disco e amostragem não depende só de WDA. |
| Arraste real e salvar pausado | PASS em `.test-artifacts/ux11-ui-4b8694a5c0354c32bbc55d18b249eb4d/Drag`: arraste pelo fundo da barra principal, texto da preparação e cronômetro moveu as janelas e as manteve dentro da área útil; clique no botão Pausar com movimento de 2 pixels executou a ação sem deslocar a janela. Salvar enquanto explicitamente pausado gerou MP4 válido sem áudio, 15 fps e decode completo aprovado. |
| Core final | Release --live --faults PASS **68 asserções**, informado e executado pelo agente de captura em `.test-artifacts/qa-11065829dcf846d0ae925c047a63820c`. Inclui relógio baseado somente em progresso codificado do FFmpeg, descarte em gravação/pausa/falha, concorrência, isolamento de sessões e vídeos anteriores, links/reparse points reais, falhas de exclusão, leitura de propriedade inválida, falha de início e limites de encerramento do processo/leitores. |

O primeiro ensaio Functional encontrou um problema do próprio teste: o ponteiro avançava antes de o Windows processar o mouse down, produzindo região menor; foi corrigido com espera explícita. Outro ensaio de pixels foi invalidado porque uma janela do terminal cobriu parte do fundo de teste; o fundo foi elevado antes do aplicativo de teste e as provas foram repetidas. Um ensaio de arraste usava o texto antigo da preparação; o localizador foi atualizado para o texto final. Esses ensaios permanecem nos artefatos como falhas do teste, não como aprovações nem como falhas atribuídas ao aplicativo.

Comandos atuais:

```
powershell -File tests/UI-Flow.ps1 -Scenario All
# ou execução focada: Functional, ExclusionControl, Exclusion, Drag
```

Pendências específicas: escalas 125%/150% e DPI misto, monitores com coordenadas negativas e desconexão durante fluxo, inspeção visual detalhada do anel animado e presença do cursor no MP4; amostragem do estado de botões durante finalização muito curta e abertura efetiva da pasta nesta nova UI. O botão Abrir pasta foi localizado, mas o roteiro usa Concluir para não abrir janelas extras. O smoke de quatro bordas é relatado separadamente pelo líder/agente desktop. WDA foi comprovado neste Windows; outros drivers e ambientes precisam repetir a prova, sem promessa universal de exclusão.

Métricas de exclusão (fração de pixels magenta no retângulo interno da barra): controle positivo gdigrab=0, primeiro frame=0; padrão WDA gdigrab=1, primeiro frame=1. Região central de contagem: controle=0.99835555555555555, padrão=0.99835555555555555.

## Retomada pendente: tooltips capturados
A inspeção visual pelo líder encontrou um popup branco de tooltip ainda capturado em `ux11-ui-c064ee90ebad4430bc4e8550121f56b3/Exclusion/actualbar-gdigrab.png`. A prova anterior aprovou o CORPO da barra, não todas as janelas auxiliares. O popup fica fora do retângulo inicialmente amostrado. Exclusão completa dos controles permanece pendente.
Próximo teste: hover no fundo/estado, Pausar e cronômetro, obter HWND/BoundingRectangle do ToolTip via UIAutomation, comparar pixels com fundo magenta e controle positivo, inclusive no MP4. Correção do ToolTip explícito/WDA do popup ainda não foi implementada. O agente QA recebeu limite de uso antes de concluir o cenário. Ver o ponto exato no ROADMAP.md.

## Fechamento da regressão de tooltips — 17/11/2026

A primeira prova de exclusão do corpo da barra não cobria os popups: o líder encontrou um tooltip branco vazando fora do retângulo amostrado. A correção usa WDA no HWND próprio de cada tooltip, com opacidade inicial zero até aplicar a afinidade e sem sombra independente. A validação abaixo substitui qualquer conclusão anterior de exclusão completa baseada somente no corpo da barra.

Também foi identificada e corrigida uma fragilidade do teste: a duração efetivamente codificada exibida na UI pode atrasar o instante da captura por causa do buffering. Os quadros antigos dos cenários `ux11-ui-7dbc0f2e86174d51a8d878d2160dea8e` e `ux11-ui-cfb5c2e74ea84c19a27f02ef1011093c`, escolhidos pelo relógio da UI, **não são mais aceitos como prova de tooltip no MP4**: as amostras de Pausar e duração podiam atingir a barra/cursor antes de o popup aparecer. Isso não exigiu alterar o relógio do aplicativo, que deve continuar representando o vídeo efetivamente codificado.

O roteiro focalizado agora espera o popup estabilizar, mede seu retângulo por UI Automation, inclui margem de dois pixels, usa o instante real em relação a `session.json/Started`, mantém o popup visível para a amostra e exige mais de 45% de pixels brancos no controle positivo. Assim, uma região escura da barra não produz falso positivo. Os três recortes MP4 positivos foram inspecionados e mostram os textos completos dos tooltips.

| Tooltip real | Controle positivo: tempo / pixels brancos MP4 | WDA normal: tempo / fundo magenta MP4 |
|---|---|---|
| Fundo/estado: atalhos | 5,442 s / 66,92% | 7,367 s / 100% |
| Pausar gravação | 10,544 s / 65,64% | 12,572 s / 95,64% |
| Tempo efetivamente gravado | 15,771 s / 64,75% | 17,925 s / 99,23% |

- Controle positivo aprovado: `.test-artifacts/ux11-ui-d0046dee2ea842528ee381066dbc3919/TooltipsControl`, executado com `--ux-test --disable-capture-exclusion`. Os três popups estavam visíveis via UI Automation e apareceram no gdigrab e nos quadros MP4; os recortes `tooltip-{background,pause,duration}-mp4-crop.png` mostram os textos legíveis.
- Comportamento normal aprovado: `.test-artifacts/ux11-ui-32598b59835042ada901b83052bff127/Tooltips`. Os mesmos três tipos de popup estavam visíveis via UI Automation, mas seus retângulos ficaram com 100% de fundo magenta nas capturas gdigrab. Nos quadros MP4, a inspeção dos três recortes mostrou somente fundo magenta e, nas margens de Pausar/duração, a ponta do cursor gravado; nenhum texto, fundo ou borda do tooltip permaneceu. O cursor deve continuar sendo gravado.
- Ambos os vídeos finais: um fluxo de vídeo, zero áudio, 15 fps, decodificação completa aprovada. `tooltip-exclusion.json` registra coordenadas, instantes, proporções de magenta/branco e origem do timestamp; `flow.log` registra o fluxo e a restauração das preferências. Nenhum processo de usuário precisou ser encerrado nesta retomada.

Comandos para repetir somente as provas afetadas:

```
powershell -File tests/UI-Flow.ps1 -Scenario TooltipsControl
powershell -File tests/UI-Flow.ps1 -Scenario Tooltips
```

### Comandos repetidos durante transições

Cenário focalizado `RepeatedActions` aprovado em `.test-artifacts/ux11-ui-b8e320c380b84543992cc9ae1cd7dab8/RepeatedActions`:

- Dois InvokePattern no botão Gravar: o segundo foi rejeitado por estar desabilitado; exatamente uma janela de contagem e uma sessão de gravação foram observadas.
- Dois InvokePattern no botão Parar e salvar: os dois foram entregues à UI, e a proteção de estado resultou em exatamente um MP4 e uma sessão. O arquivo final passou por ffprobe e decodificação completa, com um vídeo, zero áudio e 15 fps.

Esse cenário valida a interface durante início/finalização, além dos testes de concorrência do Core. O padrão `-Scenario All` inclui agora os cenários de tooltips e comandos repetidos. A retomada executou somente os cenários afetados; os resultados anteriores de Functional, Drag, Core e smoke permanecem documentados sem repetição desnecessária.

Limites da conclusão: exclusão dos popups comprovada neste Windows, monitor único a 100%, para os tooltips reais de fundo/estado, Pausar e cronômetro. Não implica garantia universal para outros drivers, versões do Windows ou DPI misto. Continuam pendentes os cenários de hardware/escala listados anteriormente. Build e publicação do executável local são relatados pelo líder separadamente; nenhum release remoto novo foi publicado pelo QA.
