# Auditoria de paridade — Asteria 2 / Minecraft Java 26.3

## Escopo e baseline

- Repositório: `sarakborges/asteria2`
- Branch auditada: `feature/block-registry`
- Commit-base auditado: `80d57adb82dabc285819ef11384f8c72ada9d5c0`
- Inventário após o incremento de biomas: 141 arquivos versionados, incluindo 98 arquivos C# em `src/Asteria`, 13 arquivos C# no harness de testes e 1 arquivo C# de bootstrap em `scripts`.
- Escopo de produto: jogo exclusivamente single-player/offline/local; redstone e multiplayer/network play estão explicitamente fora do escopo.
- Referência: Minecraft Java Edition 26.3, usando o código-fonte decompilado e os contratos observáveis das classes equivalentes.

Esta auditoria cobre todo o conteúdo atualmente existente no repositório. “Paridade confirmada” significa que o comportamento que Asteria implementa foi comparado com o membro correspondente da referência; não significa que Asteria já implemente o jogo inteiro.

## Escopo do produto

Asteria é um jogo local single-player/offline. Esta decisão altera quais partes da referência são requisitos e quais são deliberadamente excluídas.

**Fora de escopo:**
- Redstone: circuitos, níveis de potência, fios, repetidores, comparadores e propagação de sinais.
- Multiplayer/network play: transporte de rede, protocolo de jogo, servidor/cliente autoritativo, replicação e sincronização remota.

**Ainda necessários no modo local:**
- Simulação e scheduler locais para atualizações de blocos/fluidos e random ticks não relacionados a redstone.
- Mundo, chunks, geração, entidades, física, rendering e persistência/save-load local.
- APIs de serialização interna ou em disco quando necessárias para salvar/carregar o mundo; não implementar codecs de protocolo de rede apenas para imitar o servidor Minecraft.

## Resultado executivo

Os subsistemas de conteúdo e armazenamento — identificadores, registries, holders, estados genéricos, estados de bloco, mapas de IDs, paletas e armazenamento compactado — têm paridade comportamental confirmada no escopo que foi portado e testado. A geometria discreta já contém as 48 transformações octaédricas, rotação de ocupação, enumeração de caixas/arestas/faces e as consultas geométricas fundamentais. `VoxelShape` também contém recortes de faces, arestas contínuas e colisão por eixo. O clipping de raios com `BlockHitResult` foi adicionado no incremento seguinte; validação pendente até o CI correspondente.

Asteria ainda não é um jogo funcional completo: mundo, chunks completos, biomas avançados, geração, iluminação, simulação de fluidos, entidades, rendering e persistência local continuam ausentes ou parciais. O armazenamento quart, núcleo climático, efeitos visuais e ruído 2D de informação dos biomas já foram iniciados. A geometria local agora cobre rotação contínua, raycast, colisão e oclusão de formas, mas ainda não está ligada ao mundo/rendering. Multiplayer, transporte de rede e servidor/cliente são fora de escopo, não pendências.

## Matriz por subsistema

