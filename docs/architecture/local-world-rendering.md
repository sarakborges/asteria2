# Port do runtime local e renderização do Overworld

## Objetivo

Entregar primeiro um mundo local gerado e visualmente correto, mantendo a arquitetura real do Minecraft Java 26.3. O produto é single-player e não oferece conexões externas. O runtime preserva `IntegratedServer`, o `ClientLevel` e o canal interno de pacotes locais (`LocalConnection`/`Connection`) usado entre as duas metades, sem escutar sockets externos nem publicar o mundo para LAN/internet.

## Fronteira local, não multiplayer

Asteria deve preservar estes papéis:

- **Servidor integrado:** `IntegratedServer` local; mantém autoridade sobre mundo, chunks, ticks, geração e iluminação server-side.
- **Gerenciamento de chunks:** caminho equivalente a `ServerChunkCache`, `ChunkMap`, `ChunkHolder`, `ChunkGenerationTask`, `GeneratingChunkMap` e `ChunkPyramid`. As tarefas devem obedecer aos estados e dependências da referência, sem um loop manual que força cada etapa de forma síncrona.
- **Cliente local:** um `ClientLevel` separado do nível autoritativo, ligado ao Integrated Server pelo canal interno de pacotes locais. Não há listeners de socket externo nem publicação LAN.
- **Backend gráfico Godot:** converte os dados extraídos em recursos do Godot e os apresenta. Não é dono da lógica de mundo, culling, seleção de faces, modelos, iluminação ou fluids.

Não implementar sockets externos, publicação LAN, conexão a servidores remotos, matchmaking, lobbies, presença ou hospedagem multiplayer. Preservar o canal interno local e os pacotes do Integrated Server. Não remover os papéis server-side apenas porque o produto não oferece multiplayer externo.

## Implementações próprias a retirar/substituir

A remoção é feita apenas quando o equivalente de referência estiver portado e integrado, para não deixar o projeto sem caminho funcional:

- `OverworldWorld`: substituir o host manual por níveis e gerenciamento local equivalentes a `ServerLevel` e `ServerChunkCache`.
- `ChunkLightingTasks`: substituir a orquestração manual síncrona pela implementação equivalente a `ThreadedLevelLightEngine` e suas tarefas, mantendo a engine de iluminação já portada quando houver paridade.
- `WorldGenContext`: alinhar ao contrato 26.3, com `ServerLevel`, `ChunkGenerator`, `StructureTemplateManager`, `ThreadedLevelLightEngine`, executor e listener de dirty/save de `LevelChunk`. O tipo existe na referência; não é uma oportunidade para criar um contexto Asteria simplificado.
- Qualquer mesh de cubos, malha diagnóstica, material derivado de `MapColor`, iluminação direcional substituta, sistema de streaming de renderização ad hoc ou simulação simplificada de fluidos: não reintroduzir. Se houver código residual desses protótipos, removê-lo em mudanças isoladas.
- A adaptação específica de Godot fica restrita à apresentação: recursos de textura/material, buffers/meshes, instâncias, câmera e chamadas de renderização. Deve consumir resultados do port, não duplicar algoritmos de Minecraft.

## Ordem de implementação

### 1. Runtime integrado local e chunk lifecycle

- Portar os tipos-base de nível e o caminho server-side necessário, preservando as responsabilidades de `ServerLevel`, `ServerChunkCache`, `ChunkMap` e holders.
- Completar a cadeia de `EMPTY` até `FULL`, inclusive promotion de `ProtoChunk` para `LevelChunk`, loading e unload.
- Substituir o caminho síncrono/serial de geração e a orquestração própria de iluminação por seus equivalentes reais.
- Manter as etapas de features/structures no pipeline, mas sem implementar conteúdo nem algoritmos concretos desses sistemas neste marco, salvo dependências mínimas reais exigidas pelo contrato da referência.

### 2. Blocos, estados, modelos e atlas

