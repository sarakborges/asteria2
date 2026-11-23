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
| `BlockProperties` / `Block` | `BlockBehaviour.Properties` / `Block` | Defaults e setters locais modelados comparados, incluindo `NoCollision`, `NoOcclusion`, resistência, fricção, random ticks, replaceability, lava, piston reaction, seleção de map color e predicates genéricos de estado. `GetDefaultMapColor` usa o estado default atual; `GetDefaultDestroyTime` lê diretamente o builder retido, sem confundir esse valor com o snapshot `BlockState.DestroySpeed`. O predicado genérico `IsRedstoneConductor` está exposto como contrato do estado; APIs de sinais, fios e circuitos continuam fora do escopo. |
| `MapColor` | `MapColor` | Os 62 registros, IDs, cores RGB, slots vazios 62/63, quatro brilhos, cálculo ARGB com escala inteira e codificação/decodificação do ID compactado são cobertos por testes de limites e valores. Cada `BlockState` armazena a cor calculada por sua definição de propriedades no momento da construção. |
| `BlockState` / `StateDefinition` | `StateHolder` / `StateDefinition` | Ordenação de propriedades, produto cartesiano, vizinhos pré-computados, identidade de propriedades, parsing/valores e transições comparados; getters genéricos de colisão, luz, map color, suffocation, view blocking e post-process permanecem no escopo; o view-blocking default preserva o closure do vanilla sobre o builder mutável de propriedades; o estado fluido inicial é `Fluids.EMPTY` até `initCache()` substituir o valor pelo fluido do bloco.  `IsSolid` preserva a ordem de decisão legada de 26.3, incluindo `forceSolidOn/off`, cache de colisão e os limites geométricos, distinto de `IsSolidRender`; o `initCache()` reexecuta e recalcula os valores derivados em cada chamada, sem trava de idempotência, como em 26.3; o cache de colisão estático rejeita formas não vazias combinadas com função de deslocamento quando `DynamicShape` não está habilitado; `SolidDebugger` mantém motivos, ordinais, mensagens, primeira razão por holder, usando `Holder<Block>` registrado e `GetRegisteredName()`, e dump de diagnóstico. As adaptações operacionais documentadas são `ScopedValue` → `AsyncLocal`, enum Java com campos → singletons imutáveis com ordinal, logging → `System.Diagnostics.Trace` e propriedades de sistema → variáveis de ambiente .NET. `BlockState.Is(tag, predicate)` preserva membership seguida de predicate com short-circuit, como em 26.3. `PathComputationType` (`LAND/WATER/AIR`), `isPathfindable` e `skipRendering` seguem os contratos base de 26.3 e são cobertos por testes de delegação e comportamento padrão. `canBeReplaced(Fluid)` também segue o predicado legado `replaceable || !isSolid`; `getSeed(BlockPos)` delega ao `Mth` equivalente. A emissão de sinais, conexão de fios e níveis/propagação de redstone permanecem fora do escopo; o predicado genérico `IsRedstoneConductor` é a única API relacionada a redstone mantida como trait de estado. |
| `BlockStateIdMap` | `IdMapper<BlockState>` | IDs por identidade, mappings esparsos, enumeração e capacidade inicial dos dois índices comparados. |
| `CrudeIncrementalIntIdentityHashBiMap<T>` | `CrudeIncrementalIntIdentityHashBiMap<T>` | Hash mixer, sondagem, crescimento, carga, mapeamento esparso e cópia comparados com a implementação 26.3. |
| `PalettedContainerStrategy<T>` | `Strategy<T>` / `Configuration` | Dimensões, ordem X/Z/Y, larguras e transições single/local/global comparadas. |
| `SingleValuePalette`, `LinearPalette`, `HashMapPalette`, `GlobalPalette` | Paletas homônimas | Alocação, lookup, overflow, promoção e identidade comparados. |
| `PalettedContainer<T>` | `PalettedContainer<T>` | Resize, remapeamento de IDs, cópia, contagem, lookup e publicação do snapshot comparados. O cache do último ID antigo durante resize também existe na referência 26.3. |
| `PackedBitStorage` | `SimpleBitStorage` | Packing por palavra, limite sem cruzamento de palavra, tabela de divisão, get/set/fill/getAll/unpack e raw data comparados. |
| `ZeroBitStorage` | `ZeroBitStorage` | Semântica de zero bits, validação de índice/valor, unpack e cópia comparados. |
| `ThreadingDetector` | `ThreadingDetector` | Protocolo de exclusão de escritor e detecção de concorrência equivalentes. A representação do erro é adaptada: Asteria lança `InvalidOperationException`; o jogo usa `ReportedException` com `CrashReport` e logging. |
| `StateHolder<TOwner, TState>` / `StateDefinition<TOwner, TState>` | `StateHolder` / `StateDefinition` | Lookup por identidade, array de valores, produto cartesiano, vizinhos pré-computados, builder e igualdade por identidade comparados. `Equals` é sealed e delega a `Object.Equals`; `GetHashCode` delega a `Object.GetHashCode` e permanece sobrescrevível, como em 26.3. O código é compartilhado por `BlockState` e `FluidState`. |
| `SupportType` / `BlockState.IsFaceSturdy` | `SupportType` / `BlockStateBase.isFaceSturdy` | As três regiões geométricas e a ordem `FULL/CENTER/RIGID` seguem 26.3. O harness verifica seis direções, shape vazio/full e consulta dinâmica sem cache estático. |
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
- Redstone (power propagation, wires, repeaters, comparators e circuit updates) é explicitamente fora de escopo. A exceção é o predicado estrutural `BlockState.IsRedstoneConductor`, sem implementação de sinal/propagação.
- As demais APIs `BlockStateBase` pendentes são agrupadas por dependência: sinais (`isSignalSource`, `hasAnalogOutputSignal`, `getOwnSignal`, `getSignal`, `getDirectSignal`, `getAnalogOutputSignal`, `shouldRedstoneWireConnectTo`) dependem do subsistema de redstone; spawn/colisão/ações de entidades e jogador, quebra, uso, cópia e drops dependem de entidades, jogador e itens; atualizações de vizinhança, sobrevivência, eventos e ticks dependem de `Level`/scheduler; ticker/menu e retenção de block entity dependem do modelo de block entities; som e instrumento dependem de áudio. Sobrecargas com `CollisionContext` e substituição por `BlockPlaceContext` também ficam pendentes até seus contextos existirem. Nenhum desses contratos foi preenchido com stubs.
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