| Subsistema Asteria | Referência Minecraft 26.3 | Resultado |
|---|---|---|
| `ResourceLocation` | `Identifier` | Paridade comportamental no escopo implementado. O namespace padrão `asteria` é intencional; a proteção de caminho de `ResolveAgainst` é adaptada ao .NET. |
| `Registry<T>` | `MappedRegistry<T>` | Índices por chave, localização, valor por identidade e ID; holders estáveis; registro intrusivo; congelamento; ciclo de tags e lifecycle comparados. Nenhuma divergência algorítmica confirmada. |
| `DefaultedRegistry<T>` | `DefaultedMappedRegistry<T>` | Fallbacks de valor, chave, ID, `ById`, `GetAny`, `GetRandom` e validação correspondem ao contrato consultado. |
| `Holder<T>` / `HolderSet<T>` | `Holder` / `HolderSet` | Variantes direct/reference, binding, identidade, tags, unwrap, membership e regras de serialização comparados. |
| `RegistryKey<T>` / `TagKey<T>` | `ResourceKey<T>` / `TagKey<T>` | Igualdade observável e pertencimento a registry correspondem. Java usa interning fraco; Asteria usa structs/records com igualdade por valor, preservando os contratos observáveis usados neste escopo. |
| `HolderLookup`, providers e reload | `HolderLookup`, `HolderGetter`, `MappedRegistry.PendingTags` | Lookups tipados, delegates, filtros de elementos e snapshots de reload comparados. |
| `BlockProperties` / `Block` | `BlockBehaviour.Properties` / `Block` | Defaults e setters modelados comparados, incluindo `NoCollision`, `NoOcclusion`, resistência, fricção, random ticks, replaceability, lava e flags de estado. |
| `BlockState` / `StateDefinition` | `StateHolder` / `StateDefinition` | Ordenação de propriedades, produto cartesiano, vizinhos pré-computados, identidade de propriedades, parsing/valores e transições comparados. |
| `BlockStateIdMap` | `IdMapper<BlockState>` | IDs por identidade, mappings esparsos, enumeração e capacidade inicial dos dois índices comparados. |
| `CrudeIncrementalIntIdentityHashBiMap<T>` | `CrudeIncrementalIntIdentityHashBiMap<T>` | Hash mixer, sondagem, crescimento, carga, mapeamento esparso e cópia comparados com a implementação 26.3. |
| `PalettedContainerStrategy<T>` | `Strategy<T>` / `Configuration` | Dimensões, ordem X/Z/Y, larguras e transições single/local/global comparadas. |
| `SingleValuePalette`, `LinearPalette`, `HashMapPalette`, `GlobalPalette` | Paletas homônimas | Alocação, lookup, overflow, promoção e identidade comparados. |
| `PalettedContainer<T>` | `PalettedContainer<T>` | Resize, remapeamento de IDs, cópia, contagem, lookup e publicação do snapshot comparados. O cache do último ID antigo durante resize também existe na referência 26.3. |
| `PackedBitStorage` | `SimpleBitStorage` | Packing por palavra, limite sem cruzamento de palavra, tabela de divisão, get/set/fill/getAll/unpack e raw data comparados. |
| `ZeroBitStorage` | `ZeroBitStorage` | Semântica de zero bits, validação de índice/valor, unpack e cópia comparados. |
| `ThreadingDetector` | `ThreadingDetector` | Protocolo de exclusão de escritor e detecção de concorrência equivalentes. A representação do erro é adaptada: Asteria lança `InvalidOperationException`; o jogo usa `ReportedException` com `CrashReport` e logging. |
| `StateHolder<TOwner, TState>` / `StateDefinition<TOwner, TState>` | `StateHolder` / `StateDefinition` | Lookup por identidade, array de valores, produto cartesiano, vizinhos pré-computados e builder são genéricos e compartilhados por `BlockState` e `FluidState`. |
| `Fluid` / `FluidState` | `Fluid` / `FluidState` | Parcial: registry, holder intrusivo, definição/estado padrão, estado vazio, ID map, source/empty/amount/own-height/random-ticking e `BlockState.GetFluidState`. `FlowingFluid`, água/lava, consultas de mundo, formas, fluxo, ticks e interação ainda não foram portados. |
| `Direction` / `Direction.Axis` / `Plane` / `FrontAndTop` / `AttachFace` | `Direction` / `FrontAndTop` / `AttachFace` | Parcial: seis direções, vetores, IDs, eixo, plano, yaw, nearest-direction, `FrontAndTop`, `AttachFace` e 48 transformações de `OctahedralGroup`. APIs dependentes de entidade, RNG, codecs locais e integração com renderer permanecem pendentes. O modelo C# usa objetos singleton por identidade onde os membros carregam dados. |
| `Position` / `Vec3` | `Position` / `Vec3` | Parcial: factories por `Vec3i`, aritmética, produto escalar/vetorial, distância, normalização, rotações com tabela Mth, alinhamento, projeção, igualdade/hash e finitude. Codec para persistência local, RNG, `Vec2` e JOML permanecem pendentes; codecs de rede não são alvo. `Position` tem nomes de acesso adaptados ao C#. |
| `AABB` / `HitResult` / `BlockHitResult` | `AABB` / `HitResult` / `BlockHitResult` | Parcial: AABB cobre normalização, operações vetoriais, clipping de segmento, colisão e medidas. `HitResult`/`BlockHitResult` agora fornecem o resultado de raycast, `MISS`, `inside`, direção, posição e sinalizador de world border. Sobrecargas específicas de JOML/BoundingBox e integração com entidades ainda pendentes. |
| `DiscreteVoxelShape` / `BitSetDiscreteVoxelShape` | Shapes homônimas | Parcial: grade/bitset X/Y/Z, bounds em cache, `isFullWide`, `firstFull`/`lastFull`, cópia, região preenchida, interior, rotação por `OctahedralGroup`, enumeração greedy de caixas, arestas com mesclagem e faces por transição de ocupação. As operações discretas centrais estão portadas; falta a integração com block states, mundo e renderer. |
| `VoxelShape` / `CubeVoxelShape` / `ArrayVoxelShape` / `SliceShape` / `SubShape` | Tipos homônimos | Parcial: coordenadas uniformes/explicitas, min/max, bounds, move, optimize, caixas/AABBs, arestas contínuas, closest-point, queries transversais, cache das seis faces, slices/subshapes, clipping para `BlockHitResult` e colisão swept-AABB por eixo. Igualdade de ocupação estática via `Shapes.Equal`; oclusão é feita pela álgebra de slices de `Shapes`. Contextos e integração com mundo/rendering ainda pendentes. |
| `Shapes` | `Shapes` | Parcial: factories e álgebra booleana; `Equal`; rotação por `OctahedralGroup` com centro arbitrário; tabelas de rotação horizontal, all-axis e attach-face; colisão agregada; `BlockOccludes`, `MergedFaceOccludes` e `FaceShapeOccludes`. A integração com block-state occlusion/sturdiness e renderer continua pendente. |
| `BooleanOp` / `IndexMerger` | Tipos homônimos | As 16 funções booleanas e os quatro mergers foram portados: identical, non-overlapping, indirect e discrete-cube. Seleção com tolerância `1e-7` e teto de custo 256 preservada. |


