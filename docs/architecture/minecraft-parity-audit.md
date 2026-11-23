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
| `BlockProperties` / `Block` | `BlockBehaviour.Properties` / `Block` | Defaults e setters locais modelados comparados, incluindo `NoCollision`, `NoOcclusion`, resistência, fricção, random ticks, replaceability, lava, piston reaction, seleção de map color e predicates genéricos de estado. `GetDefaultMapColor` usa o estado default atual. APIs de redstone/conductor não são expostas porque redstone está fora do escopo. |
| `MapColor` | `MapColor` | Os 62 registros, IDs, cores RGB, slots vazios 62/63, quatro brilhos, cálculo ARGB com escala inteira e codificação/decodificação do ID compactado são cobertos por testes de limites e valores. Cada `BlockState` armazena a cor calculada por sua definição de propriedades no momento da construção. |
| `BlockState` / `StateDefinition` | `StateHolder` / `StateDefinition` | Ordenação de propriedades, produto cartesiano, vizinhos pré-computados, identidade de propriedades, parsing/valores e transições comparados; getters genéricos de colisão, luz, map color, suffocation, view blocking e post-process permanecem no escopo. APIs de emissão/condução de sinais, conexão de fios e níveis de redstone foram removidas da implementação local. |
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
| `Biome` / `BiomeResolver` | `Biome` / `BiomeResolver` | Parcial: registry default e holders para Plains/Desert, clima, resolver quart e palette storage. BiomeSpecialEffects cobre cores e modificadores; BIOME_INFO_NOISE seed 2345, temperatura FROZEN seed 3456, correção por altitude seed 1234, precipitação e cache térmico por thread estão portados. Núcleo de atributos ambientais tipados portado: registries environment_attribute/attribute_type, boolean/float/angle_degrees/RGB/ARGB/integer/moon_phase e 19 definições genéricas dentro do escopo; cada Biome agora armazena um EnvironmentAttributeMap imutável e o expõe por GetAttributes; a resolução/layering das fontes ambientais no mundo ainda está pendente. O cross-fade genérico de listas foi portado; os tipos PARTICLE/AMBIENT_PARTICLES usam codecs locais e os tipos de partículas genéricos mantidos no escopo. ACTIVITY aguarda o registry e o domínio de schedules/AI; definições de atividade específicas de villagers não fazem parte do escopo atual. Áudio, bed rules e atributos de blocos/entidades específicos estão fora do escopo. Spawn settings, generation settings/features/carvers, verificações dependentes de nível e codecs locais ainda pendentes. |
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
- Resolução/layering de atributos ambientais entre dimensão, bioma, timeline e clima; o mapa imutável por bioma já está integrado, mas ainda não há resolvedor ambiental do mundo. Mob spawn settings, biome generation settings/features/carvers, verificações dependentes de nível e codec de save/load local também permanecem pendentes.
- Lighting engine, sky/block light e propagação.
- Integração de block shapes, colisão e raycast com o mundo local, contextos de colisão, física e rendering local.
- Renderização de blocos, culling, meshing, materiais e atlas.
- Tick scheduler local, block ticks, fluid ticks e random tick loop permanecem necessários para simulação single-player.
- Redstone (power propagation, wires, repeaters, comparators e circuit updates) é explicitamente fora de escopo.
- Entidades, AI, jogadores, itens, inventário, crafting, menus e drops.
- Partículas em runtime, comandos e save/load local ainda não foram implementados. Áudio está fora do escopo.
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


## Incremento — registry de tipos de atributos

- Adicionado o registry simples attribute_type ao root de built-in registries, seguindo a ordem de bootstrap e o freeze do restante do catálogo.
- Registradas as políticas BOOLEAN e FLOAT com os identificadores asteria:boolean e asteria:float.
- A interface IAttributeType mantém a fronteira de registry sem apagar o tipo genérico dos objetos AttributeType<T>; o consumidor recupera as políticas tipadas originais por identidade.
- O catálogo contém boolean, float e angle_degrees. Tipos de cor, inteiro, moon phase, partículas, listas e activities aguardam os respectivos modelos e codecs. Áudio, bed rules e atributos específicos de blocos/entidades foram excluídos do produto.


## Incremento — registries e definições ambientais built-in

