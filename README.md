# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O runtime inicializa os registries, cria um `OverworldWorld` single-player com seed de desenvolvimento fixa e gera uma região de 3×3 chunks até `TERRAIN`. A cena exibe essa região por uma malha 3D de diagnóstico: somente blocos `MODEL` com forma de cubo completo, coloridos por `MapColor`, com colisão estática derivada da mesma geometria. Há um controlador temporário em primeira pessoa com gravidade, colisão, caminhada e salto. Essa visualização não é o renderer do jogo e não cobre modelos, texturas, fluidos ou camadas de transparência.

A base inclui modelos de conteúdo, block states, registries, armazenamento paletizado, geometria voxel, chunks, biomas, ruído e partes do lifecycle/tick. O mundo local já mantém chunks geradas sob demanda e oferece consultas e escrita de blocos. O controlador e a região visível ainda são um teste limitado: faltam streaming de chunks ativas, etapas de geração posteriores a `TERRAIN`, iluminação completa, renderer definitivo, interação com blocos, simulação e persistência.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Executar o bootstrap

1. Abra `project.godot` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com `F6` ou `F5`.

A execução gera uma região 3×3 em torno da origem e inicia o controlador temporário em primeira pessoa. Use W/A/S/D ou as setas para caminhar, mouse para olhar e Espaço para saltar. Esc libera o mouse; clique para capturá-lo novamente. A câmera, a geometria e as colisões são componentes de diagnóstico, não a implementação definitiva.

## Estrutura

- `assets/`: arquivos de arte e recursos externos.
- `resources/`: recursos nativos do Godot.
- `scenes/`: cenas do projeto.
- `scripts/`: bootstrap e integração com o Godot.
- `src/Asteria/`: modelos e sistemas centrais de jogo.
- `tests/`: verificações automatizadas de paridade.
- `docs/architecture/`: decisões arquiteturais e auditoria de paridade.