## Incremento — ordem de avaliação do construtor de BlockState (23/11/2026)

- Ajustada a ordem de avaliação para corresponder ao construtor BlockStateBase de Minecraft 26.3: light emission, useShapeForLightOcclusion, map color e, por fim, os snapshots/delegates restantes.
- Os callbacks recebem o estado parcialmente inicializado; a ordem é observável quando consultam propriedades do próprio estado.
- Teste de regressão verifica a ordem dos callbacks e que isAir e destroySpeed ainda têm seus valores iniciais durante light emission/map color.


## Incremento — IntegerProperty indexação e parsing (23/11/2026)

- `IntegerProperty.IndexOf` agora reproduz `getInternalIndex` de 26.3: retorna -1 somente acima do máximo e usa subtração Int32 unchecked para os demais valores, inclusive o overflow Java de valores abaixo do mínimo.
- `TryParse` passou a validar o intervalo diretamente (`minimum <= value <= maximum`), como `IntegerProperty.getValue(String)`; isso evita aceitar texto fora do intervalo quando o índice sofre wraparound.
- Teste atualizado para distinguir a semântica do índice da validação do parser.


## Incremento — lifecycle do codec de BlockState (23/11/2026)

- O ramo de objeto de `BlockStateCodec` agora aplica `Lifecycle.Stable()` ao resultado de encode e decode, inclusive resultados de erro, reproduzindo `BlockState.FULL_CODEC = codec(...).stable()` de Minecraft 26.3.
- O ramo compacto por identificador continua preservando o lifecycle do `Registry.byNameCodec()`, em vez de forçar estabilidade sobre o codec do registry.
- Testes verificam o lifecycle em encode/decode dos formatos compacto e completo e do codec completo estrito.


## Incremento — visibilidade do construtor de StateDefinition (23/11/2026)

- O construtor genérico de `StateDefinition` deixou de ser público e passou a `protected internal`, alinhando-se ao construtor protegido do Minecraft 26.3.
- O builder interno continua podendo materializar a definição, e tipos derivados externos podem continuar estendendo o contrato.
- Teste de reflexão garante que consumidores externos não conseguem contornar a validação de propriedades feita pelo `StateDefinitionBuilder`.


## Incremento — predicado genérico de redstone conductor (23/11/2026)

- Portados `BlockBehaviour.Properties.isRedstoneConductor` e `BlockStateBase.isRedstoneConductor(level, pos)` como propriedade genérica do estado.
- O padrão da referência é `BlockStateBase::isCollisionShapeFullBlock`; a configuração permite substituir o predicado e recebe level/position da consulta.
- Testes cobrem o padrão em ar/pedra e a avaliação contextual de um predicado configurado.


