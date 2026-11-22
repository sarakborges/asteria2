# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Executar

1. Abra o arquivo \`project.godot\` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute o projeto com \`F6\` ou \`F5\`.

A cena inicial agora abre uma área local jogável com nove chunks, terreno de teste, iluminação, câmera em primeira pessoa, colisão física e edição de blocos. O terreno plano é temporário: geração procedural, streaming e demais sistemas de mundo ainda serão conectados às estruturas de chunk já existentes.

### Controles

- \`W A S D\`: movimentar
- \`Espaço\`: pular
- Mouse: olhar
- Botão esquerdo: quebrar bloco
- Botão direito: colocar bloco
- \`1\`: grama; \`2\`: terra; \`3\`: pedra
- \`Esc\`: liberar/recapturar o cursor

## Estrutura

- \`assets/\`: arquivos de arte e recursos externos.
- \`resources/\`: recursos nativos do Godot.
- \`scenes/\`: cenas do projeto.
- \`scripts/Gameplay/\`: mundo local, visualização de chunks e controle do jogador.
- \`src/Asteria/\`: modelos e sistemas centrais de jogo.
- \`tests/\`: verificações automatizadas.
