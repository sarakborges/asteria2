# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O runtime inicializa os registries, cria um `OverworldWorld` single-player com seed de desenvolvimento fixa e gera a chunk de origem até o estágio `TERRAIN`. Isso ainda não é um mundo jogável: não há rendering de chunks, jogador, física, simulação ou persistência local.

A base inclui modelos de conteúdo, block states, registries, armazenamento paletizado, geometria voxel, chunks, biomas, ruído e partes do lifecycle/tick. O mundo local já mantém chunks geradas sob demanda e oferece consultas e escrita de blocos; ainda faltam as etapas de geração posteriores a `TERRAIN`, iluminação completa, chunks ativas, rendering, jogador/física, simulação e persistência.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Executar o bootstrap

1. Abra `project.godot` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com `F6` ou `F5`.

A execução inicializa os registries e gera a chunk de origem do Overworld. O viewport permanece sem geometria de mundo até a implementação do renderer.

## Estrutura

- `assets/`: arquivos de arte e recursos externos.
- `resources/`: recursos nativos do Godot.
- `scenes/`: cenas do projeto.
- `scripts/`: bootstrap e integração com o Godot.
- `src/Asteria/`: modelos e sistemas centrais de jogo.
- `tests/`: verificações automatizadas de paridade.
- `docs/architecture/`: decisões arquiteturais e auditoria de paridade.
