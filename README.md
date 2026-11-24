# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O runtime inicializa os registries, gera a chunk de origem até `TERRAIN` e posiciona um jogador temporário em primeira pessoa. Um controlador mantém uma região de renderização 3×3 ao redor do jogador, adicionando até uma chunk por frame. A visualização usa uma malha de diagnóstico: apenas blocos `MODEL` com forma de cubo completo, coloridos por `MapColor`, com colisão estática derivada da mesma geometria. Não é o renderer definitivo e não cobre modelos, texturas, fluidos ou transparência.

A base inclui modelos de conteúdo, block states, registries, armazenamento paletizado, geometria voxel, chunks, biomas, ruído e partes do lifecycle/tick. O mundo local já mantém chunks geradas sob demanda e oferece consultas e escrita de blocos. O streaming atual remove apenas os nós de renderização fora do raio: as `ProtoChunk` permanecem no cache do mundo para preservar alterações ainda não salvas. Por isso, a memória cresce com a exploração até existir persistência local e uma política segura de descarregamento. A geração ainda é síncrona; uma chunk por frame limita a quantidade de trabalho, mas não elimina travamentos durante a geração. Ainda faltam scheduler/lifecycle completo, etapas posteriores a `TERRAIN`, iluminação, renderer definitivo, interação com blocos, simulação e save/load.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Executar o bootstrap

1. Abra `project.godot` no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com `F6` ou `F5`.

A execução gera a chunk de origem e inicia o streaming em uma região 3×3 ao redor do jogador. Use W/A/S/D ou as setas para caminhar, mouse para olhar e Espaço para saltar. Clique esquerdo remove instantaneamente o bloco atingido; clique direito coloca terra na face adjacente, desde que não intercepte o jogador. Esc libera o mouse; clique para capturá-lo novamente. Essas ações são ferramentas temporárias de teste, sem inventário, ferramentas, drops ou tempo de mineração.

## Estrutura

- `assets/`: arquivos de arte e recursos externos.
- `resources/`: recursos nativos do Godot.
- `scenes/`: cenas do projeto.
- `scripts/`: bootstrap e integração com o Godot.
- `src/Asteria/`: modelos e sistemas centrais de jogo.
- `tests/`: verificações automatizadas de paridade.
- `docs/architecture/`: decisões arquiteturais e auditoria de paridade.