- O root agora registra environment_attribute antes de attribute_type, conforme a ordem de 26.3.
- AttributeTypes registra boolean, tri_state, float e angle_degrees. TriState e o wrap angular do partial tick foram portados com seus thresholds.
- 21 definições ambientais genéricas que usam os três tipos mantidos foram registradas com defaults, ranges e flags correspondentes. Atributos de áudio, bed rules e de blocos/entidades específicos não fazem parte do produto.
- Tipos dependentes de valores ainda ausentes foram omitidos; IDs numéricos após essas lacunas não são considerados equivalentes. Lookup por chave é validado.
- EnvironmentAttributeMap ainda não está ligado ao Biome; codecs locais permanecem pendentes. Multiplayer e sincronização remota continuam fora de escopo.


## Incremento — cross-fade genérico de listas (21/11/2026)

- `LerpFunction<T>.OfListCrossFade` reproduz `LerpFunction.ofListCrossFade`: endpoints retornam a lista original; no intervalo, percorre a lista anterior com peso `1 - alpha` e a nova com peso `alpha`, mantendo a concatenação e a ordem.
- O resultado intermediário é um snapshot somente leitura. Não há clamp, ordenação ou deduplicação adicional.
- `PARTICLE` e `AMBIENT_PARTICLES` estavam pendentes nesta etapa inicial; ambos foram posteriormente implementados com `ParticleOptions`, `ParticleTypes.CODEC`, `AmbientParticle.CODEC` e codecs locais, conforme os incrementos registrados abaixo.
- `ACTIVITY` também não foi registrado: requer o registry de activities e o domínio de schedules/AI; não há definição de atividade genérica atualmente mantida no catálogo ambiental do produto.


## Incremento — modificador append de listas (21/11/2026)

- `ListModifier<T>.APPEND` reproduz os casos de identidade e concatenação do `ListModifier.append` de 26.3, preservando a ordem subject → argumento.
- O catálogo de modificadores de lista contém exclusivamente `append`.
- `AMBIENT_PARTICLES` foi posteriormente registrado com o modelo e codec concretos, sem placeholders.


## Incremento — AmbientParticle e codecs de atributo (21/11/2026)

- `AttributeType<T>` passou a armazenar `ValueCodec` opcional. Os tipos `particle` e `ambient_particles` possuem codecs concretos; os demais tipos de valor ainda requerem codecs específicos.
- Portado o modelo `AmbientParticle(particle, probability)`, com codec por record fields obrigatórios e range inclusivo [0, 1].
- `CanSpawn` consome uma amostra float de 24 bits e usa comparação inclusiva `<=`; testes cobrem a igualdade exata no limite, probabilidade zero/um e número de chamadas.
- `AMBIENT_PARTICLES` usa listas cross-fade escalando somente a probabilidade e o modifier `append`; o atributo genérico `visual/ambient_particles` está registrado com default vazio e é syncable.
- O registry `attribute_type` agora contém nove tipos no escopo: os sete anteriores, `particle` e `ambient_particles`.
- Após a revisão de conteúdo específico, o registry `particle_type` contém 46 tipos reutilizáveis: 33 simples e 13 com opções genéricas (coloridas, spell, escalares, três tipos de partículas de bloco, ITEM, VIBRATION e TRAIL). `ItemStackTemplate` mantém apenas o sentinela `air`; patches de componentes não vazios ainda não são suportados. A lista global de itens permanece incompleta, então os IDs numéricos do registry de itens ainda não têm paridade total.
- Atributos específicos de blocos/objetos, áudio e regras de cama continuam excluídos. Stream codecs, buffers de pacote e multiplayer continuam excluídos pelo escopo offline.


## Incremento — opções de partículas coloridas (21/11/2026)

- Portada a opção genérica `ColorParticleOption`, que mantém o `ParticleType` e um ARGB empacotado. Os getters de canal devolvem frações normalizadas de 8 bits.
- O codec usa o campo obrigatório `color`: inteiro ARGB como forma principal e vetor RGBA de quatro floats como alternativa. A codificação usa sempre o formato inteiro principal.
- Registrados `entity_effect`, `tinted_leaves` e `flash` nas posições relativas da declaração 26.3, todos com `overrideLimiter=false`.
- Contagem local: 106 tipos sem payload + 3 tipos com opção colorida. Tipos especializados restantes continuam pendentes de seus modelos próprios; os IDs numéricos ainda não têm paridade com o registro completo.


## Incremento — SpellParticleOption (21/11/2026)

