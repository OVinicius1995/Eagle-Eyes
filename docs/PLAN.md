# Plano e decisões

1. Inspecionar pasta, SDKs, FFmpeg e Git. A pasta inicial não tinha fontes/histórico; o remoto estava sem referências. Git inicializado em main com o origin solicitado.
2. Definir contratos e áreas exclusivas em AGENTS.md antes de delegar captura, desktop e QA, com integração pelo líder.
3. Provar gdigrab, região, cursor configurado, 15 fps, duas pausas por segmentos, concatenação sem áudio e exclusão de janela antes de expandir a interface.
4. Integrar barra WPF compacta e manter captura/controlador separados da apresentação.
5. Executar build Release, harness e fluxo real de seleção/atalhos; publicar executável local. Registrar separadamente validações automáticas e visuais.
6. Preservar o checkpoint inicial 8f4e054. Após o usuário informar testes concluídos e autorizar, preparar versão 1.0.0, ZIP, SHA256 e release no GitHub sem force push.

## Tecnologia
SDK 11.0.100 estável com WPF 11, fixado sem prerelease. SDK 10/reference pack WPF 10 não estavam instalados. Política consultada: https://dotnet.microsoft.com/en-us/platform/support/policy . Dispositivo: https://ffmpeg.org/ffmpeg-devices.html#gdigrab .

## Pausas e recuperação
Pausa encerra o segmento; retomar inicia outro com os mesmos parâmetros. Não há entrada de áudio nem silêncio artificial. Concatenação exclui a pausa por construção. Há latência de inicialização/fechamento de processo; a duração do arquivo é a evidência definitiva.
A saída usa nome único. Temporários permanecem inclusive após sucesso e não são apagados antes da validação. O app não recria quadros nunca capturados.

## Pacote público
Distribuir apenas o aplicativo dependente de runtime, instruções e documentação. FFmpeg permanece externo por falta de proveniência exata confirmada da cópia local. Registrar versão e SHA256; não incluir vídeos de teste no Git ou no ZIP.
