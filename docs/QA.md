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
