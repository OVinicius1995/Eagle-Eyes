# Eagle Eyes Ã¢â‚¬â€ roadmap

## Implementado
- [x] InspeÃƒÂ§ÃƒÂ£o local/remota, Git main/origin preservando ausÃƒÂªncia de histÃƒÂ³rico, SDK11.0.100 estÃƒÂ¡vel fixado.
- [x] Contratos e ÃƒÂ¡reas de arquivos definidos antes da delegaÃƒÂ§ÃƒÂ£o; regras em AGENTS.md.
- [x] Core separado de WPF: FFmpeg gdigrab, cursor, H.264 a 15 fps, sem ÃƒÂ¡udio.
- [x] Pausa/retomada por segmentos, concatenaÃƒÂ§ÃƒÂ£o em MP4 ÃƒÂºnico, nomes sem sobrescrita.
- [x] ValidaÃƒÂ§ÃƒÂ£o FFprobe, duraÃƒÂ§ÃƒÂ£o dos trechos, rejeiÃƒÂ§ÃƒÂ£o de comandos concorrentes/invÃƒÂ¡lidos.
- [x] Falhas e temporÃƒÂ¡rios preservados; nenhum apagamento automÃƒÂ¡tico inclusive apÃƒÂ³s sucesso.
- [x] Barra WPF escura com vetores, configuraÃƒÂ§ÃƒÂµes, fechar ocioso, indicador/tempo, pausar/retomar/finalizar.
- [x] RegiÃƒÂ£o por arraste limitada a um monitor; opÃƒÂ§ÃƒÂ£o monitor inteiro e contagem regressiva3 s.
- [x] Atalhos Ctrl+Shift+P e Ctrl+Shift+F com detecÃƒÂ§ÃƒÂ£o de conflito; ÃƒÂºnica instÃƒÂ¢ncia desktop.
- [x] Quatro bordas, orientaÃƒÂ§ÃƒÂ£o, arraste, recolhimento170ms e fixaÃƒÂ§ÃƒÂ£o nas configuraÃƒÂ§ÃƒÂµes.
- [x] PersistÃƒÂªncia por monitor/borda/posiÃƒÂ§ÃƒÂ£o/estado, fallback de monitor e ÃƒÂ¡rea ÃƒÂºtil; PerMonitorV2.
- [x] Escolha de pasta/FFmpeg, descoberta components, abrir saÃƒÂ­da no Explorer.
- [x] Tentativa WDA e alternativa padrÃƒÂ£o: ocultar durante captura, exibir somente depois de fechar trecho.
- [x] README, scripts, plano e dependÃƒÂªncias/licenÃƒÂ§as documentados.

## Validado atÃƒÂ© agora
- [x] Prova direta FFmpeg antes da expansÃƒÂ£o da interface: regiÃƒÂ£o, duas pausas, decode completo MP4 sem ÃƒÂ¡udio.
- [x] Prova Core real: duas pausas, vÃƒÂ­deo ÃƒÂºnico15 fps, sem ÃƒÂ¡udio, dimensÃƒÂµes e duraÃƒÂ§ÃƒÂ£o sem pausas.
- [x] Falha na validaÃƒÂ§ÃƒÂ£o FFprobe preserva arquivos recuperÃƒÂ¡veis.
- [x] Build Release integrado: zero erros e avisos.
- [x] Smoke WPF: janela/atalhos registrados, limites visÃƒÂ­veis no monitor atual a 100%; PNG da barra inspecionado.
- [x] Texto legÃƒÂ­vel em um quadro640x360 da prova Core; nÃƒÂ£o generalizar para todas as escalas.
- [x] Prova WDA em janela magenta de teste no ambiente atual; ainda nÃƒÂ£o comprova barra transparente.

## Em validaÃƒÂ§ÃƒÂ£o / entrega
- [x] Fluxo real usa handlers da barra e alternativa padrão de ocultar.
- [ ] Fluxo real botÃƒÂ£oÃ¢â€ â€™seleÃƒÂ§ÃƒÂ£oÃ¢â€ â€™contagemÃ¢â€ â€™duas pausasÃ¢â€ â€™finalizaÃƒÂ§ÃƒÂ£o.
- [ ] Publicar executÃƒÂ¡vel local e preparar commit solicitado (~20:32 UTC em 17/11/2026).

## ValidaÃƒÂ§ÃƒÂ£o visual/hardware pendente
- [ ] AparÃƒÂªncia, ÃƒÂ¡rea clicÃƒÂ¡vel e animaÃƒÂ§ÃƒÂ£o em100%, 125%, 150% nas quatro bordas.
- [ ] DPI misto, monitores com coordenadas negativas, movimentaÃƒÂ§ÃƒÂ£o e desconexÃƒÂ£o fÃƒÂ­sica.
- [ ] ExclusÃƒÂ£o da barra transparente com opÃƒÂ§ÃƒÂ£o de ocultar desabilitada; cursor no vÃƒÂ­deo.
- [ ] ReproduÃƒÂ§ÃƒÂ£o no player do Windows e legibilidade de texto em cenÃƒÂ¡rios reais.
- [ ] Pasta sem permissÃƒÂ£o por ACL e falhas de disco/finalizaÃƒÂ§ÃƒÂ£o durante escrita.

Consulte docs/QA.md para comandos e evidÃƒÂªncias atuais. Recursos implementados nÃƒÂ£o equivalem a validaÃƒÂ§ÃƒÂ£o visual completa. Sem push/publicaÃƒÂ§ÃƒÂ£o remota nesta etapa. Sem editor, captura de imagem, webcam, login ou nuvem.
