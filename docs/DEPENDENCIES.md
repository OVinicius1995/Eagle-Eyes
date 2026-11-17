# Dependências e distribuição

- .NET SDK 11.0.100 / runtime Windows Desktop 11: Microsoft, código .NET sob MIT e componentes com seus próprios avisos. Origem https://dotnet.microsoft.com/download/dotnet/11.0. Nenhum pacote NuGet de terceiros previsto.
- FFmpeg e ffprobe externos fornecidos pelo usuário em `components/ffmpeg-master-latest-win64-gpl/bin`. Versão observada: `N-128959-g4dbee3b241-20261116`, compilado gcc 16.2.0. Configuração inclui `--enable-gpl --enable-version3 --enable-libx264`.
- O arquivo `components/ffmpeg-master-latest-win64-gpl/LICENSE.txt` contém GPL versão 3. A origem exata do arquivo baixado pelo usuário ainda não foi confirmada. O nome coincide com builds de https://github.com/BtbN/FFmpeg-Builds ; isso é identificação provável, não proveniência verificada.
- FFmpeg oficial: https://ffmpeg.org ; licenciamento: https://ffmpeg.org/legal.html ; x264: https://www.videolan.org/developers/x264.html .

O Git ignora components. Esta entrega não redistribui FFmpeg no repositório. Para distribuir um pacote contendo os executáveis, preservar LICENSE/avisos, registrar URL e hash da distribuição exata, disponibilizar código-fonte correspondente e cumprir as obrigações das bibliotecas habilitadas. Não declarar esta build como LGPL.

A identidade e os ícones vetoriais Eagle Eyes são implementados no projeto, sem assets Movavi.
SHA256 dos executáveis locais inspecionados em 17/11/2026:
- ffmpeg.exe: D0DDD82AA9247BEEFCF31788E6EF9AA492E2BF1FB9CA06E67E487CD59F8F1EB1
- ffprobe.exe: 0038F578F03161F23F6016142E2CD31BA30A290FA9B6BD1B7DEF3D06DA47EACE