- Portado `SpellParticleOption` para `effect` e `instant_effect`, com `ParticleType`, cor RGB empacotada e `power`.
- Codec de registro mantém defaults da referência: `color=-1` e `power=1.0`. Campos ausentes recebem defaults; ao codificar, valores iguais aos defaults são omitidos. Campo presente inválido não cai silenciosamente no default.
- `RGB_COLOR_CODEC` usa inteiro como representação primária e vetor RGB de três floats como alternativa. Os getters de canal retornam componentes RGB normalizados.
- Os dois tipos estão registrados nas posições relativas da declaração 26.3, com `overrideLimiter=false`. Contagem atual: 106 simples + 3 coloridos + 2 spell.


## Incremento — PowerParticleOption e SculkChargeParticleOptions (21/11/2026)

- Portado `PowerParticleOption` para `dragon_breath`: `power` é um campo opcional com default `1.0`; o default é omitido durante a codificação e um valor presente inválido causa erro. `overrideLimiter=false`.
- Portado `SculkChargeParticleOptions` como record com campo obrigatório `roll`; o tipo associado é o singleton registrado `sculk_charge`, com `overrideLimiter=true`.
- Os dois registros ocupam suas posições relativas na ordem built-in 26.3. Contagem de tipos agora é 113: 106 simples, 3 coloridos, 2 spell e 2 opções numéricas.
- Os contratos de buffer e stream codecs permanecem excluídos por escopo offline.


## Incremento — ShriekParticleOption (21/11/2026)

- Portado `ShriekParticleOption` com o único campo obrigatório `delay`, tipado como Int32; a representação do record é o mapa `{delay: integer}`.
- O codec rejeita campo ausente ou não inteiro e preserva valores assinados sem impor limites extras.
- Registrado `shriek` imediatamente após `scrape` e antes de `egg_crack`, com `overrideLimiter=false`, conforme a ordem de declaração 26.3.
- Contagem atual do registry local: 114 tipos (106 simples, 3 coloridos, 2 spell e 3 opções numéricas). O stream codec VarInt permanece excluído por escopo offline.


## Incremento — TrailParticleOption e codecs vetoriais de três campos (21/11/2026)

- Portado `RecordCodecBuilder.Group3`, mantendo a composição dos três resultados de campo e a acumulação de erros/parciais.
- `Vec3.CODEC` usa exatamente uma lista de três `double` em ordem X/Y/Z. Listas curtas falham sem parcial; listas longas falham carregando os três primeiros componentes como resultado parcial.
- Adicionado `ExtraCodecs.POSITIVE_INT`, que aceita somente o intervalo Int32 de 1 a `int.MaxValue` e preserva a mensagem de erro da referência.
- Portado `TrailParticleOption(target, color, duration)`: campos obrigatórios `target` (Vec3), `color` (RGB inteiro, com vetor RGB como alternativa) e `duration` (inteiro positivo).
- Registrado `trail` com `overrideLimiter=false`. A ordem local mantém `instant_effect < trail < pause_mob_growth`; os tipos de payload `item` e `vibration`, que antecedem `trail` na referência, ainda não foram portados. Portanto, os IDs numéricos do registro continuam sem paridade completa.
- O stream codec de `Vec3` e o stream codec de `TrailParticleOption` permanecem fora do escopo single-player/offline.

## Incremento — `DustParticleOptions` e `DustColorTransitionOptions` (21/11/2026)

- Portada a base `ScalableParticleOptionsBase`, compartilhada pelos dois tipos: o construtor limita `scale` a [0,01; 4,0], enquanto o codec rejeita valores fora do intervalo com a mensagem de validação da referência.
- `DustParticleOptions` usa os campos obrigatórios `color` (RGB inteiro ou vetor RGB alternativo) e `scale`; inclui a cor padrão `REDSTONE` e retorna os canais RGB normalizados.
- `DustColorTransitionOptions` usa `from_color`, `to_color` e `scale`, todos obrigatórios; inclui `SCULK_PARTICLE_COLOR`, `SCULK_TO_REDSTONE` e getters separados para as duas cores.
- Registrados `dust` e `dust_color_transition` imediatamente após `falling_water` e antes de `effect`, ambos com `overrideLimiter=false`, preservando a ordem relativa da referência.
- O registry local passa a conter 117 tipos: 106 simples, 3 coloridos, 2 spell, 3 numéricos, 1 record e 2 opções com escala.
- Testes cobrem round-trip, alternativas vetoriais RGB, campos obrigatórios, limites do codec, clamp do construtor, cores padrão e ordem de registro.
- Stream codecs continuam omitidos por decisão de produto single-player/offline. A ausência de `item`, `vibration` e tipos de bloco/objeto fora do escopo ainda impede paridade dos IDs numéricos globais.

### Fontes específicas

