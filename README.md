# TabletMonitor

Aplicativo Android para usar um tablet como monitor adicional de um computador
Windows. Esta primeira etapa implementa o receptor de vídeo do tablet.

## Estado atual

- Configuração do IP e da porta do computador.
- Conexão TCP com reconexão automática.
- Recepção de vídeo H.264.
- Decodificação por hardware com `MediaCodec`.
- Exibição em tela cheia e orientação horizontal.
- Sem toque, S Pen, áudio ou controle remoto.

O servidor e o driver de monitor virtual do Windows serão criados nas próximas
etapas.

## Requisitos de desenvolvimento

- Android Studio compatível com Android Gradle Plugin 8.13.
- JDK 17.
- Android SDK 36.
- Tablet com Android 10 ou superior.

## Protocolo TabletMonitor v1

O servidor abre uma conexão TCP, por padrão na porta `5000`, e envia os dados em
ordem de rede, big-endian.

### Cabeçalho da sessão

| Campo | Tamanho | Valor |
|---|---:|---|
| Assinatura | 4 bytes | `TMON` |
| Versão | 1 byte | `1` |
| Largura | 2 bytes | Resolução horizontal |
| Altura | 2 bytes | Resolução vertical |
| FPS | 1 byte | Quadros por segundo |

### Quadros de vídeo

Para cada unidade de acesso H.264:

| Campo | Tamanho |
|---|---:|
| Tamanho do quadro | 4 bytes |
| H.264 Annex B | quantidade informada acima |

O servidor deve enviar SPS e PPS antes do primeiro quadro IDR e repetir esses
parâmetros após qualquer alteração de resolução ou reinício do codificador.

## Próxima etapa

Criar o servidor Windows que gere um fluxo H.264 de teste. Depois disso será
criado o Indirect Display Driver para que o Windows reconheça o tablet como um
monitor estendido.
