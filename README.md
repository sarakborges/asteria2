# Asteria 2

Nova implementação do Asteria, construída do zero com Godot .NET.

## Escopo do produto

Asteria é exclusivamente single-player/offline/local. Redstone e multiplayer/network play não fazem parte do projeto. Consulte [docs/architecture/project-scope.md](docs/architecture/project-scope.md).

## Requisitos

- Godot 4.7.2 — edição .NET
- .NET SDK 8.0

## Estado atual

O projeto está concentrado no port dos sistemas de Minecraft Java 26.3. A base contém conteúdo e registries, estados de bloco, armazenamento paletizado, geometria voxel, estruturas de chunks, biomas e partes da geração do Overworld. O mundo de geração e seus dados ainda não estão conectados a um cliente jogável completo.

O protótipo visual anterior foi removido: não há mais malha cúbica diagnóstica, cores de MapColor usadas como materiais, luz direcional substituta, controlador de movimento/interação ou streaming de renderização próprio. Esses componentes não eram ports dos sistemas equivalentes da referência e não serão usados como base do jogo.

A iluminação e o rendering ainda estão incompletos. O runtime do Godot, por enquanto, inicializa os registries; não apresenta terreno ou jogador até que os sistemas equivalentes sejam portados e integrados. A ausência de uma cena jogável é intencional e evita apresentar um substituto incorreto como implementação do jogo.

A referência de paridade é Minecraft Java 26.3. Os sistemas fora do escopo do produto estão documentados em [docs/architecture/project-scope.md](docs/architecture/project-scope.md). O status detalhado de cada port está em [docs/architecture/minecraft-parity-audit.md](docs/architecture/minecraft-parity-audit.md).

## Executar o bootstrap

1. Abra project.godot no Godot 4.7.2 .NET.
2. Aguarde a importação inicial dos arquivos.
3. Execute com F6 ou F5.

A cena inicializa os registries de conteúdo. Ela ainda não é uma cena jogável.

## Verificações automatizadas

Execute o harness de paridade:

```bash
dotnet run --project tests/BlockRegistryChecks/BlockRegistryChecks.csproj
```

## Estrutura

- assets/: arquivos de arte e recursos externos.
- resources/: recursos nativos do Godot.
- scenes/: cenas do projeto.
- scripts/: bootstrap e integração mínima com o Godot.
- src/Asteria/: modelos e sistemas centrais de jogo.
- tests/: verificações automatizadas de paridade.
- docs/architecture/: decisões arquiteturais e auditoria de paridade.