## Incremento — Property.parseValue e partial state (23/11/2026)

- Implementado `StateProperty<T>.ParseValue`, equivalente ao `Property.parseValue` de Minecraft 26.3.
- O parse válido resolve a instância pré-calculada com `SetValue`; falhas de valor ou de tipo de entrada preservam o estado original como resultado parcial em `DataResult`.
- Testes verificam estado resultante, mensagem de erro vanilla e partial state para string inválida e entrada não textual.


## Incremento — cópia de propriedades no BlockState (23/11/2026)

- Portados `BlockStateBase.withPropertiesOf` e `BlockStateBase.copyProperty` para `BlockState.WithPropertiesOf` e `BlockState.CopyProperty`.
- `WithPropertiesOf` aplica a cópia sobre o estado receptor atual; não reseta para o estado default do bloco-alvo.
- `CopyProperty` usa `TrySetValue` para copiar somente a propriedade solicitada. A identidade da instância da propriedade é preservada; propriedade ausente ou homônima mas distinta não altera o destino.
- `Block.WithPropertiesOf` permanece como wrapper de conveniência sobre o estado default, sem duplicar a lógica.
- Testes cobrem cópia integral, cópia individual, preservação de outras propriedades e ausência/identidade distinta de propriedades no destino.


## Incremento — cópia integral das propriedades de bloco (23/11/2026)

- Portado `BlockBehaviour.Properties.ofFullCopy(BlockBehaviour)` como `BlockProperties.OfFullCopy(Block)`.
- O novo builder é independente do builder original, copiando todos os valores escalares, flags, delegates, função de offset e reação de piston que Asteria atualmente modela.
- Delegates são preservados por referência, sem wrapper que altere closure ou semântica do callback.
- Testes cobrem valores, flags, delegates, mutabilidade independente e argumento nulo.
- SoundType/instrument, feature flags, loot tables, description ID e predicate de spawn por EntityType ainda não existem no modelo Asteria; o factory não declara cópia desses domínios.


## Incremento — holder tipado do BlockState (23/11/2026)

- Portado `BlockStateBase.typeHolder()` como `BlockState.TypeHolder`.
- Estados de blocos registrados retornam a mesma referência intrusive já criada pelo registry, preservando identidade e metadados.
- Fixtures não registradas retornam o holder direto já usado internamente para diagnóstico, sem criar um novo objeto por consulta.
- Testes cobrem identidade do holder registrado e resolução do holder direto de uma fixture.


## Incremento — ciclo de vida de chunks: ChunkStatus (23/11/2026)

- Portados `ChunkType` e os dez `ChunkStatus` do Minecraft Java 26.3 na ordem original, com parent chain, índices, comparação de status, `max`, resolução pelo nome e codec do registry.
- Adicionado o registry built-in `chunk_status` ao root registry. `full` é o único status de tipo `LEVELCHUNK`; os nove anteriores são `PROTOCHUNK`.
- Os status iniciais compartilham o conjunto `WORLDGEN_HEIGHTMAPS`; `terrain` e os seguintes compartilham `FINAL_HEIGHTMAPS`, conforme a referência.
- Esse commit porta apenas o metadado dos estágios. Execução de tarefas, dependências de vizinhança e promoção ProtoChunk/LevelChunk permanecem pendentes de `ChunkStep`, `ChunkPyramid`, `ProtoChunk` e `LevelChunk`.


## Incremento — ChunkDependencies (23/11/2026)

- Portada a estrutura `ChunkDependencies` de Minecraft Java 26.3, com sequência imutável de status por distância, `size`, `getRadius`, `get`, `getRadiusOf` e representação textual em ordem de distância.
- O índice inverso por status mantém a maior distância na qual o status consultado ou um posterior está garantido; valores não garantidos retornam `-1`.
- Teste reproduz a janela acumulada de `FULL` e verifica os raios 11/3/2/1/0/-1, janela vazia e limites de indexação.
- `ChunkStep`, `ChunkPyramid` e execução de tasks não foram introduzidos neste incremento.


## Incremento — StaticCache2D (23/11/2026)

