# Marca oficial do Eagle Eyes

A imagem aprovada pelo usuário está preservada em `Emblema de olhos de águia azul-ardósia.png` e na cópia idêntica `eagle-eyes-original.png`.
SHA256 de ambas: `4FB6DFD59C974351C7E954FEA789E5ECBE7F4CE80DC58664EAB4DADBAF1CB9F2`.

A conversão é inteiramente local, por System.Drawing/GDI+ em `scripts/BrandAssetTool.cs`, executado por `scripts/Prepare-BrandAssets.ps1`. Não houve redesenho, geração de novo símbolo, recoloração ou envio a conversores externos. O fundo branco, inclusive os olhos, foi removido. Pixels opacos de referência foram preservados; as bordas foram separadas do matte branco com estimativa de alfa/cor local para evitar franjas brancas. O reamostramento necessário preserva a proporção.

Fonte: 1254x1254; área original do símbolo: x359, y444, 535x365. Saída: `Assets/eagle-eyes.png`, 1024x1024 RGBA, centralizado, símbolo com aproximadamente 85% da largura. O arquivo `Assets/eagle-eyes-source.json` registra o processo.

`Assets/eagle-eyes.ico` é um contêiner ICO real, com oito entradas de 32 bits e payloads PNG com alfa: 16, 20, 24, 32, 48, 64, 128 e 256 pixels. Não é um PNG apenas renomeado.

O ICO é usado em ApplicationIcon do EXE, nos recursos WPF de todas as janelas e pela bandeja nativa. A bandeja registra o ícone no Windows e oferece Mostrar controles e Sair, com confirmação segura para gravação ativa. O Windows controla se o ícone aparece diretamente na bandeja ou no painel de ícones ocultos.

Não há instalador nem gerador de atalhos neste repositório; Iniciar.cmd inicia o aplicativo local. O empacotamento inclui Assets/eagle-eyes.ico, necessário para a bandeja. Ícones funcionais (engrenagem, X, vídeo, pausa, continuar, parar) são vetores WPF separados e não alteram o símbolo oficial.

Prévia claro/escuro: `.test-artifacts/brand-preview/light-dark-contact-sheet.png`. Testes independentes de estrutura, alfa, proporção, olhos, cor, vetores e HICON extraído do EXE: `tests/EagleEyes.BrandTests`. Inspeção visual de tamanhos pequenos foi feita sem mudar a cor aprovada.
