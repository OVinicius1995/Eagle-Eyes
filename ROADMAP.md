# Eagle Eyes - roadmap

## Release 1.0.0 implementado
- [x] Arquitetura separada entre WPF, sessão e FFmpeg; contratos e responsabilidades documentados antes da delegação.
- [x] Captura de região em um monitor e opção de monitor inteiro, cursor, H.264 a 15 fps, sem áudio.
- [x] Contagem regressiva, múltiplas pausas e retomadas, MP4 único sem tempo de pausa.
- [x] Duração efetiva ajustada pelos arquivos, comandos serializados, única instância desktop.
- [x] Barra compacta com vetores, configurações, estado, pausa/retomada e finalização.
- [x] Arraste, encaixe nas quatro bordas, orientação, recolhimento animado e fixação.
- [x] Persistência de monitor/borda/posição/estado, área útil, fallback de monitor e PerMonitorV2.
- [x] Atalhos Ctrl+Shift+P e Ctrl+Shift+F com detecção de conflito.
- [x] Escolha de pasta, nomes únicos, abertura no Explorer e descoberta do FFmpeg externo.
- [x] Tentativa de exclusão por WDA e ocultação previsível como padrão.
- [x] Tratamento de falhas e preservação dos temporários, inclusive após sucesso.
- [x] README, regras, plano, QA, scripts e dependências documentados.

## Validações realizadas
- [x] Prova direta FFmpeg antes de expandir a interface: região, duas pausas, MP4 sem áudio e decodificação completa.
- [x] Harness: 27 verificações, incluindo reuso de sessão, nomes únicos e preservação após falha no FFprobe.
- [x] Fluxo real via UIAutomation, seleção por mouse e duas pausas/retomadas pelos atalhos: MP4 de 7,2 segundos, 640x360, 15 fps, sem áudio e decodificação completa.
- [x] Build Release sem erros/avisos e executável publicado localmente.
- [x] Smoke Debug/Release das quatro bordas nos dois estados a 100%, posição visível e hit tests internos WPF.
- [x] Quadros de vídeo e PNGs da barra inspecionados; texto legível nas amostras.
- [x] WDA verificado por pixels em janelas WPF opaca e transparente neste ambiente.
- [x] Usuário informou que realizou os testes e aprovou a publicação do release 1. Os cenários individuais não foram detalhados.

## Publicação
- [x] Checkpoint inicial local `8f4e054`, preservado.
- [x] Versão 1.0.0 e notas de release definidas; pacote com dependências externas e SHA256.
- [x] Branch main, tag v1.0.0 e GitHub Release publicados com ZIP e SHA256: https://github.com/OVinicius1995/Eagle-Eyes/releases/tag/v1.0.0 . Download público conferido por SHA256.

## Verificações específicas ainda não documentadas
- [ ] Escalas 125%/150%, DPI misto, monitores negativos, movimento entre monitores e desconexão física.
- [ ] Monitor inteiro pelo fluxo real e presença visual do cursor no vídeo.
- [ ] Reprodução no player do Windows; leitura de textos em diferentes cenários.
- [ ] Clique real através da parte transparente em outro aplicativo e percepção da animação/foco.
- [ ] Falhas por ACL, disco sem espaço e interrupção externa de processo.

A aprovação geral do usuário não substitui evidência individual desses cenários. Sem editor, captura de imagem, webcam, login ou nuvem nesta versão.

## UX 1.1 - checkpoint de retomada
Branch local: feature/ux-1.1. Base pública v1.0.0 preservada. Versão local 1.1.0 implementada e validada no ambiente disponível, incluindo a prova final robusta dos tooltips. Nenhum novo release ou push autorizado.