- Portada a utilidade genérica `StaticCache2D<T>` de Minecraft Java 26.3, incluindo `create`, `map`, `forEach`, `get`, `contains`, indexação e representação textual.
- A inicialização materializa toda a janela retangular em ordem X/Z; a transformação cria novo armazenamento e mantém a mesma área.
- Testes cobrem limites inclusivos/exclusivos, ordem, indexação, mapping, raio zero, tipo de exceção e diagnóstico de acesso externo.
- A classe é uma dependência direta de `ChunkStep.apply` e `ChunkGenerationTask`; não introduz scheduler customizado.


## Incremento — Interval (23/11/2026)

- Portada a classe `Interval` de Minecraft Java 26.3 para a aritmética de limites usada por Density Functions e ruídos.
- Preservados os singletons `NaI`, `INFINITE`, `[-1;1]` e `[0;1]`, identidade específica de NaI, validação de bounds, operações intervalares e tratamentos de zero/infinito/NaN.
- Os métodos `pow`, `log`, `lerp` e `mapMonotonic` seguem os ramos de borda da implementação 26.3. O delegate C# substitui o operador funcional fastutil sem mudar a ordem de aplicação.
- `IntervalChecks` cobre os ramos principais e erros. A classe fornece a faixa de retorno da próxima etapa `Noise/PerlinNoise`.


## Incremento — GradientNoise e SimplexNoise 2D/3D (23/11/2026)

- `GradientNoise` agora segue a tabela de 16 gradientes, a permutação sem sinal, seleção `permuteToGrad`, produto de gradiente e wrap de coordenadas de Minecraft Java 26.3.
- `SimplexNoise` agora expõe a faixa `[-2,2]`, o desvio padrão `0.42544` e a amostragem 3D completa por ranking de coordenadas, quatro cantos, atenuação `0.6` e escala `32`.
- A amostragem 2D foi mantida com a fórmula 26.3 e passou a compartilhar a função de canto 3D da referência, incluindo o range e constants.
- `GradientNoise.wrap` usa a redução modular 2^25 da referência. O override `SharedConstants.DEBUG_ENABLE_FARLANDS` não foi incluído porque Asteria ainda não possui esse modo de depuração.
- Testes cobrem amostras de seed fixo em pontos dos vários ramos tetraédricos, faixa/desvio padrão e fronteiras do wrap. Os testes de bioma existentes também protegem as amostras 2D anteriores.


## Incremento — DensityBuffer (23/11/2026)

- Portado `DensityBuffer` de Minecraft Java 26.3: array `float[]`, tamanho/capacidade fixos, `set`, `setRange`, `addTo`, `get`, `fill` e `copyFrom`.
- `copyFrom` mantém a mensagem e o tipo de erro da referência ao receber tamanho incompatível. Não foi introduzido pooling: somente a factory `createUnpooled` faz parte da classe-base atual.
- Testes verificam armazenamento linear, preenchimento de intervalo semiaberto, soma em precisão `float`, cópia, mismatch, capacidade fixa e buffer vazio.
- `DensityVolume` ainda depende do port de `BoundingBox`; `Noise.addToVolume` e a integração do volume serão conectados quando esse contrato estiver disponível.


## Incremento — BoundingBox geométrico (23/11/2026)

- Portada a geometria completa de `BoundingBox` de Minecraft Java 26.3: construção/normalização, ordenação por cantos, caixa infinita, orientação por direção, chunks intersectados, interseção 3D/2D, encapsulamento, translação, inflação, contenção, dimensões, centro, enumeração de cantos, igualdade, hash e string.
- A classe permanece com limites inclusivos, mesma ordem de cantos e mutabilidade nas operações `move`/`encapsulate`.
- `CODEC` de disco permanece pendente do escopo atual de persistência; `STREAM_CODEC` é excluído por multiplayer/rede. A geometria não depende desses codecs.
- `BoundingBoxChecks` cobre normalização, limites, interseção, transforms, orientação cardinal, chunks, cantos, igualdade e hash.
- O tipo agora fornece o contrato exigido por `DensityVolume.intersects` sem criar uma abstração geométrica ad hoc.


## Incremento — DensityVolume (23/11/2026)

- Portado `DensityVolume` como record imutável de Minecraft Java 26.3: dimensões X/Y/Z, coordenadas mínimas, passo de amostragem independente por eixo, máximos inclusivos e tamanho total.
- Preservada a ordem linear do volume (`Y` interno, depois X, depois Z) e a indexação de coordenadas mundiais por passo. `IndexOfBlock` retorna `-1` para coordenadas externas ou desalinhadas, inclusive em volumes esparsos por passo.
- `Intersects` usa `BoundingBox` em coordenadas mundiais inclusivas. Os construtores validam todas as dimensões e passos positivos com os diagnósticos da referência.
- Testes cobrem volume unitário e stepped, máximos, índices, interseção, igualdade do record e erros de configuração.
- A geometria e o armazenamento necessários para `Noise.addToVolume` estão agora presentes; o próximo passo é conectar o contrato de ruído e o amostrador Perlin 26.3.


