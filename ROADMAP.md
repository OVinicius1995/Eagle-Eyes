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
Branch: feature/ux-1.1. Base publicada preservada: v1.0.0; HEAD inicial 383034b.
Estado: retomado após o usuário confirmar restauração do limite. Correção dos tooltips implementada e build Debug aprovado; provas focadas em andamento, sem nova publicação autorizada.

### Etapas
- [x] Inspeção do código existente e Git limpo; contratos/arquivos definidos em AGENTS.md antes de distribuir.
- [x] Core: descarte serializado com proteção dos arquivos de outras sessões; 68 verificações reais aprovadas, incluindo reparse/symlink e arquivos desconhecidos.
- [x] Implementada seleção: instrução, Espaço no monitor do ponteiro, Esc e consumo das teclas. Validação UI real aprovada no monitor atual.
- [x] Implementada preparação: área destacada, barra persistente, iniciar/reselecionar/cancelar. Validação UI real aprovada no monitor atual.
- [x] Implementada contagem central 3,2,1, anel verde e Esc voltando à preparação. Remoção e espera do compositor antes do início; prova em vídeo aprovada no monitor atual.
- [x] Implementado arraste por áreas não interativas com limiar e ajuste à área útil. QA por mouse aprovado em fundo, preparação e cronômetro.
- [x] Integrada barra horizontal sempre visível com estado textual, cronômetro, pausa/retomada e salvar. Cronômetro por progresso real FFmpeg implementado e validado, inclusive zero sem quadros codificados.
- [x] Integrada confirmação de descarte com pausa prévia, restauração de estado e exclusão restrita. Cenários pela UI aprovados.
- [x] Integradas mensagens breves, detalhes/logs, sucesso após validação e ação Abrir pasta.
- [x] Build Debug/Release e Core: 68 asserções reais. Functional UI PASS: seleção, Esc, contagem, pausas, salvar, descarte/cancelamento e MP4 sem áudio. Exclusão da barra REAL comprovada com controle positivo, pixels e primeiro frame de MP4; contagem ausente. Arraste real aprovado. Atenção: exclusão de TOOLTIP ainda pendente; ver próximo passo.
- [ ] Executável local e commit de entrega; documentar pendências visuais/hardware.

### Próximo passo exato - retomar aqui
Branch feature/ux-1.1; checkpoint anterior f63e794 preservado. O usuário confirmou restauração do limite e autorizou continuar.
- Correção implementada em CaptureSafeToolTip.cs e MainWindow.cs: ToolTip explícito, Opacity zero antes de abrir, WDA no HwndSource próprio do popup, sem sombra separada. Tooltips de botões, corpo, duração e alça usam o helper; controle positivo --ux-test --disable-capture-exclusion desabilita também a exclusão do popup.
- Build Debug passou sem erros/avisos. QA ativo preparando/executando testes focados TooltipsControl/Tooltips, com hover no corpo/estado, Pausar e cronômetro, bounding rectangle UIAutomation, fundo magenta, pixels gdigrab e quadros MP4. Aguardar evidência antes de declarar a exclusão completa dos controles.
- Se prova falhar, corrigir somente o comportamento afetado e repetir a prova. Após passar, atualizar o executável Release local, relatório QA/ROADMAP e salvar commit final. Não refazer toda a regressão sem motivo.
- Não publicar novo release nem enviar a branch sem pedido. Se limite aparecer novamente, salvar checkpoint e encerrar conforme combinado.
### Resultados já aprovados / não refazer sem motivo
- Core final: 68 asserções Release --live --faults, artefatos .test-artifacts/qa-11065829dcf846d0ae925c047a63820c. Inclui relógio por out_time_us, descarte seguro, arquivo bloqueado, symlinks reais e isolamento entre sessões.
- Functional UI: .test-artifacts/ux11-ui-b2e708ac98b04a4caf913d91e062b445. Seleção mouse/Espaço, Esc seleção e contagem, preparação persistente, 3/2/1 sem arquivos, pausas e relógio, salvar, cancelamento de descarte restaurando gravando/pausado, descarte confirmado preservando vídeo anterior; MP4 sem áudio, 15 fps e decodificação.
- Drag: .test-artifacts/ux11-ui-4b8694a5c0354c32bbc55d18b249eb4d. Fundo principal, preparação e cronômetro arrastáveis com limiar; clique em Pausar não arrasta; salvar pausado aprovado.
- Exclusão do CORPO da barra real: controle positivo ux11-ui-feeb35826c204e889bfaedf6ebcaacf4 e WDA ux11-ui-c064ee90ebad4430bc4e8550121f56b3. Área da barra apresentou fundo magenta com WDA, mas popup tooltip fora da área amostrada vazou. Centro do primeiro frame sem contagem e ausência de segmentos durante 3/2/1 aprovados.
- Build final local: scripts/Build.ps1 passou sem erros/avisos e 9 verificações básicas; artifacts/eagle-eyes/EagleEyes.App.exe é 1.1.0 local. Ainda NÃO é entrega final por causa dos tooltips.
- Smoke do executável Release: 9 relatórios .test-artifacts/ux11-release-smoke*.json, quatro bordas expandida/recolhida a 100%, posição visível e hit tests WPF aprovados.
- Instância antiga PID41228 estava ociosa e foi fechada normalmente pelo próprio botão após autorização. Nenhum processo de teste deveria permanecer; conferir novamente ao retomar.

### Pendências de ambiente
DPI 125%/150%, DPI misto e múltiplos monitores/negativos/desconexão, cursor visual, player do Windows, clique real através da janela e inspeção detalhada da animação ainda não possuem evidência individual. Não converter aprovação geral em certificação desses cenários.

### Motivo do encerramento deste checkpoint
Agente QA reportou explicitamente limite de uso. O usuário pediu salvar etapas e interromper antes de esgotar o tempo, para retomar após restaurar. Código e testes estão salvos em commits locais; a próxima sessão deve começar pela correção dos tooltips acima. Saldo de uso total não é visível ao líder.
