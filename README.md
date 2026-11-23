# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O projeto ainda não contém um mundo jogável. A cena inicial executa somente o bootstrap e a validação dos registries de conteúdo em `scripts/Main.cs`.

A base já inclui modelos de conteúdo, block states, registries, armazenamento paletizado, geometria voxel, primitivas de chunks, biomas, ruído e partes do lifecycle/tick de chunks. Ainda precisam ser integrados o mundo local, o gerenciador e a geração de chunks, o rendering, o jogador/física, a simulação e a persistência local.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Executar o bootstrap

1. Abra `project.godot` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com `F6` ou `F5`.

A execução atual valida o carregamento e o congelamento dos registries; não inicia gameplay.

## Estrutura

- `assets/`: arquivos de arte e recursos externos.
- `resources/`: recursos nativos do Godot.
- `scenes/`: cenas do projeto.
- `scripts/`: bootstrap e integração com o Godot.
- `src/Asteria/`: modelos e sistemas centrais de jogo.
- `tests/`: verificações automatizadas de paridade.
- `docs/architecture/`: decisões arquiteturais e auditoria de paridade.
