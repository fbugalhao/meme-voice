# MemeVoice - Voice Changer Application

MemeVoice é uma aplicação de mudança de voz para Windows 11 que permite transformar sua voz em tempo real durante chamadas e jogos, utilizando o Discord como plataforma principal.

## Funcionalidades

- **Processamento de Áudio em Tempo Real**: Captura do microfone físico, aplica efeitos DSP e redireciona para VB-Cable
- **9 Efeitos de Voz Pré-definidos**:
  1. Grave/Robusto (pitch para baixo)
  2. Agudo/Esquilo (pitch para cima) 
  3. Robô (vocoder-like / ring modulation)
  4. Eco/Cavernão (reverb/delay)
  5. Pitch Livre (slider -12 a +12 semitons)
  6. Demônio (pitch baixo + distorção)
  7. Chipmunk extremo (pitch muito alto)
  8. Telefone/rádio (filtro passa-faixa)
  9. Alien/interferência (modulação)

## Arquitetura

```
Microfone real → Captura (WASAPI) → Buffer → Cadeia de efeitos DSP → Saída (WASAPI) → VB-Cable (CABLE Input) → Discord/Jogo
```

## Componentes Principais

- **AudioEngine**: Gerencia captura WASAPI do microfone selecionado e renderização WASAPI para o dispositivo de saída
- **EffectChain**: Cadeia thread-safe de efeitos com troca atômica
- **DeviceManager**: Enumeração de dispositivos de áudio e detecção do VB-Cable
- **ConfigStore**: Persistência de configurações em JSON
- **SimpleLogger**: Registro de erros

## Requisitos

- Windows 10 ou superior
- VB-Audio Virtual Cable instalado
- .NET 8 Runtime

## Uso

1. Instale o VB-Audio Virtual Cable
2. Execute o aplicativo MemeVoice
3. Selecione seu microfone de entrada e "CABLE Input" como saída
4. Escolha um efeito e clique em "Iniciar"
5. Configure o Discord para usar "CABLE Output" como dispositivo de entrada

## Atalhos de Teclado

- **Ctrl+Alt+V**: Alternar processamento
- **Ctrl+Alt+N**: Próximo efeito

## Construção

```bash
dotnet build MemeVoice.sln
```

## Testes

```bash
dotnet test
```