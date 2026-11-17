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
Estado: implementação iniciada, sem nova publicação autorizada.

### Etapas
- [x] Inspeção do código existente e Git limpo; contratos/arquivos definidos em AGENTS.md antes de distribuir.
- [x] Core: descarte serializado com proteção dos arquivos de outras sessões; 58 verificações reais aprovadas, incluindo reparse/symlink e arquivos desconhecidos.
- [x] Implementada seleção: instrução, Espaço no monitor do ponteiro, Esc e consumo das teclas. Validação UI real ainda pendente.
- [x] Implementada preparação: área destacada, barra persistente, iniciar/reselecionar/cancelar. Validação UI real ainda pendente.
- [x] Implementada contagem central 3,2,1, anel verde e Esc voltando à preparação. Remoção e espera do compositor antes do início; prova em vídeo ainda pendente.
- [x] Implementado arraste por áreas não interativas com limiar e ajuste à área útil. QA por mouse ainda pendente.
- [x] Integrada barra horizontal sempre visível com estado textual, cronômetro, pausa/retomada e salvar. Cronômetro por progresso FFmpeg implementado; regressão dessa mudança em execução.
- [x] Integrada confirmação de descarte com pausa prévia, restauração de estado e exclusão restrita. Cenários pela UI ainda pendentes.
- [x] Integradas mensagens breves, detalhes/logs, sucesso após validação e ação Abrir pasta.
- [ ] Build e testes de regressão/Core/UI, exclusão real das barras/contagem no gdigrab e MP4 sem áudio.
- [ ] Executável local e commit de entrega; documentar pendências visuais/hardware.

### Próximo passo exato
Build integrado Debug e Release passou sem erros/avisos. Executar tests/UI-Flow.ps1 após fechamento da instância antiga PID41228 do usuário; pedido de fechamento enviado e ainda sem confirmação. Não encerrar essa instância sem autorização: pode haver gravação ativa. QA preparou modos funcional/exclusão/controle positivo com fundo magenta para provar a barra real excluída por gdigrab e contagem ausente do MP4. Core está concluindo testes de arquivo bloqueado e cronômetro por progresso FFmpeg. Após estabilizar fontes: salvar commit local da integração, executar QA, corrigir falhas encontradas e publicar executável em pasta isolada (não sobrescrever app aberto). Não publicar novo release nem enviar a branch sem pedido.

Retomada: ler AGENTS.md seção UX1.1 e esta seção antes de agir; verificar Git e processos. Não repetir provas já aprovadas sem mudança relevante. O usuário pediu checkpoints frequentes; saldo de uso não é acessível ao agente.