## Incremento — Noise e PerlinNoise (23/11/2026)

- Portado o contrato `Noise` com `range/get(2D)/get(3D)` e o percurso de volume default Z/X/Y da referência Minecraft Java 26.3; `SimplexNoise` agora implementa essa interface.
- Portado `PerlinNoise` com o range `[-2,2]`, desvio padrão vanilla, amostragem 2D no plano XZ, interpolação 3D dos oito gradientes, derivadas e configuração de paridade.
- O override `addToVolume` foi portado como caminho otimizado da referência: pré-calcula os oito gradientes por coluna quando a célula Y muda, separa o produto XZ da contribuição Y e acumula no buffer em ordem Z/X/Y. Não é substituído por chamadas ponto-a-ponto.
- Acrescentados os helpers `Mth.Lerp/Lerp2/Lerp3/Smoothstep/SmoothstepDerivative` em precisão `float`, conforme a revisão 26.3 que mudou os cálculos de density/noise para precisão simples.
- `NoiseUtils` fornece os dois contratos utilizados por `PerlinNoise`: bias toward extreme e texto de paridade do octave.
- `PerlinNoiseChecks` inclui golden samples fixos, derivadas acumulativas, valores exatos da rota volumétrica otimizada, acumulação sobre buffer existente, configuração seed/permutation e interpolação float.
- A construção e a combinação em fractal de múltiplos octaves (`NormalNoise`, `NoiseStack.Perlin`) permanecem etapas posteriores; nenhum conjunto de octaves ou seed derivado foi inventado aqui.


## Incremento — NoiseStack (23/11/2026)

- Portado `NoiseStack` de Minecraft Java 26.3 como composição de `Noise` genéricos, com getters 2D/3D, faixa intervalar acumulada, `GetLayer`, builder, `AddStack` e composição de volume.
- `AddStack` achata as camadas e multiplica frequência/amplitude por camada, preservando a ordem do array. `AddToVolume` delega aos ruídos com as escalas e amplitudes combinadas, sem recalcular a composição por amostras pontuais.
- `Builder.Build` seleciona a subclasse interna `Perlin` quando todas as camadas têm tipo exato `PerlinNoise`, conforme a especialização 26.3. Combinações mistas mantêm a pilha genérica.
- `NoiseStackChecks` cobre faixa com amplitude negativa, composição 2D/3D, identidade das camadas, flattening, especialização Perlin, volume aditivo, pilha vazia e ruídos mistos.
- `NormalNoise` permanece pendente do contrato de randomização posicional (`forkPositional/fromHashOf`), que ainda não existe em Asteria; não foi substituído por hash/seed próprio.


## Incremento — randomização legada e WorldgenRandom (23/11/2026)

- Expandido `IRandomSource` para o contrato comum de 26.3: fork, positional factory, reseed, draws int/long/bool/float/double/Gaussian, inclusive range, triangular e consumo.
- `LegacyRandomSource` agora é extensível, reinicializa o cache gaussiano no reseed e implementa os draws completos do LCG de 48 bits; sua factory posicional deriva streams via `Mth.GetSeed` e Java `String.hashCode`.
- Portados `PositionalRandomFactory` e `MarsagliaPolarGaussian` com cache do segundo resultado.
- `WorldgenRandom` agora herda de `LegacyRandomSource` e envolve um `IRandomSource`; delega `Fork`/`ForkPositional`, incrementa o contador em cada pedido bruto de bits e preserva os métodos de seed usados em decoração, features e estruturas.
- `LegacyRandomSourceChecks` cobre vetores Java LCG, limites por potência e rejeição, positional streams, fork, contador e cache Gaussian.
- Xoroshiro e `RandomSupport` seguem como etapa distinta; a factory de oitavas de `NormalNoise` só será implementada depois que os dois algoritmos estiverem disponíveis.


## Incremento — RandomSupport e Xoroshiro 128++ (23/11/2026)