- [DustParticleOptions](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/dustparticleoptions/)
- [DustColorTransitionOptions](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/dustcolortransitionoptions/)
- [ScalableParticleOptionsBase](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/scalableparticleoptionsbase/)
- [ParticleTypes](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/particletypes/)

## Incremento — `BlockParticleOption` e `BlockState.CODEC` (21/11/2026)

- Portado o modelo `BlockParticleOption(type, state)` e o codec de mapa com campo obrigatório `block_state`. Não inclui posição de spawn, que não faz parte do tipo vanilla 26.3.
- `BlockState.FULL_CODEC` representa o estado como objeto com `id` e `properties`; `BlockState.CODEC` também aceita a forma compacta pelo ID do bloco para o estado default.
- O mapa `properties` aceita somente valores serializados válidos da definição de estado; propriedades ausentes mantêm o default e valores iguais ao default são omitidos na codificação. Chaves desconhecidas são ignoradas.
- Registrados `block` (`overrideLimiter=false`) e `block_marker` (`true`) após `angry_villager`; `falling_dust` (`false`) após `sonic_boom`, conforme a ordem de declaração da referência.
- O registry local passa a conter 120 tipos: 106 simples, 3 coloridos, 2 spell, 3 numéricos, 1 record, 2 escalares e 3 tipos de partículas de bloco.
- Testes cobrem os três discriminadores, round-trip, forma compacta e completa do estado, defaults de propriedades, rejeição de campos/valores inválidos, flags e ordem de registro.
- O codec usa registry local de blocos; stream codecs e buffers continuam fora do escopo single-player/offline.

### Fontes específicas

- [BlockParticleOption](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/blockparticleoption/)
- [BlockState](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/blockstate/)
- [StateHolder](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/stateholder/)
- [StateDefinition](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/block/state/statedefinition/)

## Incremento — `ItemParticleOption` e modelo de template de item (21/11/2026)

- Criado o registry defaulted/intrusive `item`, com `air` como default e holders associados à identidade dos objetos. O catálogo mantido por enquanto contém somente `air` e `stick`; não é uma importação da lista completa de `Items`.
- `Item.CODEC` resolve holders pelo nome registrado e rejeita explicitamente `asteria:air`, sem usar o fallback do `DefaultedRegistry` para IDs desconhecidos.
- Portado `ItemStackTemplate` com holder do item, `count` opcional de 1 a 99 (default 1), `components` opcional (default vazio) e alternativa compacta que aceita somente o identificador do item.
- Portado `ItemParticleOption` com campo obrigatório `item`, mantendo o `ParticleType` associado; registrado `item` com `overrideLimiter=false` entre `instant_effect` e `trail`.
- `DataComponentPatch` atualmente suporta apenas o patch vazio. Entradas de componentes não vazias são rejeitadas porque `DataComponentType`, seu registry e os codecs dos tipos de componentes ainda não foram portados; não é declarado suporte genérico de componentes.
- O registry de partículas passa a 121 tipos locais. `vibration` continua ausente e a lista parcial de itens impede paridade dos IDs numéricos do registry de itens.
- Stream codecs, criação/uso de `ItemStack` em gameplay, inventário, receitas e rede continuam fora deste incremento; o item foi portado apenas na fronteira de identidade/codec exigida pela partícula.

### Fontes específicas

- [BuiltInRegistries](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/registries/builtinregistries/)
- [Item](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/item/item/)
- [ItemStackTemplate](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/item/itemstacktemplate/)
- [ItemParticleOption](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/itemparticleoption/)
- [ParticleTypes](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/particletypes/)

## Incremento — VibrationParticleOption e fontes de posição (21/11/2026)

- Criado o registry local `position_source_type`, com o tipo `block` registrado. O modelo de dispatch serializa o discriminador `type`.
- Portado `BlockPos.CODEC`: exatamente três inteiros assinados em X/Y/Z; listas curtas ou longas falham sem valor parcial. O codec preserva lifecycle `stable` tanto na leitura quanto na escrita.
- Portado `BlockPositionSource`: campo obrigatório `pos` e resolução para o centro geométrico do bloco.
- Portado `VibrationParticleOption(destination, arrival_in_ticks)`: ambos os campos são obrigatórios; `arrival_in_ticks` usa Int32 sem restrição adicional de sinal.
- O codec seguro da vibração rejeita fontes que não sejam de bloco com a mensagem da referência `Entity position sources are not allowed`. O tipo `entity` e `EntityPositionSource` continuam ausentes porque o domínio de entidades ainda não foi portado; o discriminador não registrado também é rejeitado.
- `vibration` foi registrado com `overrideLimiter=false`, entre `item` e `trail`. O registry local de partículas passa a 122 tipos.
- Não foram adicionados stream codecs, buffers de pacote ou dependências de rede. A API de posição de bloco não depende de `Level`; a obtenção de posição dependente de entidade permanece pendente do domínio correspondente.