- Completar os blocos e estados usados pelo terreno gerado; não tratar um registro genérico de bloco como port completo de cada bloco.
- Portar os contratos de modelos, model states, baked models/quads, sprites, atlas e reload dos recursos necessários ao terreno.
- Adicionar os dados reais de modelos e texturas dos blocos usados pelo Overworld. Não inferir textura a partir de MapColor e não gerar cubos genéricos como substituto.
- Preservar modelos e formas não cúbicas. A seleção de faces deve respeitar estado, face occlusion, neighbor culling e render shape.

### 3. Compilação e apresentação das seções

- Portar o caminho 26.3 de leitura regional/snapshot, compilação de seções, dirty propagation, culling e troca atômica do resultado compilado.
- Acompanhar o fluxo atual da referência: `LevelExtractor`, cache/cópia de regiões, `SectionRenderDispatcher`, `SectionCompiler`, modelos e atlas.
- Adaptar somente a etapa de upload/apresentação para Godot. Não criar um segundo scheduler de chunks ou um mesher próprio simplificado.
- O primeiro teste visual pode usar câmera estática e uma pequena área de chunks; não exige controlador de jogador.

### 4. Lighting e fluids

- Expor ao caminho de renderização os dados corretos de luz de bloco e céu, shade/ambient occlusion e os limites de seção.
- Completar `FlowingFluid`, estados still/flowing de água e lava, leitura de vizinhos, níveis/queda, propagação, conversão de fontes, tick scheduling e forma geométrica conforme 26.3.
- Portar `FluidRenderer` e sua integração com modelos/seções para faces superiores, laterais, cantos, altura, transparência e iluminação. Não usar um retângulo ou cubo translúcido genérico como substituto.
- Simulação de fluidos pode ser validada após o pipeline de chunk/world local, mas o rendering de fluids faz parte do marco visual.

### 5. Depois do primeiro marco visual

Features concretas, estruturas/templates, entidades, movimento, colisão de jogador e interação de quebrar/colocar blocos são etapas posteriores. Não devem bloquear uma cena em que o terreno e os fluids já estejam corretamente renderizados ao redor de uma câmera estática.

## Seed de mundo

- Uma nova criação de mundo usa `WorldOptions.defaultWithRandomSeed()` ou o fluxo equivalente 26.3.
- A seed escolhida é uma propriedade do mundo e deve ser persistida em seus dados locais.
- Reabrir um mundo carrega a seed persistida; não escolhe outra seed.
- Seeds explícitas e estáveis são aceitas em testes de paridade e fixtures. Nunca fixar uma seed de desenvolvimento no bootstrap normal do jogo.

## Critérios de aceitação do primeiro marco

1. Cada execução do fluxo de criação de mundo novo escolhe uma seed diferente sem intervenção do usuário.
2. A mesma seed produz o mesmo terreno, biomas e iluminação em testes de paridade.
3. O runtime local solicita chunks por meio da arquitetura server-side portata e publica chunks prontos pelo estado correto, sem depender de multiplayer.
4. Uma câmera estática consegue ver terreno real gerado, com modelos/texturas, faces corretamente ocultadas, formas não cúbicas, iluminação de bloco/céu e água/lava renderizadas.
5. Não existe mesh cúbico de diagnóstico, cor de MapColor como textura, iluminação direcional substituta ou simulação simplificada usada no lugar dos ports.
6. A CI compila o projeto e executa testes automatizados dos contratos portados. A validação visual é registrada separadamente; build verde, sozinho, não é prova de paridade.

## Referências

- [Rendering — Minecraft Java 26.3](https://minecraftdocs.dev/systems/rendering/)
- [Section meshing — Minecraft Java 26.3](https://minecraftdocs.dev/systems/rendering/section-meshing)
- [WorldGenContext — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/level/chunk/status/WorldGenContext.html)
- [ChunkMap — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/server/level/ChunkMap.html)
- [WorldOptions — Minecraft 26.3](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/level/levelgen/WorldOptions.html)
