# Voice Changer — Fase 1: Engine de Pitch/Timbre em Tempo Real

Data: 2026-09-09
Status: Aprovado para planejamento

## Contexto e escopo

Objetivo geral do produto (fora do escopo desta spec): um voice changer para uso com amigos no Discord e em jogos online, com três fases:

1. **Fase 1 (esta spec)**: engine de captura/processamento de áudio em tempo real com efeitos de pitch/timbre, roteada para Discord/jogos via dispositivo de áudio virtual.
2. Fase 2 (futura): soundboard (tocar efeitos sonoros por cima da fala, hotkeys dedicados).
3. Fase 3 (futura): conversão de voz por IA (estilo RVC — transformar a voz do usuário na voz de outro personagem/pessoa).

Esta spec cobre **apenas a Fase 1**.

## Plataforma e dependências externas

- Windows apenas (11), .NET / C# (WPF para GUI).
- Roteamento de áudio para Discord/jogos via **VB-Audio VB-CABLE** (freeware fechado, mas maduro e amplamente usado). O usuário instala o VB-Cable separadamente; o app detecta se está presente.
- Pitch shifting via **SoundTouch.NET** (biblioteca open source).

## Arquitetura

```
Microfone real → Captura (WASAPI) → Buffer → Cadeia de efeitos DSP → Saída (WASAPI) → VB-Cable (CABLE Input) → Discord/Jogo
```

O usuário seleciona "CABLE Input (VB-Audio Virtual Cable)" como microfone no Discord/jogo. O app captura do microfone físico, aplica o efeito escolhido em tempo real e escreve o resultado no VB-Cable. Toda a lógica roda em user-mode; não há driver de kernel próprio.

## Componentes

- **AudioEngine**: gerencia captura WASAPI do microfone selecionado e renderização WASAPI para o dispositivo de saída (VB-Cable). Buffers pequenos (~10-20ms) para baixa latência.
- **EffectChain / EffectProcessor**: cadeia de efeitos DSP. Cada efeito implementa `IVoiceEffect.Process(float[] buffer)`. Não conhece WASAPI, apenas transforma `float[]`.
- **DeviceManager**: enumera dispositivos de entrada/saída, detecta se o VB-Cable está instalado, permite trocar o microfone de entrada.
- **HotkeyManager**: registra atalhos globais via `RegisterHotKey` para trocar de efeito ou ligar/desligar o processamento.
- **MainWindow (WPF) + Tray Icon**: lista de efeitos, slider de pitch livre, indicador de status (ativo/bypass), configuração de hotkeys, seleção de dispositivos.
- **ConfigStore**: persiste preferências (dispositivo, hotkeys, último efeito) em JSON em `%AppData%`.

Cada componente é isolado e comunica-se por interfaces bem definidas: `AudioEngine` não conhece a GUI; `EffectChain` só recebe/devolve `float[]`; `HotkeyManager` dispara eventos que a `MainWindow` escuta.

## Efeitos da Fase 1

Conjunto clássico + extras "meme":

- Grave/Robusto (pitch para baixo)
- Agudo/Esquilo (pitch para cima)
- Robô (vocoder-like / ring modulation)
- Eco/Cavernão (reverb/delay)
- Pitch livre (slider -12 a +12 semitons)
- Demônio (pitch baixo + distorção)
- Chipmunk extremo (pitch muito alto)
- Telefone/rádio (filtro passa-faixa)
- Alien/interferência (modulação)

## Interface

- Janela WPF simples: lista/seleção de efeitos, slider de pitch livre, seleção de dispositivos de entrada/saída, indicador de status.
- Ícone na bandeja do sistema (minimiza sem fechar o processamento).
- Atalhos de teclado globais configuráveis para trocar de efeito e ligar/desligar o processamento sem precisar focar a janela.

## Tratamento de erros

- **VB-Cable não instalado**: `DeviceManager` detecta ausência do "CABLE Input" na lista de dispositivos de render; GUI mostra aviso com instrução de instalação e desabilita o botão de iniciar até resolver.
- **Microfone desconectado durante uso**: `AudioEngine` captura a exceção do WASAPI, para o processamento, notifica o usuário e volta ao estado "parado" sem travar o app.
- **Buffer underrun/glitch de áudio**: registrado em log local para diagnóstico; não interrompe o app.
- **Troca de efeito em tempo real**: troca de `IVoiceEffect` é thread-safe (troca atômica de referência) para evitar corrupção no meio do processamento de um buffer.
- **Hotkey já em uso por outro programa**: `HotkeyManager` reporta falha de registro na GUI e permite escolher outra combinação.

## Testes

- **Unitários de DSP**: cada `IVoiceEffect` testado isoladamente com buffers de amostras conhecidos (ex.: seno de 440Hz), validando a transformação esperada (pitch shift altera a frequência fundamental, filtro passa-faixa atenua fora da banda, etc.) sem depender de hardware de áudio.
- **Integração do pipeline**: alimentar a `EffectChain` completa com um arquivo WAV de fixture e validar que a saída não é silêncio/NaN e tem as características esperadas.
- **Manual**: verificação de latência percebida e teste real com Discord + VB-Cable antes de considerar a Fase 1 concluída.

## Fora de escopo (Fase 1)

- Soundboard e sons extras (Fase 2).
- Conversão de voz por IA (Fase 3).
- Suporte a outras plataformas além de Windows.
- Driver de áudio virtual próprio.