### Fontes específicas

- [VibrationParticleOption](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/vibrationparticleoption/)
- [PositionSource](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/gameevent/positionsource/)
- [PositionSourceType](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/gameevent/positionsourcetype/)
- [BlockPositionSource](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/gameevent/blockpositionsource/)
- [BlockPos](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/blockpos/)
- [BuiltInRegistries](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/registries/builtinregistries/)
- [ParticleTypes](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/core/particles/particletypes/)



## Revisão de conteúdo exclusivo de objetos (21/11/2026)

- Removidas 74 entradas de partículas cuja finalidade é exclusiva de itens, blocos, entidades ou mecânicas específicas fora do escopo, incluindo partículas de illagers/raids, mobs nomeados, blocos e itens individuais, sulfur/copper, sculk, dripstone, campfire, honey, trial spawners e vaults. Geyser e suas partículas são excluídos pela mesma regra de conteúdo exclusivo de um objeto; nenhum desses registros existia nesta branch.
- Removidos os payloads especializados `PowerParticleOption`, `SculkChargeParticleOptions` e `ShriekParticleOption`, que só serviam às entradas excluídas.
- Mantidos os modelos e codecs genéricos `BlockParticleOption`, `ItemParticleOption`, `ColorParticleOption`, `SpellParticleOption`, `DustParticleOptions`, `DustColorTransitionOptions`, `VibrationParticleOption` e `TrailParticleOption`. Presets de cor REDSTONE/SCULK foram removidos, mas os modelos RGB parametrizados permanecem.
- O registry de itens agora contém somente `air` como sentinela; `stick` não é conteúdo de Asteria. `AIR`, `EMPTY` e outros sentinelas estruturais continuam válidos.
- Removidas as definições ambientais `can_start_raid`, `surface_slime_spawn_chance` e `can_pillager_patrol_spawn`. O registry ambiental contém 19 definições genéricas.
- O catálogo local de partículas agora tem 46 tipos. Os IDs globais continuam deliberadamente diferentes da tabela Minecraft completa porque as entradas específicas excluídas não são requisito de produto.


## Incremento — varredura residual de conteúdo específico (21/11/2026)

- Revisados os arquivos C# de `src/Asteria`: catálogos e registries em Content, definições de World e as camadas genéricas Core, Serialization e Util. Não foram encontrados outros registros de item, bloco, entidade, mecânica ou efeito exclusivos de um único objeto além dos itens já excluídos nesta revisão.
- Excluídos `pause_mob_growth` e `reset_mob_growth`: são emitidos pelo uso do item Golden Dandelion para pausar e retomar o crescimento de mobs. Não são partículas genéricas de crescimento; dependem desse item específico.
- O catálogo de partículas agora contém 46 tipos reutilizáveis: 33 simples e 13 configurados por opções genéricas. Os testes verificam a ausência dos dois identificadores e preservam a ordem relativa dos registros restantes.
- Permanecem no escopo as famílias genéricas: partículas parametrizadas de bloco e item, folhas, fluidos e efeitos de entidade; as sentinelas `air` e `empty`; tags de famílias de blocos; definições/clima de biomas; identificadores de famílias de fluidos ainda não registradas; atributos ambientais genéricos; e fontes de posição de bloco.


## Incremento — mapa ambiental armazenado em Biome (22/11/2026)

- `Biome` agora mantém um `EnvironmentAttributeMap` imutável e expõe o mesmo snapshot por `GetAttributes()`.
- O construtor existente continua compatível e usa `EnvironmentAttributeMap.Empty`; um overload recebe explicitamente o mapa, como no modelo 26.3.
- A integração não registra atributos ligados a objetos individuais nem adiciona conteúdo de áudio, cama, raid/illager ou entidades.
- Testes usam uma registry fixture isolada para verificar lifecycle do holder, identidade do mapa, modificador float, override booleano, ausência de overrides e isolamento da registry de biomas de produção.
- Resolução efetiva de atributos por fontes de dimensão/bioma/timeline/clima, codecs e o `BiomeBuilder` completo dependente de `BiomeGenerationSettings` permanecem pendentes; nenhum placeholder foi criado.