| `LegacyRandomSource` / `WorldgenRandom` / `GradientNoise` / `SimplexNoise` | Tipos homônimos | Parcial: LCG de 48 bits, adaptador de worldgen, tabela de permutação, gradientes, Simplex 2D e NoiseStack 2D cobrem os ruídos usados pelos efeitos e temperatura dos biomas. APIs adicionais de RNG, Simplex 3D e empilhamento 3D permanecem pendentes. |
| `Mth` | `Mth` | Parcial: tabela trigonométrica SIN de 65536 entradas, `Sin`, `Cos`, `Floor`, `Lerp`, `Square`, `LengthSquared` e `Length`, cobrindo a dependência portável de `Direction`/`Vec3`. O restante da utilitária numérica não foi introduzido. |
| `Heightmap` / `HeightmapType` | `Heightmap` / `Heightmap.Types` | Seis variantes com IDs, nomes serializados, envio ao cliente, retenção após worldgen e predicados por tags/fluidos; priming compartilhado, atualização incremental e fallback para dados incompatíveis. Persistência por codecs ainda ausente. |
| `Chunk` | `ChunkAccess` / `ProtoChunk` / `LevelChunk` | Parcial: posições, seções, lookup de blocos e heightmaps. Ainda não há base abstrata `ChunkAccess`, subclasses com lifecycle separado, block entities, estruturas, biomas, ticks, iluminação ou serialização. |
| `Biome` / `BiomeResolver` | `Biome` / `BiomeResolver` | Parcial: registry default e holders para Plains/Desert, clima, resolver quart e palette storage. BiomeSpecialEffects cobre cores e modificadores; BIOME_INFO_NOISE seed 2345, temperatura FROZEN seed 3456, correção por altitude seed 1234, precipitação e cache térmico por thread estão portados. Núcleo de atributos ambientais tipados (boolean/float, ranges, modificadores e mapa imutável) portado, ainda sem registro built-in ou integração com dados de bioma. Spawn settings, generation settings/features/carvers, verificações dependentes de nível e codecs locais ainda pendentes. |
| `ChunkSection` | `LevelChunkSection` | Parcial: palettes de estados de bloco e `Holder<Biome>`; biome palette 4³ com dois bits por eixo, `GetNoiseBiome`, fill via `BiomeResolver`, cópia independente; contadores non-air/fluid/ticking e lookup de fluido. Packet IO foi omitido por escopo offline; codec de save/load local ainda pendente. |
| `Main.cs`, cena e configuração Godot | Host de execução do jogo | Não há equivalente de gameplay direto. É atualmente um bootstrap/smoke test sobre uma cena Node3D vazia, não um loop de jogo. |

## Divergência encontrada e corrigida

### Diagnóstico de ID ausente com valor nulo

O C# interpolava `null` como string vazia no diagnóstico de `IIdMap.GetIdOrThrow(null)`, enquanto a concatenação de string Java de `IdMap.getIdOrThrow` produz `null`.

Correção aplicada:
- `src/Asteria/Content/Registries/IIdMap.cs`: normaliza explicitamente o valor nulo para o texto `null`.
- `tests/BlockRegistryChecks/Program.cs`: teste agora espera `Can't find id for 'null' in map ...`.