- Portados `RandomSupport` e `Seed128bit`: expansão LCG→128 bits, mixer Stafford13, MD5 UTF-8 com leitura big-endian, XOR dos words e gerador de seed único.
- Portados `Xoroshiro128PlusPlus` e `XoroshiroRandomSource` 26.3: transição de estado, recuperação do estado zero, draws int/long/bool/float/double/Gaussian, rejeição sem viés para `nextInt(bound)`, consumo, fork e factory posicional.
- `WorldgenRandom.Algorithm` seleciona as famílias LEGACY e XOROSHIRO. Em C#, o `enum` vanilla foi adaptado a um tipo fechado com instâncias nomeadas porque enums C# não podem declarar o método de fábrica `NewInstance`.
- `XoroshiroRandomSourceChecks` verifica vetores 26.3 do estado inicial, stream nextLong, bounded int, MD5, factory por coordenada/nome/seed, wrapper WorldgenRandom e cache Gaussian.
- Codec de serialização do estado Xoroshiro continua fora deste incremento; a integração de geração passa a poder usar os dois algoritmos e será exercitada por `NormalNoise`.


## Incremento — NormalNoise e composição multi-octave (23/11/2026)

- Portado `NormalNoise` de Minecraft Java 26.3: construção das oitavas pelo expoente base, persistência 0.5, lacunaridade 2.0, modifiers, normalização desativada/habilitada/legada, estimativa de desvio e range intervalar.
- A criação de ruído usa duas `PositionalRandomFactory` independentes. Cada oitava é semeada pelo nome `octave_<index>` via hash da factory e produz dois `PerlinNoise`: frequências `f` e `f * 1.0181268882175227`, com a mesma amplitude normalizada.
- Portados `CreateParity` e suas fórmulas antigas de normalização para preservar configurações com listas de amplitude existentes.
- O builder mantém os defaults vanilla (base amplitude 1, base octave 0, uma oitava, normalização ENABLED, modifiers vazios) e as falhas/ordem de configuração de `setOctaveCount` e `setAmplitudeModifier`.
- `createForLegacyNetherBiome` não foi substituído por ruído novo: continua pendente do port integral de `LegacyFbmInitializer`. Os codecs de parâmetros também seguem pendentes do escopo de persistência local.
- `NormalNoiseChecks` verifica faixas, fórmulas de normalização, configurações parity, factories posicionais independentes, sampling 3D e seleção de oitavas.


## Incremento — LegacyFbmInitializer (23/11/2026)

- Portado `LegacyFbmInitializer` de Minecraft Java 26.3 para o fBm histórico das Nether biomes. Preservados o consumo do octave zero mesmo quando desligado, a ordem de criação das oitavas inferiores, os `consumeCount(262)` para cada lacuna, o erro de quantidade de ruídos e o bloqueio atual de oitavas positivas.
- `NormalNoise.CreateForLegacyNetherBiome` agora compõe duas pilhas legacy sequenciais; a segunda usa o fator de frequência `1.0181268882175227` e ambas usam o `normalizationFactor * baseAmplitude` do `NormalNoise`.
- O caminho legacy foi mantido separado da criação moderna por nome `octave_<index>`; não foi substituído nem convertido para o algoritmo novo.
- `LegacyFbmInitializerChecks` compara o resultado contra a sequência manual vanilla de construções e consumo, testa oitava zero silenciada e verifica a falha de oitavas positivas.


## Incremento — buffers com escopo e contrato DensitySampler (23/11/2026)

- Portados `DensityBufferArena` e `ScopedDensityBuffer`: o arena global cria buffers sem pooling e libera com no-op; o buffer com escopo preserva `restore`, capacidade, validação de uso simultâneo, contador de idade e liberação idempotente.
- Portados `DensitySampler` e o record `Bound`: amostragem pontual e de volume, traversal naive vanilla em ordem Z/X/Y e aquisição de buffer escopado pelo `SamplerContext`.
- Portados `ContextKey` e `ContextMap` como dados tipados imutáveis; as keys preservam igualdade por identidade. O builder suporta parâmetros required/optional e leitura de parâmetros durante a construção.
- Portado `SamplerContext`: contexto de campos, arena configurável, buffers escopados, cache de volume e cache pontual indexados pelo cache id, com o mesmo uso de `DensityVolume.IndexOfBlock` e da posição empacotada de `BlockPos`.
- A expansão de células usa o arredondamento para múltiplos de 16 da referência; cada cache de volume fecha o buffer anterior quando a geometria muda. O cache de ponto evita reusar NaN como valor calculado.
- Testes cobrem ordenação das amostras, bind, buffer lease/restore/close, context keys/map, buffers customizados e caches de volume/ponto.
- `DensityFunction` e operadores ainda serão portados depois desta infraestrutura; este incremento não introduz nenhum gerador de densidade alternativo.


