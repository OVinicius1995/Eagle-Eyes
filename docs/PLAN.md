# Plano e decisÃµes

1. Inspecionar pasta, SDKs, FFmpeg e Git; preservar histÃ³rico. Pasta inicialmente vazia; ls-remote retornou sucesso sem refs. Git inicializado em main e origin configurado.
2. Definir contratos e Ã¡reas exclusivas em AGENTS.md antes de distribuir implementaÃ§Ã£o.
3. Prova tÃ©cnica de gdigrab: regiÃ£o, cursor, 15 fps, duas pausas por segmentos, concatenaÃ§Ã£o sem Ã¡udio e exame da exclusÃ£o de janela. A prova precede expansÃ£o da interface.
4. Integrar barra WPF compacta; captura/controlador desacoplados da apresentaÃ§Ã£o.
5. Build Release e harness; produzir executÃ¡vel. Registrar separadamente validaÃ§Ãµes automÃ¡ticas e visuais.
6. Preparar commit local no ponto solicitado pelo usuÃ¡rio (~20:32 UTC, 17/11/2026). Sem push.

## Tecnologia
SDK 11.0.100 estÃ¡vel, instalado com WPF reference pack 11.0.0; net11.0-windows para desktop. global.json nÃ£o aceita prerelease. .NET9 instalado nÃ£o escolhido por estar fora de suporte na data da implementaÃ§Ã£o. .NET10 runtime existe, mas SDK/reference pack WPF10 nÃ£o estÃ£o instalados.
PolÃ­tica consultada: https://dotnet.microsoft.com/en-us/platform/support/policy
FFmpeg gdigrab: https://ffmpeg.org/ffmpeg-devices.html#gdigrab

## Pausa e finalizaÃ§Ã£o
Pausa encerra corretamente o segmento atual. Retomar abre um novo segmento com os mesmos parÃ¢metros. NÃ£o se captura Ã¡udio nem se injeta silÃªncio. ConcatenaÃ§Ã£o exclui o tempo de pausa por construÃ§Ã£o. InÃ­cio/fechamento dos processos pode introduzir pequena latÃªncia nas transiÃ§Ãµes; duraÃ§Ã£o do arquivo Ã© a evidÃªncia definitiva.

## PublicaÃ§Ã£o e recuperaÃ§Ã£o
Arquivo final recebe nome Ãºnico, sem sobrescrita; temporÃ¡rios permanecem disponÃ­veis em falhas. O app nÃ£o reconstrÃ³i quadros nunca capturados. BinÃ¡rios de terceiros e evidÃªncias nÃ£o entram no Git.