Commit: [`968aa70`](https://github.com/sarakborges/asteria2/commit/968aa709c4c20e440b5af246044056b0c0d3a822).

## Sistemas ainda ausentes — não são bugs de paridade dos módulos atuais

A base atual não implementa ainda os sistemas completos abaixo. Eles devem ser tratados como trabalho futuro e só podem ser comparados em profundidade depois de existir o modelo equivalente em Asteria.

- Mundo/Level, dimensões, chunk source/manager, carregamento e lifecycle de chunks.
- Geração de terreno, noise settings, carvers, features, estruturas e spawn.
- `FlowingFluid`, água/lava, propagação, geometria, ticks e interação com o mundo; os contadores de fluidos em seções e o núcleo `FluidState` já foram portados parcialmente.
- Biome effects, environmental attributes, mob spawn settings, biome generation settings/features/carvers, temperatura dependente de posição/noise e codec de save/load local.
- Lighting engine, sky/block light e propagação.
- Integração de block shapes, colisão e raycast com o mundo local, contextos de colisão, física e rendering local.
- Renderização de blocos, culling, meshing, materiais e atlas.
- Tick scheduler local, block ticks, fluid ticks e random tick loop permanecem necessários para simulação single-player.
- Redstone (power propagation, wires, repeaters, comparators e circuit updates) é explicitamente fora de escopo.
- Entidades, AI, jogadores, itens, inventário, crafting, menus e drops.
- Áudio/partículas em runtime, comandos e save/load local ainda não foram implementados.
- Networking/protocol, client/server, multiplayer e sincronização remota são explicitamente fora de escopo.
- Data packs/resource packs, codecs, NBT e serialização em disco para save/load local.
- Codecs de protocolo, rede e sincronização remota estão fora de escopo.

O `ChunkSection` já possui os contadores de fluido correspondentes à referência. Não deve ganhar armazenamento ou APIs de bioma fictícios antes da criação do modelo e do container reais.

## Lacunas de integração e manutenção

1. **Conteúdo de teste no runtime (resolvido em 21/11/2026):** `TEST_BLOCK` e suas propriedades agora existem somente no harness de testes. O catálogo de runtime registra apenas `AIR`, e `Main._Ready` inicializa os registries e valida o bloco padrão, sem executar assertions de fixtures. O loop de jogo continua sendo trabalho futuro.
2. **Relatórios de concorrência:** o detector preserva o protocolo de exclusão, mas precisa de uma camada de crash report/logging Asteria para reproduzir a forma de diagnóstico do jogo.
3. **Persistência e rede:** as paletas têm a lógica em memória, mas ainda não têm codecs nem leitura/escrita de buffer/arquivo. Isso é uma etapa de integração, não uma falha nas transições de paleta implementadas.
4. **Testes:** o harness cobre as áreas atuais, mas está concentrado num único `Program.cs`. Separar por subsistema é uma melhoria de manutenção a considerar quando o conjunto de testes crescer.

## Validação desta baseline

- Workflow [Block registry — run #382](https://github.com/sarakborges/asteria2/actions/runs/44937120010), commit `db6623c`: harness e build Godot aprovados.
- A suíte cobre `AttachFace`, todas as tabelas de rotação, rotação das 48 simetrias, equivalência de shapes, face occlusion, swept collision e clipping para `BlockHitResult`.
- Nenhum merge para `main` ou deploy foi realizado.

## Fontes de referência consultadas

- [Identifier](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/resources/identifier/)
- [MappedRegistry](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/MappedRegistry/)
- [DefaultedMappedRegistry](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/DefaultedMappedRegistry/)
- [Holder](https://mc-packet-reference.netlify.app/26.x/source/src/main/java/net/minecraft/core/Holder.java)
- [HolderSet](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/holderset/)
- [HolderGetter](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/HolderGetter/)
- [ResourceKey](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/resources/ResourceKey/)
- [TagKey](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/tags/TagKey/)
- [IdMap](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/idmap/)
- [BlockBehaviour](https://mc-packet-reference.netlify.app/26.x/source/src/main/java/net/minecraft/world/level/block/state/BlockBehaviour.java)
- [StateHolder](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/StateHolder/)
- [StateDefinition](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/StateDefinition/)
- [Property](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/properties/Property/)
- [PalettedContainer](https://mc-packet-reference.netlify.app/26.x/source/src/main/java/net/minecraft/world/level/chunk/PalettedContainer.java)
- [Strategy](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/Strategy/)
- [Configuration](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/configuration/)
- [SimpleBitStorage](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/util/SimpleBitStorage/)
- [ZeroBitStorage](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/util/zerobitstorage/)
- [LevelChunkSection](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/LevelChunkSection/)
- [AABB](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/AABB.html)
- [HitResult](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/HitResult.html)
- [BlockHitResult](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/BlockHitResult.html)
- [AttachFace](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/level/block/state/properties/AttachFace.html)
- [Shapes](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/shapes/)
- [VoxelShape](https://aldak0.ru/javadoc/26.3.x/net/minecraft/world/phys/shapes/VoxelShape.html)
- [ChunkAccess](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/ChunkAccess/)
- [Biome](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biome/)\n- [BiomeSpecialEffects](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/biomespecialeffects/)\n- [LegacyRandomSource](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/legacyrandomsource/)\n- [WorldgenRandom](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/worldgenrandom/)\n- [GradientNoise](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/synth/gradientnoise/)\n- [SimplexNoise](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/synth/simplexnoise/)\n- [NoiseStack](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/synth/noisestack/)\n- [Heightmap](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/Heightmap/)
- [OctahedralGroup](https://mc-packet-reference.netlify.app/26.x/source/com/mojang/math/OctahedralGroup/)
- [SymmetricGroup3](https://mc-packet-reference.netlify.app/26.x/source/com/mojang/math/SymmetricGroup3/)
- [FrontAndTop](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/FrontAndTop/)
- [VoxelShape](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/VoxelShape/)
- [SubShape](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/SubShape/)
- [SliceShape](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/phys/shapes/SliceShape/)
- [StateHolder](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/StateHolder/)
- [FluidState](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/FluidState/)
- [Fluids](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/Fluids/)
- [EmptyFluid](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/material/EmptyFluid/)


## Follow-up — isolamento de fixtures (21/11/2026)

A contaminação de conteúdo identificada na baseline foi removida:
- `src/Asteria/Content/Blocks/Blocks.cs` registra somente `AIR`.
- O bloco com propriedades `powered` e `stage` foi movido para uma fixture privada no harness de testes; ele continua cobrindo estados, IDs globais, paletas e seções sem entrar no catálogo de runtime.
- Os testes agora verificam explicitamente que o registry de blocos de produção contém somente o conteúdo definido por `Blocks`.
- `scripts/Main.cs` limita-se ao bootstrap de conteúdo e validação do bloco `AIR`; ainda não existe loop de jogo.

A separação segue o princípio de manter fixtures de teste fora do catálogo de conteúdo carregado pelo jogo. Não cria APIs ou modelos de gameplay antes de existirem seus domínios correspondentes.


## Incremento — ruído de informação dos biomas

- Implementados `LegacyRandomSource` (LCG de 48 bits), `WorldgenRandom`, `GradientNoise` e a amostragem 2D de `SimplexNoise`.
- `BiomeInfoNoise` inicializa a cadeia com seed fixa 2345 e descarta o offset espacial, preservando os draws aleatórios de inicialização.
- O modificador SWAMP agora usa o ruído real com coordenadas escaladas por 0.0225; o sampler injetado foi removido.
- Testes incluem amostras positivas e negativas, cores resultantes e aplicação do override antes do modificador.
- A amostragem 3D do Simplex e APIs gerais adicionais de RNG não foram portadas neste incremento.


## Incremento — temperatura e precipitação dos biomas

- Portado o NoiseStack 2D com acumulação float, frequências e amplitudes por camada.
- Implementado o modificador FROZEN com três SimplexNoise sequenciais a partir da seed 3456, combinado com BIOME_INFO_NOISE nas escalas 0.2 e 0.09.
- Implementada a correção de temperatura por altitude com SimplexNoise seed 1234 acima de seaLevel + 17.
- Implementados os predicados de chuva/neve e o limiar de derretimento de iceberg.
- Cache térmico FIFO de 1024 posições por thread, indexado pela posição compactada, está implementado. Verificações dependentes de nível/iluminação permanecem pendentes.


## Incremento — núcleo de atributos ambientais tipados

- AttributeRange<T>: validação e sanitização genéricas, incluindo os intervalos float unitário, unitário com epsilon e não negativo.
- LerpFunction<T>: interpolação tipada e os quatro limiares distintos de transição para tipos sem interpolação.
- AttributeType<T>: bibliotecas de modificadores permitidos, quatro políticas de interpolação e projeções numéricas opcionais.
- EnvironmentAttribute<T>: valor padrão, range e flags de sincronização, posicionamento e resolução de biomas.
- EnvironmentAttributeMap: entradas tipadas, composição por modificador, snapshot imutável, Set como override e PutAll com substituição pela última entrada.
- Bibliotecas booleanas (AND/NAND/OR/NOR/XOR/XNOR) e float (alpha blend/add/subtract/multiply/minimum/maximum).
- Escopo: runtime tipado boolean/float. Registro de tipos/atributos built-in, codecs locais, outros domínios de valor e integração do Biome com o mapa ainda pendentes.