### Etapas concluídas
- [x] Inspeção do projeto existente; contratos e arquivos definidos antes da delegação, sem recriar o projeto.
- [x] Seleção por arraste ou Espaço no monitor sob o ponteiro; consumo do KeyUp evita acionar Gravar em seguida. Esc volta à barra principal.
- [x] Preparação persistente com destaque, Gravar, Selecionar novamente e Cancelar; seleção não inicia captura.
- [x] Contagem central 3, 2, 1 com anel verde, painel translúcido e Esc para voltar à área preparada; fechada antes da captura.
- [x] Arraste nas áreas não interativas das barras, incluindo cronômetro, com limiar e preservação dos botões; encaixes da barra principal mantidos.
- [x] Barra horizontal visível durante captura e pausa, estado textual, cronômetro por progresso codificado FFmpeg, pausa/retomada e salvar.
- [x] Parar e salvar gravando ou pausado, proteção contra comandos duplicados, mensagem de sucesso e Abrir pasta após validar o arquivo.
- [x] Descarte confirmado com pausa prévia, restauração do estado anterior ao cancelar, encerramento e exclusão somente de arquivos conhecidos da sessão ao confirmar.
- [x] Proteção de arquivos anteriores, sessões vizinhas, arquivos desconhecidos, bloqueados, symlinks e diretórios reparse; falhas de exclusão informadas.
- [x] Mensagens em português, detalhes em logs e Ver detalhes, sem promessa de recuperar quadros ausentes.
- [x] Exclusão do corpo da barra e dos três TOOLTIPs reais comprovada no gdigrab e MP4, com controle positivo legível. WDA aplicado separadamente ao HWND do popup antes de pintar.
- [x] Build Debug/Release sem erros/avisos; executável local 1.1.0 atualizado em artifacts/eagle-eyes/EagleEyes.App.exe e acessível por Iniciar.cmd.

### Evidências aprovadas / não refazer sem mudança relevante
- Core: 68 asserções Release --live --faults em `.test-artifacts/qa-11065829dcf846d0ae925c047a63820c`, incluindo relógio out_time_us, descarte seguro e isolamento entre sessões.
- Functional UI: `ux11-ui-b2e708ac98b04a4caf913d91e062b445`. Seleção por mouse/Espaço, Esc seleção/contagem, preparação, pausas repetidas e relógio congelado; salvar; cancelar descarte restaurando gravando/pausado; descarte preservando o MP4 anterior.
- Drag: `ux11-ui-4b8694a5c0354c32bbc55d18b249eb4d`. Arraste real no fundo principal, preparação e cronômetro; botão Pausar com pequeno movimento não arrastou a janela; salvar pausado aprovado.
- Exclusão do corpo da barra: controle positivo `ux11-ui-feeb35826c204e889bfaedf6ebcaacf4` e WDA `ux11-ui-c064ee90ebad4430bc4e8550121f56b3`. Contagem não criou segmentos e estava ausente no primeiro frame.
- Tooltip completo (prova final): controle positivo `ux11-ui-d0046dee2ea842528ee381066dbc3919/TooltipsControl`, com os três textos legíveis nos frames MP4 de 5,442 / 10,544 / 15,771 segundos e proporção de pixels brancos acima de 64%. WDA `ux11-ui-32598b59835042ada901b83052bff127/Tooltips`, frames de 7,367 / 12,572 / 17,925 segundos: fundo magenta 100% / 95,64% / 99,23%, sem texto/fundo do tooltip; resíduos correspondem à ponta do cursor, visível e gravável. Os três popups estavam visíveis pela UIAutomation e foram amostrados após estabilizar a posição. Líder e QA inspecionaram todos os crops do controle e WDA.
- Ensaios anteriores `ux11-ui-7dbc0f2e86174d51a8d878d2160dea8e` / `ux11-ui-cfb5c2e74ea84c19a27f02ef1011093c`: NÃO usar os antigos frames MP4 de 8/13 segundos como prova dos popups. O relógio de codificação atrasava a imagem e o crop podia amostrar a barra. A prova final usa o horário da sessão/hover estabilizado e exige fundo branco/texto no controle positivo para evitar falso positivo.
- RepeatedActions: `ux11-ui-b8e320c380b84543992cc9ae1cd7dab8`. Gravar duas vezes gerou uma contagem/sessão; salvar duas vezes gerou um MP4. Decodificação, vídeo único, 15 fps e nenhuma faixa de áudio aprovados.
- Smoke do executável Release: nove relatórios `ux11-release-smoke*.json`, quatro bordas expandida/recolhida a 100%, limites visíveis e hit tests WPF aprovados.
- Build após correção dos tooltips: scripts/Build.ps1 passou com zero erros/avisos e nove verificações básicas; arquivo executável confirmado como 1.1.0.0.

### Histórico dos checkpoints
- a34639d: contratos e plano antes da distribuição.
- 372b8e4: integração da preparação, contagem, barra visível e descarte.
- f63e794: checkpoint por limite de uso; preservou código/testes e registrou o vazamento dos tooltips como pendência explícita.
- f7738fe: correção CaptureSafeToolTip e MainWindow após o usuário confirmar restauração do limite.
A falha dos tooltips foi corrigida e aprovada nas provas finais robustas de três popups. A autorização anterior para fechar a instância ociosa foi usada pelo botão normal, sem interrupção forçada de gravação. Scripts QA restauram preferências e encerram somente processos de teste.