## Incremento — DensityFunction base e ConstantFunction (23/11/2026)

- Portado o contrato base `DensityFunction` 26.3: máscaras de domínio X/Y/Z, `axesFrom`, compilação para `DensitySampler`, reescrita de filhos, intervalo e domínio espacial.
- Portado `CompileContext` com resolução de `NormalNoise` via `Holder`, streams aleatórios por identificador e fonte legacy de End Islands.
- Portados `ConstantFunction` e `ConstantFunction.Sampler`: faixa pontual exata, domínio sem eixos, reescrita identidade, preenchimento de buffer e avaliação independente das coordenadas.
- Portados `DensityFunctions.zero()` (singleton compartilhado) e `constant(value)` (nova instância por chamada).
- `DfRewriteRule` usa delegate C# para representar a interface funcional Java; `DfRewriteRules.Sequence` mantém a ordem e o encadeamento dos resultados. A forma específica `SliceUniformAxes` aguardará `SliceFunction`; `INLINE_REFERENCE` aguardará o wrapper `HolderHolder`.
- Os codecs de função (`DensityFunction.CODEC`, `ConstantFunction.CODEC`, registry de tipos e bootstrap) não foram substituídos nem stubados: seguem adiados com persistência local. Os métodos default dos operadores também aguardam os nós reais `UnaryFunction`/`BinaryFunction` para não expor factories incompletas.
- `DensityFunctionChecks` cobre máscaras de eixos, singleton zero, criação de constantes, range, amostragem escalar/volumétrica, capacidade residual do buffer, reescrita identidade, ordem de sequência e superfície do CompileContext.


## Incremento — UnaryFunction (23/11/2026)

- Portado `UnaryFunction` de Minecraft Java 26.3 com os onze tipos e ids: ABS, SQUARE, CUBE, SQRT, HALF_NEGATIVE, QUARTER_NEGATIVE, RECIPROCAL, NEGATE, SQUEEZE, LOG e SIGN.
- Cada tipo compila para seu sampler correspondente; HALF_NEGATIVE/QUARTER_NEGATIVE compartilham o sampler parametrizado `LeakyReLUSampler`. Os samplers volumétricos primeiro delegam a amostragem ao filho e transformam somente os elementos do buffer ativo.
- `Range` usa as operações `Interval` já portadas: abs, square, mapeamento monotônico para transformações monotônicas, recíproco, log e sign. O domínio de eixos é herdado do filho.
- Portadas as factories de `DensityFunctions` e os métodos default correspondentes de `DensityFunction`.
- O enum conserva a ordem 26.3; ids são mapeados para nomes de registro, sem registrar codecs nesta etapa. `MapCodec` continua adiado com persistência, não substituído por codec local.
- `UnaryFunctionChecks` cobre as onze transformações, extremos e intervalos, aplicação escalar/volumétrica, ordem do enum, ids e reconstrução após rewrite.


## Incremento — BinaryFunction (23/11/2026)

- Portado `BinaryFunction` de Minecraft Java 26.3 com a ordem de enum ADD, SUB, MUL, DIV, MIN, MAX; ids de registro; reescrita dos dois filhos; união dos eixos de domínio e aritmética intervalar.
- O compilador preserva o caminho especializado por constante e o lado do operando: ConstAdd, ConstSub, ConstMul e ConstDiv. Divisão por divisor constante vira multiplicação pelo recíproco; divisão por numerador constante usa ConstDivSampler.
- `MulSampler` e `DivSampler` mantêm o curto-circuito escalar quando o lado esquerdo é zero; seus samplers volumétricos avaliam ambos os buffers. ConstDiv não herda esse curto-circuito.
- `Min`/ `Max` eliminam a outra branch quando os ranges não se sobrepõem, preservam os tipos especializados com constantes e usam os limites mínimos/máximos do lado direito para podar a amostragem pontual.
- Samplers genéricos avaliam a esquerda no buffer de saída e a direita em um `ScopedDensityBuffer` do contexto, liberando-o ao final. Min/max volumétrico preserva as comparações condicionais do código 26.3 em vez de `Math.Min/Max`.
- Portadas factories de `DensityFunctions` e métodos default `Add/Sub/Mul/Div` em `DensityFunction`. Map codecs de enumeração seguem adiados com persistência local.
- `BinaryFunctionChecks` cobre ids/ordem, otimização de operandos constantes em ambos os lados, curto-circuito escalar, ranges, domínios, poda de ranges disjuntos, samplers volumétricos, liberação de buffers e rewrite dos dois filhos.


