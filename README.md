# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O runtime inicializa os registries, cria um `OverworldWorld` single-player com seed de desenvolvimento fixa e gera a chunk de origem até `TERRAIN`. A cena exibe essa chunk por uma malha 3D de diagnóstico: somente blocos `MODEL` com forma de cubo completo, coloridos por `MapColor`. Essa visualização não é o renderer do jogo e não cobre modelos, texturas, fluidos ou camadas de transparência. Ainda não há jogador, física, simulação ou persistência local.

A base inclui modelos de conteúdo, block states, registries, armazenamento paletizado, geometria voxel, chunks, biomas, ruído e partes do lifecycle/tick. O mundo local já mantém chunks geradas sob demanda e oferece consultas e escrita de blocos; ainda faltam as etapas de geração posteriores a `TERRAIN`, iluminação completa, chunks ativas, renderer definitivo, jogador/física, simulação e persistência.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Executar o bootstrap

1. Abra `project.godot` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com `F6` ou `F5`.

A execução inicializa os registries, gera a chunk de origem do Overworld e mostra sua visualização 3D de diagnóstico. A câmera e a luz também são temporárias; a cena ainda não oferece movimentação ou interação.

## Estrutura

- `assets/`: arquivos de arte e recursos externos.
- `resources/`: recursos nativos do Godot.
- `scenes/`: cenas do projeto.
- `scripts/`: bootstrap e integração com o Godot.
- `src/Asteria/`: modelos e sistemas centrais de jogo.
- `tests/`: verificações automatizadas de paridade.
- `docs/architecture/`: decisões arquiteturais e auditoria de paridade.