### Próximo passo exato ao retomar
Nenhuma implementação do escopo atual ficou em andamento. Abrir `Iniciar.cmd` para validar manualmente a experiência local 1.1.0. Ler AGENTS.md e docs/QA.md antes de novas alterações; conferir Git/processos. Se houver novo pedido, continuar desta branch e preservar as evidências já aprovadas. Publicação da 1.1 depende de novo pedido do usuário; não fazer push ou release automaticamente.

### Validações de ambiente ainda pendentes
- [ ] Escalas 125%/150%, DPI misto, múltiplos monitores, coordenadas negativas e desconexão física. Espaço foi validado no monitor disponível, sem certificar hardware inexistente no teste.
- [ ] Movimento/legibilidade do cursor em cenários gerais e reprodução no player do Windows. A ponta do cursor já foi observada nas amostras MP4 dos tooltips.
- [ ] Clique real através da parte transparente em outro aplicativo e inspeção detalhada das animações/foco.
A exclusão dos controles foi comprovada neste Windows/FFmpeg; não constitui garantia universal para todo ambiente. Manter essa distinção ao informar resultados.

### Continuidade e limite de uso
O saldo total não é visível ao líder. Manter etapas concluídas, testes, pendências e próximo passo neste arquivo e salvar commits locais frequentes. Se agente/ferramenta informar limite ou o usuário pedir pausa, salvar checkpoint e encerrar. O usuário restaurou o limite para concluir esta etapa; a interrupção anterior não perdeu alterações.

## Marca oficial, ícones e configurações - checkpoint atual
Estado: etapa concluída localmente. Branch feature/ux-1.1; release público v1.0.0 preservado. Sem push ou nova publicação.
- [x] Fonte aprovada preservada em seu nome original e cópia canônica eagle-eyes-original.png, SHA256 idêntico.
- [x] Conversão exclusivamente local sem redesenho/recoloração: branco/olhos transparentes, antialias sem halo branco aparente, PNG1024 centralizado com85,06% da largura.
- [x] ICO verdadeiro de32bits com16/20/24/32/48/64/128/256, payloads/offsets válidos, alfa transparente/opaco/semitransparente em todos.
- [x] Engrenagem preenchida compacta com oito dentes/furo em14DIPs, X coeso e demais ícones vetoriais; CaptureSafeToolTip preservado.
- [x] Configurações escuras com marca, Salvar/Cancelar e sem campo de pasta FFmpeg. Par nas pastas padrão, sem PATH/CWD/configuração antiga. Alerta quando faltar o par.
- [x] Marca no EXE, janelas e bandeja nativa com Mostrar controles/Sair e encerramento protegido. Sem instalador/gerador de atalhos existente.
- [x] Corrigido corte do campo de saída; preview legível e novo teste de edição/Salvar/Cancelar aprovado após a correção.
- [x] Build Debug e Release sem erros/avisos; nove verificações básicas do Core. Core/captura não alterado nesta etapa.
- [x] Executável atualizado em artifacts/eagle-eyes/EagleEyes.App.exe; ICO copiado para Assets ao lado dele e conferido por hash contra a fonte.

### Evidências concluídas
- Marca/caminhos/vetores/EXE publicado:88 asserções em `.test-artifacts/branding-6e9402454d00405fb7766efd1f537060/result.json`. ICO com oito entradas reais, PNG1024 com dois olhos vazados, proporção/centro e cores preservadas; HICON do EXE corresponde à máscara do ICO oficial. Fonte mediana RGB93/118/123, saída opaca93/117/122, diferença de reamostragem sem recoloração.
- Comparações claro/escuro e ícones16..256 inspecionados em `.test-artifacts/brand-preview/light-dark-contact-sheet.png`. Preview final das configurações e galeria vetorial em `.test-artifacts/ui-brand-preview`.
- UIA final depois do ajuste visual: `.test-artifacts/brand-ui-ebee00d946c44c29a1d3e31ed5cd2f02`. Uma caixa de texto para saída, nenhum campo FFmpeg, marca presente, habilitação de monitor por modo, persistência/Cancelar sem alterar preferências, HICON small/big Main/Settings, Mostrar controles e Sair ocioso aprovados. Preferências originais restauradas.
- Proteção da bandeja durante gravação já aprovada em `.test-artifacts/brand-ui-ace634ac70f742e79e82df6ad892ce58`: cancelar saída restaurou gravação; confirmar descarte fechou sem gerar MP4. Callbacks nativos testados por PostMessage controlado, não clique físico no ícone do Explorer.
- Componentes ausentes:5 verificações em `.test-artifacts/missing-components-59657344a2d94093b23435f92699daa6`. Aplicativo isolado fora do repositório, sem FFmpeg: alerta correto, nenhuma seleção/encoder iniciado, retorno à barra e fechamento normal. Nenhum componente do usuário foi movido/apagado.
- Release runtime:9 relatórios `.test-artifacts/brand-release-smoke*.json` com estados/área útil/hit tests aprovados; `.test-artifacts/brand-release-report.json` confirmou bandeja registrada, ICO presente e ícone de janela256px. Processo encerrou sem erro.