## Incremento — LerpFunction (23/11/2026)

- Portado `LerpFunction` de Minecraft Java 26.3 com os três filhos `alpha/first/second`, range via `Interval.Lerp`, união dos três domínios espaciais e rewrite que mantém identidade se nenhum filho mudar.
- `compileSampler` compila os três filhos antes da seleção especializada. Um primeiro valor constante gera `ConstFirstSampler`; caso contrário, segundo valor constante gera `ConstSecondSampler`; sem constantes usa o sampler genérico.
- Os três samplers preservam os atalhos escalares e volumétricos: alpha 0 seleciona first; alpha 1 seleciona second; qualquer outro valor chama `Mth.Lerp`. O volume usa buffers escopados para os filhos auxiliares e fecha-os ao final.
- Adicionadas as factories `DensityFunctions.Lerp(alpha, first, second)` e a sobrecarga com primeiro valor constante.
- `LerpFunctionChecks` cobre os três caminhos de compilação, valores alpha fora de [0,1], atalhos de avaliação pontual, amostragem de volume, ranges, domínio espacial, buffers e reescrita.
- Codec da função continua adiado com persistência local; não foi criado substituto.


## Incremento — ClampFunction (23/11/2026)

- Portado `ClampFunction` de Minecraft Java 26.3: sampler do filho, transformação pontual/volumétrica via `Mth.clamp(float)`, intervalo via `Interval.clamp`, rewrite com preservação de identidade e domínio herdado do input.
- Adicionada a sobrecarga `Mth.Clamp(float, float, float)` com a ordem de comparação Java: retorna min somente se `value < min`, caso contrário `Math.min(value, max)`. Isso preserva o comportamento de NaN e de bounds invertidos da implementação vanilla.
- Adicionadas `DensityFunctions.Clamp` e `DensityFunction.Clamp`.
- A validação `min <= max` do codec vanilla permanece associada ao codec, não foi movida para o construtor runtime. Os codecs continuam adiados junto da persistência local.
- `ClampFunctionChecks` cobre valores abaixo/dentro/acima dos limites, NaN, intervalos disjuntos, volume, rewrite e domínio de eixos.


## Incremento — RangeChoiceFunction (23/11/2026)

- Portado `RangeChoiceFunction` de Minecraft Java 26.3: o input seleciona `whenInRange` no intervalo `[minInclusive, maxExclusive)`; os outros valores usam `whenOutOfRange`.
- O compilador primeiro compila o input. Só cria `ConstSampler` quando as duas saídas são `ConstantFunction`; se uma delas não for constante, compila ambas e usa o sampler genérico.
- O sampler constante mantém o par de valores como floats e apenas transforma o buffer do input. O sampler genérico amostra `whenInRange` no buffer de saída, o input e `whenOutOfRange` em buffers escopados, substituindo os índices fora do intervalo e liberando ambos os buffers ao final.
- `Range` encapsula apenas os intervalos das duas saídas; `DomainAxes` une input e saídas. Rewrite reescreve os três filhos e preserva identidade quando nenhum muda.
- Adicionada `DensityFunctions.RangeChoice`; codec permanece adiado com persistência local.
- `RangeChoiceFunctionChecks` cobre fronteiras min/max, valores interiores/exteriores, caminhos constante/genérico, avaliação escalar/volumétrica, ranges, domínios, buffer lifecycle e rewrite.


## Incremento — PowFunction (23/11/2026)

- Portado `PowFunction` de Minecraft Java 26.3 com avaliação escalar/volumétrica, rewrite dos dois filhos, intervalo via `Interval.Pow` e domínio pela união de base/expoente.
- Compilação avalia as duas subárvores antes da otimização. Base constante tem precedência e usa `ConstBaseSampler`; caso contrário, expoente constante usa otimização própria.
- Expoentes com módulo 0.5, 1, 2 e 3 usam `UnaryFunction.SqrtSampler`, a identidade do sampler base, `SquareSampler` ou `CubeSampler`. Expoentes negativos envolvem o sampler especializado com `ReciprocalSampler`; outros expoentes constantes usam `ConstExponentSampler`.
- O caminho geral calcula `Math.pow(base, exponent)` sem curto-circuito extra e usa buffer escopado para a amostragem do expoente.
- Adicionada a factory `DensityFunctions.Pow`. Codec continua adiado com persistência local.
- `PowFunctionChecks` cobre precedência da base constante, todos os expoentes especializados positivos/negativos, expoente genérico, base constante, caminho geral, ranges, domínio, volumes, buffers e rewrite.