### Checkpoints e continuidade
Checkpoint e84d775 preservou todos os arquivos antes da retomada final. O agente QA ficou indisponível e o líder concluiu sequencialmente, sem descartar resultados anteriores. Proveniência e transformação detalhadas em docs/BRANDING.md; resultados em docs/QA.md. Nenhuma imagem enviada a conversores externos.
Próximo passo ao retornar: ler AGENTS/ROADMAP, conferir Git/processos e aguardar a validação manual do usuário pelo Iniciar.cmd. Nenhuma implementação ficou em andamento. Não refazer assets nem a regressão inteira sem mudança relevante. Novo release/push depende de pedido do usuário.
Pendências manuais: outras escalas125%/150%, DPI misto/múltiplos monitores e aparência/posição real do ícone na bandeja/overflow, controlada pelo Windows. Ícones de atalhos externos eventualmente criados pelo usuário não foram testados; não existe instalador nesta versão.

## Diálogos e guia de identidade visual - etapa atual
Estado: concluído localmente, sem push ou release. O guia Markdown é privado por pedido do usuário.
- [x] Extraído o tema aprovado para src/EagleEyes.App/Design/EagleEyes.Theme.xaml; configurações e mensagens usam VisualIdentity.CreateResources.
- [x] Abrir pasta usa ação secundária escura; Concluir usa ação primária verde. Fonte12, raio6, padding19/9, altura mínima36, borda/foco/hover/desabilitado compartilhados. Eventos e papéis padrão/cancelamento preservados.
- [x] Guia completo criado localmente em docs/IDENTIDADE-VISUAL.md com cores, tipografia, medidas, iconografia, marca e exemplos de reutilização.
- [x] Guia adicionado ao .gitignore, nunca rastreado e ausente da pasta do executável; não copiar seu conteúdo para documentos públicos ou pacotes. Testes toleram sua ausência em outras cópias do repositório.
- [x] Build Release sem erros/avisos, nove verificações básicas Core e executável local atualizado no caminho habitual artifacts/eagle-eyes/EagleEyes.App.exe.
- [x] Testes de componentes:21 aprovados em .test-artifacts/design-7c1ff0120fcf47729be0edfe134fae22. Diálogo real renderizado, mesmas cores/estilos das configurações, dimensões, template arredondado/foco, botões habilitados e Concluir fechando o modal por evento real. Papéis Enter/Esc conferidos via IsDefault/IsCancel, sem afirmar envio de teclas físicas.
- [x] Previews saved-dialog.png/settings.png inspecionados pelo líder. Abrir pasta manteve o handler existente; não se abriu o Explorer durante este teste de aparência.

Próximo passo: validar manualmente pelo Iniciar.cmd se desejado. Nenhuma implementação ficou em andamento. O arquivo privado permanece apenas na máquina. Nenhuma publicação autorizada nesta etapa; o pedido de privacidade foi interpretado para o guia .md solicitado, sem alterar os assets de marca já rastreados em commits anteriores. Confirmar alcance adicional antes de eventual publicação futura.
Pendentes de ambiente: outras escalas/DPI e validação visual do usuário; Core/captura não alterado, não repetir regressão completa sem necessidade.

## Instalador e atualização — em andamento (18/11/2026)
- [x] Commit 9920f71 conferido: HEAD e origin/feature/ux-1.1 coincidem; árvore limpa; guia privado ignorado e não rastreado.
- [x] Contratos e propriedade definidos no AGENTS antes de implementar; líder mantém sequencialmente.
- [ ] Instalador por usuário com atalhos/ícone e runtime incluído, protegendo aplicativo em execução.
- [ ] Consulta manual/automática de releases estáveis; atualização supervisionada pela página oficial e instalador. Sem atualização silenciosa nesta etapa.
- [ ] Build, testes de respostas remotas/versões, geração e smoke de instalação.
- [ ] Registrar evidências, limitações FFmpeg/assinatura, commit local e ponto exato de continuidade. Nenhum push/release autorizado neste pedido.
