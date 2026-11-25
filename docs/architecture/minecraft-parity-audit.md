# Auditoria de paridade — Asteria 2 / Minecraft Java 26.3

> **Estado vigente (24/11/2026):** os incrementos históricos de visualização diagnóstica, controlador em primeira pessoa, streaming de renderização e interação temporária descritos mais abaixo foram retirados. Eles não fazem parte da implementação atual. O renderer de cubos por `MapColor`, a luz direcional da Godot e os controladores próprios não eram ports de Minecraft 26.3 e foram removidos. O runtime Godot permanece apenas como bootstrap de registries até o port do cliente e dos sistemas equivalentes de iluminação/renderização. `LightEngine` e `ChunkSkyLightSources` são partes já existentes do port de iluminação, não constituem por si só o motor completo.

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
| `PalettedContainer<T>` | `PalettedContainer<T>` / `PalettedContainerRO<T>` | Armazenamento single-value/local/global, resize, remapeamento de IDs, cópia, contagem, lookup e publicação de snapshot. `Pack`/`Unpack` serializam palette em primeira ocorrência e long array opcional; `CodecRW`/`CodecRO` gravam `palette` e `data` opcional, preservam fallback parcial de elementos inválidos e fazem repacking global. Integração em save/load de seção/chunk ainda pendente; packet IO segue fora do escopo single-player. |
| `PackedBitStorage` | `SimpleBitStorage` | Packing por palavra, limite sem cruzamento de palavra, tabela de divisão, get/set/fill/getAll/unpack e raw data comparados. |
| `ZeroBitStorage` | `ZeroBitStorage` | Semântica de zero bits, validação de índice/valor, unpack e cópia comparados. |
| `ThreadingDetector` | `ThreadingDetector` | Protocolo de exclusão de escritor e detecção de concorrência equivalentes. A representação do erro é adaptada: Asteria lança `InvalidOperationException`; o jogo usa `ReportedException` com `CrashReport` e logging. |
| `StateHolder<TOwner, TState>` / `StateDefinition<TOwner, TState>` | `StateHolder` / `StateDefinition` | Lookup por identidade, array de valores, produto cartesiano, vizinhos pré-computados, builder e igualdade por identidade comparados. `Equals` é sealed e delega a `Object.Equals`; `GetHashCode` delega a `Object.GetHashCode` e permanece sobrescrevível, como em 26.3. O código é compartilhado por `BlockState` e `FluidState`. |
| `SupportType` / `BlockState.IsFaceSturdy` | `SupportType` / `BlockStateBase.isFaceSturdy` | As três regiões geométricas e a ordem `FULL/CENTER/RIGID` seguem 26.3. O harness verifica seis direções, shape vazio/full e consulta dinâmica sem cache estático. |
| `Fluid` / `FluidState` | `Fluid` / `FluidState` | Parcial: registry, holder intrusivo, definição/estado padrão, estado vazio, ID map, source/empty/amount/own-height/random-ticking e `BlockState.GetFluidState`. `FlowingFluid`, água/lava, consultas de mundo, formas, fluxo, ticks e interação ainda não foram portados. |
| `Direction` / `Direction.Axis` / `Plane` / `FrontAndTop` / `AttachFace` | `Direction` / `FrontAndTop` / `AttachFace` | Parcial: seis direções, vetores, IDs, eixos, planos, yaw, nearest-direction, codecs de nome/ordinal (`Direction.CODEC`, `VERTICAL_CODEC`, `Axis.CODEC`), seleção aleatória e Fisher–Yates de `Direction`/`Axis`/`Plane`; `FrontAndTop`, `AttachFace` e 48 transformações de `OctahedralGroup`. APIs dependentes de entidade/renderer permanecem pendentes; `STREAM_CODEC` é excluído por pertencer à rede. O modelo C# usa objetos singleton por identidade onde os membros carregam dados. |
| `Position` / `Vec3` | `Position` / `Vec3` | Parcial: factories por `Vec3i`, aritmética, produto escalar/vetorial, distância, normalização, rotações com tabela Mth, alinhamento, projeção, igualdade/hash e finitude. Codec para persistência local, RNG, `Vec2` e JOML permanecem pendentes; codecs de rede não são alvo. `Position` tem nomes de acesso adaptados ao C#. |
| `AABB` / `HitResult` / `BlockHitResult` | `AABB` / `HitResult` / `BlockHitResult` | Parcial: AABB cobre normalização, operações vetoriais, clipping de segmento, colisão e medidas. `HitResult`/`BlockHitResult` agora fornecem o resultado de raycast, `MISS`, `inside`, direção, posição e sinalizador de world border. Sobrecargas específicas de JOML/BoundingBox e integração com entidades ainda pendentes. |
| `DiscreteVoxelShape` / `BitSetDiscreteVoxelShape` | Shapes homônimas | Parcial: grade/bitset X/Y/Z, bounds em cache, `isFullWide`, `firstFull`/`lastFull`, cópia, região preenchida, interior, rotação por `OctahedralGroup`, enumeração greedy de caixas, arestas com mesclagem e faces por transição de ocupação. As operações discretas centrais estão portadas; falta a integração com block states, mundo e renderer. |
| `VoxelShape` / `CubeVoxelShape` / `ArrayVoxelShape` / `SliceShape` / `SubShape` | Tipos homônimos | Parcial: coordenadas uniformes/explicitas, min/max, bounds, move, optimize, caixas/AABBs, arestas contínuas, closest-point, queries transversais, cache das seis faces, slices/subshapes, clipping para `BlockHitResult` e colisão swept-AABB por eixo. Igualdade de ocupação estática via `Shapes.Equal`; oclusão é feita pela álgebra de slices de `Shapes`. Contextos e integração com mundo/rendering ainda pendentes. |
| `Shapes` | `Shapes` | Parcial: factories e álgebra booleana; `Equal`; rotação por `OctahedralGroup` com centro arbitrário; tabelas de rotação horizontal, all-axis e attach-face; colisão agregada; `BlockOccludes`, `MergedFaceOccludes` e `FaceShapeOccludes`. A integração com block-state occlusion/sturdiness e renderer continua pendente. |
| `BooleanOp` / `IndexMerger` | Tipos homônimos | As 16 funções booleanas e os quatro mergers foram portados: identical, non-overlapping, indirect e discrete-cube. Seleção com tolerância `1e-7` e teto de custo 256 preservada. |


| `LegacyRandomSource` / `WorldgenRandom` / `GradientNoise` / `SimplexNoise` | Tipos homônimos | Parcial: LCG de 48 bits, adaptador de worldgen, tabela de permutação, gradientes, Simplex 2D e NoiseStack 2D cobrem os ruídos usados pelos efeitos e temperatura dos biomas. APIs adicionais de RNG, Simplex 3D e empilhamento 3D permanecem pendentes. |
| `NoiseSettings` | `NoiseSettings` | Portado: `min_y`/`height`, limites derivados de `DimensionType` (Y de -2032 a 2031), presets Overworld/Nether/End/Caves/Floating Islands, codec com validação de alinhamento de seção e limite superior, `Create` e interseção aritmética com `LevelHeightAccessor`. Testes cobrem valores de fronteira e interseções sem sobreposição. |
| `NoiseRouter` | `NoiseRouter` | Portado: os oito campos de densidade 26.3 (`temperature`, `vegetation`, `continents`, `erosion`, `depth`, `ridges`, `chunk_surface_level`, `final_density`) têm codec obrigatório com os mesmos nomes. `CreateClimateSampler` vincula somente os seis canais climáticos e mapeia `vegetation` para humidity e `ridges` para weirdness. Testes cobrem round-trip dos oito campos, rejeição de campo ausente/malformado, ordem de lookup e amostragem quart. CI do harness e build Godot passam. |
| `DensityFunctionType` / codecs do grafo de densidade | `DENSITY_FUNCTION_TYPE` / `DensityFunctions.DIRECT_CODEC` | Parcial: 40 MapCodecs para `constant`, `noise`, `shift_a`, `shift_b`, `shift`, `distance_to_point`, `end_outer_islands`, `find_top_surface`, `old_blended_noise`, 11 unários, seis binários, quatro arredondamentos, `gradient`, `pow`, `spline`, `lerp`, `clamp`, `range_choice`, `interval_select`, `cache`, `interpolated` e `slice`. `DensityFunction.CODEC`/`REFERENCE_CODEC` usam `RegistryCodecs.Holder`, com inline versus holder registrado, referência obrigatória, resolução canônica, lookup e owner testados. `NormalNoise.DIRECT_CODEC`/`CODEC` cobrem amplitude/octave, octave count, normalização booleana/`legacy` e amplitude modifiers. `noise` usa holder, escalas e deslocamentos opcionais; `shift_*` compartilham holder de ruído. `find_top_surface` porta limites de Y da dimensão, célula positiva, busca pela primeira densidade positiva e projeção via `YSampler`. `SmearedPerlinNoise` porta `fudgeYScale`, gradientes verticais e percurso volumétrico; `BlendedNoise` cria as três pilhas de oitavas (-15/-15/-7), fatores 0.9999847412109375/0.9999847412109375/12.75, escala base 684.412 e codec `old_blended_noise`. A seed `terrain` usa o namespace local `asteria`. As chaves `worldgen/density_function` e `worldgen/noise` estão definidas, mas os registries/datasets de valores ainda não foram instalados em `BuiltInRegistries`. Permanecem `blend_density` e valores `SimpleDensityFunction` (dependem de `Blender`/`Beardifier` ausentes); `NoiseGeneratorSettings`, bootstrap dos valores vanilla, `RandomState`, `NoiseChunk`, `Aquifer` e regras de material/superfície ainda precisam ser portados/conectados. CI cobre os codecs e samplers e o build Godot passa. |
| `Mth` | `Mth` | Parcial: tabela trigonométrica SIN de 65536 entradas, `Sin`, `Cos`, `Floor`, `Lerp`, `Square`, `LengthSquared` e `Length`, cobrindo a dependência portável de `Direction`/`Vec3`. O restante da utilitária numérica não foi introduzido. |
| `Heightmap` / `Heightmap.Types` | `Heightmap` / `Heightmap.Types` | Seis variantes com IDs, nomes, usos, predicados por tags/fluidos e priming/atualização incremental; `Types.CODEC` agora serializa nomes no formato normal e IDs ordinais com `DynamicOps` comprimido. Retenção após worldgen e regras de envio estão portadas. Persistência de dados compactados e integração com o codec de seção/chunk ainda pendentes. |
| `ChunkAccess` / `ProtoChunk` | `ChunkAccess` / `ProtoChunk` / `LevelChunk` | Parcial: base abstrata com posições, seções, lookup/mutação de blocos e fluidos, seis heightmaps com priming/atualização, post-processing por seção, acesso quart a biomas condicionado ao status, light-source tracking, dirty/light correctness, inhabited time e `CanBeSerialized`; `ProtoChunk` implementa mutação, status persistido e notificações do light engine. `NoiseBasedChunkGenerator.CreateBiomes` agora executa separadamente a fase BIOMES e `OverworldWorld.GetChunk` avança explicitamente BIOMES → TERRAIN. `ChunkStep`/`ChunkPyramid` ainda são metadados, sem delegates de tarefa nem scheduler. Ainda faltam `LevelChunk`, block entities, estruturas/referências, ticks, retrogen, serialização local e execução/scheduler real das etapas. |
| `Biome` / `BiomeResolver` / `Climate` | `Biome` / `BiomeResolver` / `Climate` | Parcial: registry default e holders para Plains/Desert, clima básico de bioma, resolver quart e palette storage. `Climate.Parameter`, `ParameterPoint`, `TargetPoint`, `Sampler` e `ParameterList` estão portados; a busca R-tree 7D preserva o fan-out padrão 19, ordenação por dimensão/custo e último resultado por thread. Os codecs de intervalo, ponto climático e lista de parâmetros são locais e testados, inclusive validação min/max antes da quantização. BiomeSpecialEffects cobre cores e modificadores; BIOME_INFO_NOISE seed 2345, temperatura FROZEN seed 3456, correção por altitude seed 1234, precipitação e cache térmico por thread estão portados. Núcleo de atributos ambientais tipados portado: registries environment_attribute/attribute_type, boolean/float/angle_degrees/RGB/ARGB/integer/moon_phase e 17 definições genéricas dentro do escopo; cada Biome armazena um EnvironmentAttributeMap imutável. Ainda faltam BiomeSource, dataset climático vanilla/OverworldBiomeBuilder, seleção integrada ao mundo e resolução/layering das fontes ambientais. O cross-fade genérico de listas foi portado; os tipos PARTICLE/AMBIENT_PARTICLES usam codecs locais e os tipos de partículas genéricos mantidos no escopo. ACTIVITY, schedules e AI específicos de mobs estão fora do escopo. Áudio, bed rules e atributos de blocos/entidades específicos estão fora do escopo. Mob spawn settings estão excluídos; biome generation settings/features/carvers, verificações dependentes de nível e codecs locais ainda pendentes. |
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
- Geração de terreno, noise settings, carvers, features e estruturas. `StructureSettings` já porta biomas, etapa de geração e ajuste de terreno (sem `spawn_overrides`); `Weighted`, `WeightedRandom` e `WeightedList` dão suporte à seleção ponderada dos elementos. `StructureTemplatePool` ainda depende do modelo de `StructurePoolElement` e do pipeline real de templates/processadores. `SpawnTargetPoint` trata da adequação da posição de spawn global do jogador, não de mobs.
- `FlowingFluid`, água/lava, propagação, geometria, ticks e interação com o mundo; os contadores de fluidos em seções e o núcleo `FluidState` já foram portados parcialmente.
- Resolução/layering de atributos ambientais entre dimensão, bioma, timeline e clima; o mapa imutável por bioma já está integrado, mas ainda não há resolvedor ambiental do mundo. Mob spawn settings estão excluídos; biome generation settings/features/carvers, verificações dependentes de nível e codec de save/load local permanecem pendentes.
- `BiomeSource` e datasets climáticos vanilla ainda não estão conectados ao mundo; o modelo de parâmetros e a R-tree estão portados, mas não há seleção de bioma integrada à geração.
- O grafo de densidade já inclui codec e sampler de `old_blended_noise`, inline/holder e referência obrigatória; `NoiseRouter` está portado. Ainda faltam `blend_density`, valores `SimpleDensityFunction` e os registries/datasets de produção; `Blender`, `Beardifier`, `NoiseGeneratorSettings`, `RandomState`, `NoiseChunk`, `Aquifer` e regras de material/superfície precisam ser portados/conectados para reproduzir a geração de terreno.
- `LightEngine`, armazenamento por seção, propagação SKY/BLOCK e `LevelLightEngine` estão portados e cobertos pelo harness. A ordem de atualização foi corrigida para processar reduções e aumentos antes de publicar o mapa visível. Ainda falta conectar o lifecycle das tarefas de iluminação ao scheduler de chunks e ao cliente/renderizador.
- Integração de block shapes, colisão e raycast com o mundo local, contextos de colisão, física e rendering local.
- Renderização de blocos, culling, meshing, materiais e atlas.
- Tick scheduler local, block ticks, fluid ticks e random tick loop permanecem necessários para simulação single-player.
- Redstone (power propagation, wires, repeaters, comparators e circuit updates) é explicitamente fora de escopo. A exceção é o predicado estrutural `BlockState.IsRedstoneConductor`, sem implementação de sinal/propagação.
- As demais APIs `BlockStateBase` pendentes são agrupadas por dependência: sinais (`isSignalSource`, `hasAnalogOutputSignal`, `getOwnSignal`, `getSignal`, `getDirectSignal`, `getAnalogOutputSignal`, `shouldRedstoneWireConnectTo`) dependem do subsistema de redstone; colisão/ações de entidades e jogador, quebra, uso, cópia e drops dependem de entidades, jogador e itens; atualizações de vizinhança, sobrevivência, eventos e ticks dependem de `Level`/scheduler; ticker/menu e retenção de block entity dependem do modelo de block entities; som e instrumento dependem de áudio. Sobrecargas com `CollisionContext` e substituição por `BlockPlaceContext` também ficam pendentes até seus contextos existirem. Nenhum desses contratos foi preenchido com stubs.
- Entidades que não sejam mobs, jogadores, itens, inventário, crafting, menus e drops. Mobs, AI e comportamento de mobs estão explicitamente excluídos do escopo.
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

- Portados os nove estágios de chunk mantidos no escopo single-player. A etapa vanilla `SPAWN` foi removida porque executa spawn de mobs; os demais estágios mantêm a ordem relativa, parent chain, índices, comparações, `max`, resolução por nome e codec do registry.
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
- `BoundingBox` agora está disponível para `DensityVolume`; `Noise.addToVolume` e a integração do volume continuam pendentes.


## Incremento — BoundingBox geométrico (23/11/2026)

- Portada a geometria completa de `BoundingBox` e o codec de dados local de Minecraft Java 26.3: seis inteiros na ordem minX/minY/minZ/maxX/maxY/maxZ, com resultado parcial para listas longas e lifecycle Stable. A geometria inclui construção/normalização, cantos, caixa infinita, orientação por direção, chunks intersectados, interseção 3D/2D, encapsulamento, translação, inflação, contenção, dimensões, centro, enumeração de cantos, igualdade, hash e string.
- A classe permanece com limites inclusivos, mesma ordem de cantos e mutabilidade nas operações `move`/`encapsulate`.
- `CODEC` de dados local está implementado. `STREAM_CODEC` permanece excluído por pertencer à camada de protocolo/rede.
- `BoundingBoxChecks` cobre codec, ordem dos seis campos, round-trip, comprimentos inválidos e valor parcial, além de normalização, limites, interseção, transforms, orientação cardinal, chunks, cantos, igualdade e hash.
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
- `DfRewriteRule` usa delegate C# para representar a interface funcional Java. `DfRewriteRules.Sequence` mantém a ordem e o encadeamento; `InlineReference` substitui `HolderHolder` pelo valor do holder; `SliceUniformAxesRewriteRule` reproduz o rewrite recursivo 26.3, preserva constantes/gradientes, remove somente eixos ausentes e evita duplicar slices existentes.
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


## Incremento — NoiseFunction (23/11/2026)

- Portado `NoiseFunction` de Minecraft Java 26.3, integrando `Holder<NormalNoise>` com `DensityFunction.CompileContext.CreateNoiseSampler` e o contrato `Noise` já portado.
- Sem shifts, compila para `Sampler`: ponto chama `Noise.Get(x*xzScale, y*yScale, z*xzScale)`; volume inicializa o buffer com zero e usa `Noise.AddToVolume` com escalas e amplitude 1.
- Quando há shifts X/Z sem shift Y, compila para `ShiftedXzSampler`; quando Y também existe, usa `ShiftedXyzSampler`. Os volumes preservam a ordem Z/X/Y, reutilizam o buffer de saída para shift X e liberam buffers temporários de Y/Z com escopo.
- `Range` é o range dos dados de ruído. `DomainAxes` remove Y quando yScale=0 e X/Z quando xzScale=0, depois une os eixos de todos os shifts. Rewrite preserva a identidade quando os três shifts não mudam.
- Adicionadas as factories `Noise(holder)`, `Noise(holder, xzScale, yScale)`, `Noise(holder, yScale)` e `ShiftedNoise2d`.
- `NoiseFunctionChecks` cobre coordenadas, escalas, os três modos de compilação, ordem de volume, ranges, domínio, rewrite e liberação dos buffers temporários.
- Codec de ruído segue adiado com persistência local.


## Incremento — ShiftNoiseFunction (23/11/2026)

- Portada a interface `ShiftNoiseFunction` e suas três variantes Java 26.3: `ShiftA`, `ShiftB`, `Shift`. Range é o intervalo do holder multiplicado por 4.
- As três funções mantêm `COORDINATE_FACTOR = 0.25` e `VALUE_FACTOR = 4.0`. `ShiftA` usa `NoiseFunction.Sampler(xzScale=0.25, yScale=0)` escalado por 4; `Shift` usa 0.25 nos três eixos e também multiplica por 4.
- `ShiftB` foi portado com o volume transposto `sizeZ × sizeX × 1`, origem/steps permutados Z/X, sampling no plano Z/X, lookup por `IndexUnchecked(z,x,0)` e replicação por Y via `SetRange`. O buffer transposto é escopado e liberado.
- Os eixos de domínio preservam X/Z para ShiftA/B e XYZ para Shift. Rewrite cria uma nova instância do mesmo subtipo, como na implementação vanilla (não retorna `this`).
- Adicionadas as factories `DensityFunctions.ShiftA/B/Shift`. Codecs permanecem adiados com persistência local.
- `ShiftNoiseFunctionChecks` verifica fórmulas escalares, range, eixos, rewrite, transposição com passos independentes, replicação vertical e liberação de buffer.


## Incremento — TilingMode e GradientFunction (23/11/2026)

- Portado `TilingMode` 26.3 com CLAMP_TO_EDGE, REPEAT e MIRRORED_REPEAT e seus nomes de serialização.
- Portado `GradientFunction`: seleção de coordenada pelo eixo, fator linear `(toValue - fromValue) / (toCoordinate - fromCoordinate)`, range encapsulando os extremos, domainAxes associado ao eixo e rewrite identidade.
- `ClampedSampler` usa coordenada clampada ao menor/maior endpoint mas preserva a direção do gradiente via `fromCoordinate` original. `RepeatSampler` e `MirroredRepeatSampler` usam floorMod/floorDiv Java, incluindo coordenadas negativas e coordenadas inicial/final invertidas.
- Samplers volumétricos preservam os três caminhos específicos: X replica por Y para cada Z; Y escreve valores por plano Y; Z preenche faixas lineares por plano Z.
- Adicionadas `DensityFunctions.Gradient` e `YClampedGradient`. A validação `fromCoordinate != toCoordinate` permanece na camada codec vanilla, não foi introduzida artificialmente no construtor runtime.
- `GradientFunctionChecks` cobre os três tiling modes, coordenadas negativas, período espelhado, endpoints invertidos, amostras volumétricas por eixo, ranges, domínios e factories.


## Incremento — RoundFunction (23/11/2026)

- Portado `RoundFunction` de Minecraft Java 26.3 com FLOOR, ROUND, CEIL e TRUNCATE, mantendo a ordem do enum e os ids.
- `roundToInteger` usa floor, ceil e o truncamento condicional por sinal como na referência. ROUND replica `Math.round(float)`: floor de `input + 0.5f`, saturação para int e NaN→0; empates negativos são arredondados para cima.
- O compilador primeiro compila o input. `IntegerMultipleSampler` é escolhido apenas quando `multiple` é ConstantFunction com valor exatamente 1; nos demais casos `multiple` também é compilado e o sampler genérico é usado.
- O sampler genérico amostra input no buffer de saída e multiple em buffer escopado. Se multiple for zero, retorna o input sem divisão; caso contrário calcula `roundToInteger(input / multiple) * multiple`.
- Range usa a fórmula 26.3: `Interval.mul(Interval.mapMonotonic(Interval.div(input.range, multiple.range), roundToInteger), multiple.range)`; domainAxes combina os filhos. Rewrite preserva identidade somente se ambos os filhos mantiverem identidade.
- Adicionadas as factories `Round`, `Floor`, `Ceil` e `Truncate`, com overloads de múltiplo default 1.
- `RoundFunctionChecks` cobre order/ids, tie-breaking Java, NaN e saturação, múltiplos unitários/dinâmicos/constantes, zero multiple, volume, range, domínio e rewrite.
- Codec e validações de persistência permanecem adiados; nenhuma implementação alternativa foi introduzida.


## Incremento — IntervalSelectFunction (23/11/2026)

- Portado `IntervalSelectFunction` de Minecraft Java 26.3: amostra o input e escolhe a primeira função cujo threshold é estritamente maior que o input; se nenhum threshold satisfaz `input < threshold`, escolhe a última função.
- Com um único threshold, compila somente o ramo abaixo e o ramo final para `SingleThresholdSampler`. Com zero ou mais de um threshold, compila a lista inteira para o sampler genérico, preservando a forma de seleção da referência.
- Em volume, o sampler único avalia as duas saídas em buffers separados. O sampler genérico adquire um buffer por função e fecha todos em `finally`, inclusive quando a avaliação de qualquer saída lança uma exceção. Na amostragem pontual, apenas o ramo selecionado é avaliado.
- `Range` encapsula apenas os ranges das funções de saída; `DomainAxes` combina input e todas as saídas. Rewrite visita input e depois cada função, mantendo a identidade se nenhum filho mudar.
- Adicionada `DensityFunctions.IntervalSelect`. As validações vanilla de quantidade de thresholds, mínimo de funções e ordenação crescente pertencem ao codec e continuam adiadas com persistência local; o runtime não inventa uma validação substituta.
- `IntervalSelectFunctionChecks` cobre thresholds estritos, NaN, sampler single/generic, branch point-only, avaliação volumétrica, range, domain axes, rewrite e liberação de buffers inclusive no caminho de exceção.


## Incremento — SliceFunction (23/11/2026)

- Portado `SliceFunction(axis, coordinate, input)` de Minecraft Java 26.3, incluindo samplers X/Y/Z e composição automática de slices aninhados X/Z em `XzSampler`.
- Os samplers pontuais fixam somente a coordenada do eixo e encaminham as outras duas coordenadas para o input.
- Cada sampler volumétrico preserva o atalho vanilla quando o volume já é um plano no eixo/coordenada solicitados. Caso contrário, cria um volume auxiliar de dimensão 1 nesse eixo, preserva a origem e todos os passos independentes, avalia o input no plano e replica os valores para as posições correspondentes do volume original.
- `XzSampler` amostra uma linha Y fixa em X/Z, depois replica pelas dimensões X e Z; slices X/Z aninhados são fundidos em uma avaliação do input mais interno.
- `Range` herda o intervalo do input. `DomainAxes` remove somente o bit do eixo fixado. Rewrite mantém identidade se o input não muda.
- Adicionadas as factories `DensityFunctions.Slice` e `SliceY`. Codec segue adiado com persistência local.
- `SliceFunctionChecks` cobre samplers por eixo, combinação X/Z em ambas as ordens, volumes com steps independentes, shortcuts de plano e liberação dos buffers auxiliares.


## Incremento — DfRewriteRule (23/11/2026)

- Portadas as duas regras estáticas de Minecraft Java 26.3: `InlineReference` e `SliceUniformAxes`.
- `InlineReference` substitui somente `DensityFunctions.HolderHolder` pelo valor do holder; qualquer outro nó mantém a identidade.
- `SliceUniformAxesRewriteRule(parentDomainAxes)` preserva ConstantFunction e GradientFunction, compara o domínio atual com o domínio do pai e reescreve os filhos com a mesma regra quando os domínios coincidem.
- Quando os domínios diferem, os filhos são reescritos com uma regra cujo domínio é o do nó atual; os eixos presentes no pai e ausentes no nó são removidos com slices de coordenada zero.
- A remoção considera slices já existentes na cadeia externa e não os duplica. A ordem de inserção permanece X, Z, Y, deixando Y como wrapper externo quando os três eixos são removidos.
- `DfRewriteRuleChecks` cobre inlining, nós ignorados, propagação recursiva, caminho de igualdade do domínio, deduplicação de eixos e ordem exata dos wrappers.


## Incremento — CacheFunction e DensityFunctionCompiler (23/11/2026)

- Portado `CacheFunction` como pedido de cache: preserva o filho em `Range` e `DomainAxes`, reescreve o filho com a regra recebida e rejeita compilação direta; o nó só pode ser preparado pelo compilador.
- Portado `CachingDensitySampler(id, input)`, que delega amostragem pontual e volumétrica às células de cache de `SamplerContext`.
- Portado `DensityFunctionCompiler`: memoiza samplers por igualdade de nós, serializa a otimização sob lock, aplica `InlineReference` e substituição/compartilhamento de caches antes de `SliceUniformAxes`.
- Cada input de cache estruturalmente igual reutiliza o mesmo `PreparedCache` e ID. A preparação reescreve o input com a sequência completa, cria o sampler de cache e preserva range/domínio do input preparado.
- `PreparedCache` é interno ao compilador, retorna o sampler já preparado e permanece não serializável; não há codec alternativo. Os codecs locais e o registry de tipos de density functions continuam adiados.
- A publicação concorrente usa `ConcurrentDictionary<DensityFunction, Lazy<DensitySampler>>` para garantir uma única compilação por chave, preservando o comportamento de `computeIfAbsent` da referência.
- `CacheFunctionChecks` e `DensityFunctionCompilerChecks` cobrem erro de compilação direta, rewrite, metadata, inlining de holders, compilação concorrente, cache pontual, cache de volume, contexto sem cache, substituição de buffer, compartilhamento de IDs e caches aninhados.


## Incremento — BoundedFloatFunction e CubicSpline (23/11/2026)

- Portado `BoundedFloatFunction<C>` como `IBoundedFloatFunction<TContext>`, incluindo identidade, constante com intervalo pré-computado e composição de contexto (`comap`).
- Portado `CubicSpline<TContext>` com nós Constant e Multipoint, builder com transformação de valores e pontos com derivadas explícitas.
- A avaliação multipoint preserva a busca binária do segmento, extrapolação linear pelos declives das extremidades e a interpolação cúbica 26.3: lerp entre os valores dos pontos mais o termo `t * (1 - t) * lerp(t, a, b)`.
- `Range` reproduz a propagação por intervalos da referência, incluindo NaI, extensões de extremidade e a estimativa de limites para segmentos com derivadas não nulas.
- `MapCoordinates` transforma recursivamente o coordenador e as splines filhas; `ForEachCoordinate` mantém a ordem pre-order. Os codecs recursivos e a integração com SplineFunction ficam adiados para o incremento próprio, sem stubs.
- `CubicSplineChecks` cobre funções limitadas, constante, construção, monotonicidade dos pontos, amostragem cúbica, extrapolação, splines aninhadas, traversal, mapping, propagação de range e casos NaI/erro.

- Adaptação C# documentada: `MapCoordinates` recria o nó Constant value-equivalente ao mudar o parâmetro genérico de contexto, já que tipos genéricos são reificados em .NET; o valor, range e comportamento de amostragem são idênticos. O método `ForEachCoordinate` continua no-op para Constant, inclusive quando o callback é nulo.


## Incremento — SplineFunction (23/11/2026)

- Portado SplineFunction de Minecraft Java 26.3 sobre a base CubicSpline já validada. A coordenada descritiva mantém a DensityFunction; durante compilação, cada ocorrência é convertida em SamplerCoordinate com sampler, índice estável de buffer e intervalo da função.
- A transformação preserva a ordem pre-order de ForEachCoordinate e MapCoordinates, incluindo coordenadas em splines aninhadas.
- O sampler pontual usa PointSplineInput, com cache local por índice durante a avaliação de um ponto.
- O sampler volumétrico usa BufferSplineInput: amostra cada coordenada sob demanda em um ScopedDensityBuffer, reutiliza os valores durante toda a varredura linear do volume e fecha todos os buffers em Dispose, inclusive quando um sampler filho lança exceção.
- Range delega ao range da spline; DomainAxes agrega os eixos das funções usadas como coordenadas. Rewrite percorre coordenadas recursivamente e preserva identidade se nenhuma função filha mudar.
- Adaptação C# documentada: o parâmetro genérico existente em CubicSpline<TContext> representa o contexto de avaliação; por isso o spline descritivo usa CubicSpline<SplineFunction.Unit> e o compilado usa CubicSpline<SplineInput>. A API Java parametriza o nó por I extends BoundedFloatFunction<?>; a semântica de amostragem e a ordem de traversal são preservadas.
- Os codecs recursivos permanecem adiados com persistência local; nenhuma implementação substituta foi criada.
- SplineFunctionChecks cobre ponto, volume, range, domínio, coordenadas aninhadas, spline constante, rewrite e liberação de buffers no caminho de exceção.


## Incremento — ContextBoundSampler e DensitySamplerSet (23/11/2026)

- Portado `ContextBoundSampler(key, fallbackSampler)`: cada amostragem pontual ou volumétrica consulta `SamplerContext.GetFieldOrDefault` e delega integralmente ao sampler encontrado ou ao fallback.
- Portada a interface funcional `DensitySamplerSet`: `Get(function)` retorna `DensitySampler.Bound`; o método default `SampleValue` resolve o bound e encaminha a amostragem pontual.
- Os dois componentes reutilizam `ContextKey<DensitySampler>`, `ContextMap`, `SamplerContext` e `DensitySampler.Bound` já existentes, sem novo estado global, cache ou dependência.
- `DensitySamplerContextBindingChecks` cobre fallback ausente, override de contexto para ponto/volume e lookup do sampler set com contexto vinculado.


## Incremento — InterpolatedFunction (23/11/2026)

- Portado o algoritmo de Minecraft Java 26.3, incluindo a seleção do caminho direto, volumes com passo não unitário, volumes em passo de bloco, cálculo das células abrangidas e amostragem dos oito cantos.
- O caminho direto só é usado quando os passos correspondem ao tamanho da célula (ou o eixo tem tamanho 1) e as coordenadas mínimas estão alinhadas.
- O caminho com passo não unitário expande o volume para coordenadas de bloco unitárias, interpola no volume expandido e recolhe exatamente os índices solicitados.
- O caminho de bloco usa floor division/modulus para coordenadas negativas, amostra uma grade de cantos e executa o preenchimento trilinear por eixo na ordem Z/X/Y. A acumulação vertical do valor preserva a ordem de arredondamento do código Java.
- A amostragem pontual consulta diretamente cantos alinhados; nos demais pontos amostra o volume 2×2×2 dos cantos e chama a interpolação trilinear do Java.
- Range, DomainAxes e RewriteChildren delegam ao filho, preservando identidade quando não há alteração.
- Mth.FloorDiv e Mth.FloorMod reproduzem a semântica de coordenadas negativas de Java.
- O construtor valida tamanhos positivos, refletindo a restrição do codec de referência (POSITIVE_INT); o codec local permanece adiado com persistência.


## Incremento — ProtoChunk block storage (23/11/2026)

- Portada a parte de armazenamento e mutação de `ProtoChunk` do Minecraft Java 26.3, com `GetPersistedStatus`, `SetPersistedStatus`, lookup de estado/fluido e `SetBlockState`.
- `SetBlockState` mantém a ordem vanilla: rejeição fora do build height, atalho de seção vazia + AIR, mutação da seção, atualização do estado de seção no light engine quando o status alcança `INITIALIZE_LIGHT`, detecção de mudança de propriedades de luz, atualização de fontes de skylight, atualização/priming em lote dos heightmaps do status.
- `LightEngine.HasDifferentLightProperties` compara identidade, light dampening, emissão e uso de shape para oclusão como a referência; os pontos de integração `CheckBlock` e `UpdateSectionStatus` são contratos abstratos do engine, não stubs.
- Adicionado o estado built-in `VOID_AIR`, distinto de `AIR`, usado por leituras/escritas fora da altura de construção como em `ProtoChunk`.
- Testes cobrem bounds, estados fora do mundo, retorno do estado anterior, atalho AIR, heightmaps das fases worldgen/finais, transição de seção vazia, notificações do light engine e atualização de skylight.
- Divergências temporárias e explícitas: construtor omite `UpgradeData`, `BlendingData`, ticks agendados e dependências de load; a classe ainda não modela block entities, entidades, estruturas, post-processing ou retrogen. Esses campos/métodos não foram preenchidos com stubs.


## Incremento — post-processing por seção de ProtoChunk (23/11/2026)

- Portado o armazenamento `postProcessing` de `ChunkAccess`: array com uma lista opcional de `short` por seção vertical, inicializada vazia e exposta pelo acesso ao backing array.
- `ChunkAccess.AddPackedPostProcess` acumula offsets no slot solicitado; o comportamento base de `MarkPosForPostProcessing` registra o aviso vanilla de operação não suportada.
- `ProtoChunk.MarkPosForPostProcessing` só registra posições dentro da altura de construção e escreve no slot da seção correspondente.
- `PackOffsetCoordinates` e `UnpackOffsetCoordinates` preservam os quatro bits locais X/Y/Z e a composição com coordenadas de seção, inclusive para chunks e Y negativos.
- Testes cobrem alocação por seção, round-trip, ordem de append, limites inclusivos e erro de índice fora do array.


## Incremento — ProtoChunk biome availability (23/11/2026)

- `ChunkAccess.GetNoiseBiome` tornou-se virtual para permitir o comportamento específico de chunks em geração.
- `ProtoChunk.GetNoiseBiome` agora só expõe a palette quando `GetPersistedStatus()` alcança `BIOMES`; antes disso lança `InvalidOperationException` com a mensagem vanilla.
- Após o estágio, a consulta delega ao lookup da classe base, preservando o mapeamento quart/section e o clamp vertical do mundo.
- Testes verificam erro antes e depois do preenchimento mas antes da promoção de status, contagem de 64 células por seção, consulta após BIOMES e limites inferior/superior de quart Y.


## Incremento — highest generated chunk status (23/11/2026)

- Adicionado `ChunkAccess.GetHighestGeneratedStatus`, separado de `GetPersistedStatus`, conforme o contrato 26.3.
- No modelo atual, que ainda não possui `BelowZeroRetrogen`, o maior status gerado é o status persistido. A separação permite que a integração de retrogen seja adicionada quando seus tipos/dependências forem portados, sem alterar consumidores.
- `ProtoChunk.GetNoiseBiome` usa agora `GetHighestGeneratedStatus` para decidir quando a palette pode ser consultada.
- Testes cobrem igualdade entre os dois status no estado atual, mudança após `SetPersistedStatus` e a guarda de biomas.


## Incremento — ChunkStep e ChunkPyramid (23/11/2026)

- Portados o builder de dependências diretas/acumuladas de `ChunkStep` e as pirâmides de geração e carregamento da referência Java 26.3.
- O cálculo preserva a posição do pai na janela, o máximo de status requerido em cada raio, os raios de escrita de bloco de `TERRAIN` (0) e `FEATURES` (1), e a regra de raio zero para o próprio status.
- A janela acumulada de `GENERATION_PYRAMID/FULL` continua com 12 anéis; a geometria de dependências e a margem de segurança são preservadas.
- Divergência deliberada de escopo: `ChunkStatus.SPAWN` e os respectivos passos das pirâmides foram removidos porque essa fase do vanilla executa spawn de mobs. Em Asteria, `FULL` sucede diretamente `LIGHT`; as tarefas de spawn de mobs não serão portadas sem solicitação explícita.
- Portada também a margem de segurança usada para limitar coordenadas máximas de chunk.
- Delimitação: estes tipos contêm os metadados de agendamento. `ChunkStep.apply`, delegates de tarefas, profiling, `WorldGenContext`, `GenerationChunkHolder` e o scheduler ainda dependem de gerador/mundo local e não foram substituídos por callbacks ou no-ops artificiais.


## Incremento — contrato de mutação de ChunkAccess (23/11/2026)

- Portada a sobrecarga compartilhada `ChunkAccess.SetBlockState(position, state)`, que encaminha para a mutação concreta com flags padrão `3`.
- A sobrecarga com flags agora é abstrata em `ChunkAccess`, como na referência, e `ProtoChunk` a implementa diretamente.
- Testes verificam o valor padrão 3, passagem sem alteração de flags explícitas, retorno do estado anterior, estado armazenado e a resposta concreta fora da altura do mundo.


## Incremento — contrato de serialização de ChunkAccess (23/11/2026)

- Portado `ChunkAccess.CanBeSerialized` como método virtual com retorno padrão `true`, conforme Minecraft Java 26.3.
- Testes verificam que a resposta permanece verdadeira em chunks recém-criados e marcados como dirty.


## Incremento — helper público de post-processing (23/11/2026)

- A visibilidade de `ChunkAccess.GetOrCreateOffsetList` foi alinhada ao contrato público estático da referência 26.3.
- Testes cobrem criação lazy, reutilização da mesma lista, isolamento entre seções e falha nativa do array para índice fora da faixa.

## Incremento — limites válidos de ChunkPos (23/11/2026)

- Portados `ChunkPos.IsValid()` e `ChunkPos.IsValid(x, z)` conforme Java 26.3, comparando `Mth.absMax` com `ChunkPyramid.MaxChunkCoordinateValue`.
- O cálculo preserva a semântica de overflow de `Math.abs(int.MinValue)` em Java; não usa `Math.Abs` do .NET, que lançaria exceção e diverge da referência.
- `ChunkChecks` cobre os dois limites inclusivos, o primeiro valor além do limite e o caso extremo de `int.MinValue`.
- Commit: `5e3f44e`; CI: [checks](https://github.com/sarakborges/asteria2/actions/runs/45226852530) — sucesso.

## Incremento — serialização de ChunkPos (23/11/2026)

- Portado `ChunkPos.CODEC` como codec estável de exatamente dois inteiros, serializados na ordem X/Z, correspondente ao `Codec.INT_STREAM` da referência Java 26.3.
- A implementação usa os codecs de listas existentes; não adiciona codec de protocolo/`StreamCodec`, fora do escopo single-player.
- `ChunkChecks` cobre round-trip com coordenadas negativas/positivas, ordem dos campos, lifecycle estável e rejeição de listas com tamanho diferente de dois.
- Commit: `3324711`; testes do harness e build Release do Godot concluídos com sucesso: [checks](https://github.com/sarakborges/asteria2/actions/runs/45227425598).

## Incremento — codec de Heightmap.Types (23/11/2026)

- Portado o codec de `Heightmap.Types` equivalente a `StringRepresentable.fromEnum`: nomes serializados em operações normais e IDs ordinais quando `DynamicOps.CompressMaps` está ativo.
- Preservadas as mensagens `Unknown element name` e `Unknown element id`; o mapeamento de ID segue a ordem dos seis valores declarados.
- `ChunkChecks` cobre os seis nomes, os seis IDs compactados, round-trip e ambos os casos desconhecidos.
- Commit: `2873b42`; CI: [checks](https://github.com/sarakborges/asteria2/actions/runs/45227917515) — sucesso.

## Incremento — pack/unpack de PalettedContainer (23/11/2026)

- Portado o contrato de `PalettedContainerRO.PackedData`: palette de entradas, armazenamento `long[]` opcional e `bitsPerEntry` (incluindo `-1` como valor desconhecido).
- `PalettedContainer.Pack` reencoda o conteúdo em IDs locais compactos na ordem da primeira ocorrência; palettes globais são serializadas com IDs locais e largura de bits derivada da palette no disco.
- `PalettedContainer.Unpack` valida bit count, ausência de storage e tamanho do long array; reconstrói palettes locais e globais, incluindo remapeamento para IDs do registry.
- Testes cobrem palette single-value, local, global (257 valores), round-trip XYZ e dados/arrays inválidos.
- Commits: `51f6c8e`, `65ff33e`, `5ae5d3b`; CI: [checks](https://github.com/sarakborges/asteria2/actions/runs/45229599908) — sucesso.

## Incremento — codecs locais de PalettedContainer (23/11/2026)

- Adicionados `CodecRW` e `CodecRO` para o record de save `palette` + `data` opcional, usando lista de longos com preservação bit a bit dos valores assinados/não assinados.
- `ExtraCodecs.OrElsePartial` reproduz o fallback da referência: mantém resultados parciais; quando não há valor parcial, fornece o default sem apagar o erro original.
- Testes cobrem omissão de `data` para single-value, round-trip JSON de palette local e global, codec read-only e fallback parcial.
- Commit: `c5880f8`; CI: [checks](https://github.com/sarakborges/asteria2/actions/runs/45230321666) — sucesso.

## Incremento — overloads de SectionPos (23/11/2026)

- Adicionado `SectionPos.Of(Position)`, convertendo as três coordenadas reais com `Mth.floor` e shift aritmético, inclusive coordenadas fracionárias negativas.
- Adicionado `SectionPos.BottomOf(ChunkAccess)`, combinando X/Z do chunk com `GetMinSectionY()`.
- Adicionado `SectionPos.Offset(long, Direction)`, delegando aos vetores unitários das seis direções.
- Testes cobrem flooring negativo, as seis direções e bottom section para chunk em (-2, 3) com build height iniciando em -64.
- Commit: `bb98db9`; CI: [checks](https://github.com/sarakborges/asteria2/actions/runs/45231314839) — sucesso.

## Incremento — NoiseSettings de worldgen (23/11/2026)

- Portados os limites verticais, codec `min_y`/`height`, validações de seção e presets dimensionais de `NoiseSettings` do Minecraft Java 26.3.
- `ClampToHeightAccessor` preserva a interseção aritmética original, inclusive alturas não positivas para intervalos sem sobreposição.
- Testes cobrem round-trip, limites dimensionais, alinhamento e presets.
- Commit: `3054037`; CI 910: [checks](https://github.com/sarakborges/asteria2/actions/runs/45234987407) — sucesso.

## Incremento — Climate parameter search (23/11/2026)

- Portados `Climate.Parameter`, `ParameterPoint`, `TargetPoint`, `Sampler` e `ParameterList`, incluindo quantização em 10.000, fitness de sete dimensões e codec local dos registros climáticos.
- Portada a R-tree 7D de seleção de bioma, com fan-out padrão 19, bucketização por custo, ordenação por dimensão e cache thread-local da última folha.
- Os testes comparam a busca indexada com força bruta em diferentes fan-outs, validam codecs e limites, e verificam amostragem nas coordenadas quart.
- O port ainda não inclui `BiomeSource`, dados climáticos vanilla nem integração com a geração do mundo.
- Commits de implementação, correção e testes: `dc953b5`, `4b374c3`, `3ecee9d`, `399c6d1`, `e5ee06b`, `127f7b1`, `085e770`, `378f296` e `49a7a0d`.
- CI 921: [checks](https://github.com/sarakborges/asteria2/actions/runs/45237271508) — harness de paridade e build Godot concluídos com sucesso.

## Incremento — bootstrap inicial de DensityFunction (23/11/2026)

- Adicionado o registry de valores `asteria:worldgen/density_function`, separado do registry de tipos de codec `density_function_type`.
- Registradas as duas primeiras entradas de `NoiseRouterData.bootstrap` do Minecraft Java 26.3, na mesma ordem: `zero` e `y`. A sequência para aqui intencionalmente: a próxima etapa vanilla é o bootstrap das regras de minério, ainda não portadas.
- `zero` usa o singleton `DensityFunctions.Zero()`. `y` usa o gradiente Y clamped com limites `DimensionType.MIN_Y * 2` e `DimensionType.MAX_Y * 2`, derivados dos limites dimensionais locais.
- Testes verificam a identidade do registry, as chaves/ordem/holders, o nó constante zero e a amostragem do gradiente nos extremos e fora dos limites.
- Este incremento não declara o dataset de density functions completo nem habilita geração de chunks.

## Incremento — horizontal density shifts (23/11/2026)

- Registradas `zero`, `y`, `shift_x` e `shift_z`. A ordem local ainda não é a ordem integral de `NoiseRouterData.bootstrap`: o estágio `registerOreVeins`, que ocorre antes dos shifts em 26.3, permanece pendente.
- `shift_x` usa `cache(ShiftA)`; `shift_z` usa `cache(ShiftB)`, conforme `NoiseRouterData.bootstrap`. Ambos referenciam o holder registrado de `asteria:offset` (parâmetros do ruído vanilla `shift`).
- Os testes verificam chaves e ordem, tipo de nó, identidade do holder de ruído e contrato de domínio/range.
- Os parâmetros do ruído `offset` já estavam no dataset de ruídos; não foi criada uma cópia nem um algoritmo alternativo.
- Correção de escopo 26.3: `vein_toggle`, `vein_ridged` e `vein_gap` não devem ser tratados como campos antigos do NoiseRouter. A versão 26.3 moveu a seleção de veios para a material rule `minecraft:ore_vein`; as density functions de suporte continuam registradas por `NoiseRouterData` e são consumidas por essa regra. A implementação da regra material permanece pendente.

- Validação final: executar o harness e o build Godot no HEAD corrigido do incremento de shifts.


## Incremento — dimension base 3D density functions (23/11/2026)

- Registradas as entradas `overworld/base_3d_noise`, `nether/base_3d_noise` e `end/base_3d_noise`. A posição dessas entradas no bootstrap local ainda será realinhada após o port do estágio de veios, que precede os shifts na ordem vanilla.
- As três usam o tipo vanilla `old_blended_noise`, já implementado em `BlendedNoise`; nenhuma implementação paralela ou aproximação foi criada.
- Parâmetros preservados: Overworld `(0.25, 0.125, 80, 160, 8)`; Nether `(0.25, 0.375, 80, 60, 8)`; End `(0.25, 0.25, 80, 160, 4)`.
- Testes verificam identidade, tipo, ordem local e os cinco parâmetros de cada entrada. A ordem integral do bootstrap continua pendente do port de `registerOreVeins`; o registro não equivale à integração desses nós em `NoiseRouter` nem habilita geração de chunks.


## Incremento — bootstrap de density functions de veios (23/11/2026)

- Portado `NoiseRouterData.registerOreVeins` antes dos shifts, corrigindo a ordem real de bootstrap do Minecraft Java 26.3.
- Registradas as seis entradas vanilla: `toggle`, `richness`, `mask`, `copper_density`, `iron_density` e `gap`.
- A máscara usa `ore_veininess`, `ore_vein_a`, `ore_vein_b`, limites interpolados em Y e células 4×8; cobre usa faixa Y [0, 50) e ferro [-60, -8).
- `clampedMap` segue a expansão vanilla: clamp do input, fator `(toMax-toMin)/(fromMax-fromMin)` e offset `toMin-fromMin*factor`, usando os nós existentes de clamp/multiplicação/adição. Não foi introduzido um operador novo nem lógica de colocação de blocos.
- Testes verificam sequência integral dos 13 registros atualmente portados, tipos de nós, disponibilidade dos quatro ruídos e intervalos das funções.
- A aplicação de minério continua fora deste incremento: as densidades ainda precisam ser consumidas pela material rule `minecraft:ore_vein`.


## Incremento — funções climáticas do Overworld (23/11/2026)

- Portadas as quatro funções de temperatura/vegetação para Overworld normal e biomas grandes, com os respectivos holders de ruído e shifts X/Z compartilhados.
- Portadas as funções de continentes, erosão e ridges, preservando os wrappers `cache` do bootstrap 26.3.
- Portada `ridges_folded` com a árvore exata de `peaksAndValleys`, incluindo as constantes float originais.
- Testes verificam ordem dos oito registros, identidade dos holders, escalas, dependências de shift, presença dos caches e árvore de expressão de ridges_folded.
- Permanecem pendentes as splines de terreno, offset/factor/jaggedness/depth, superfícies preliminares e finais, além da montagem dos NoiseRouters dimensionais.


## Incremento — pool de buffers de densidade e ownership em RandomState (24/11/2026)

- Portado `DensityBufferPool` de Minecraft Java 26.3 com arredondamento de capacidade em múltiplos de 16, busca de reutilização com limite superior exclusivo de 2×, preferência por capacidade exata, seleção best-fit, restauração do buffer, envelhecimento e descarte.
- Portados `Mth.positiveCeilDiv` e `Mth.roundToward`, usados pelo algoritmo de alocação e testados para entradas positivas, negativas e alinhadas.
- Integrado o ownership de pools em `RandomState`: criação com idade máxima 20, reutilização LIFO, limite de 16 pools retidos, sincronização e garbage collection de pools vazios.
- Adicionadas verificações de paridade para alocação/reutilização, limites best-fit, idade, clear e lifecycle de pools em RandomState.
- Esta etapa fecha a dependência de buffers exigida pelo construtor de `NoiseChunk` em 26.3. `NoiseChunk` ainda depende do port integrado de configurações de geração, aquifer e blending; nenhum substituto simplificado foi criado.


## Incremento — identidade tipada do contexto do Beardifier (24/11/2026)

- Portada a chave singleton `Beardifier.CONTEXT_KEY` com o tipo concreto `Beardifier`, conforme Minecraft Java 26.3.
- `DensityFunctionContextKeys.BEARDIFIER` agora referencia a mesma instância da chave, eliminando duas chaves de identidade distintas com o mesmo nome.
- Generalizado `ContextBoundSampler<TSampler>` para preservar o tipo real do valor de contexto. Os samplers de blend continuam usando `DensitySampler`; o sampler de beardifier usa `Beardifier` e `Beardifier.EMPTY` como fallback vanilla.
- Testes verificam identidade da chave, fallback vazio e amostragem do beardifier efetivamente injetado.
- Esta é uma dependência direta da construção de `NoiseChunk`; não altera o algoritmo geométrico de Beardifier nem cria estrutura/manager de estruturas.


### Ajuste de compatibilidade — chave única de Beardifier

- A chave de contexto é uma única instância `ContextKey<DensitySampler>`, compartilhada por `Beardifier.CONTEXT_KEY` e `DensityFunctionContextKeys.BEARDIFIER`.
- A especialização genérica foi adaptada para a interface `DensitySampler`: `ContextKey<T>` é invariante em C#, enquanto os samplers injetáveis precisam manter compatibilidade com o pipeline e fixtures já existentes. O valor concreto continua sendo `Beardifier` em runtime.
- `ContextBoundSampler` mantém sua API não genérica, preservando os consumidores existentes. O teste novo confirma identidade da chave e que a instância concreta de Beardifier injetada é amostrada.


## Correção de paridade — final_density no Minecraft Java 26.3 (24/11/2026)

- Corrigido o bootstrap dos três perfis de terreno do Overworld: `final_density` agora registra apenas o `min(post_processed, noodle)` definido pelo grafo vanilla.
- Removida a soma implícita de `SimpleDensityFunction.BEARDIFIER`. Em 26.3, o beardifier deixou de ser acrescentado implicitamente ao `final_density`; a função continua disponível para inclusão explícita quando o grafo de dados exigir.
- Atualizados os testes dos perfis normal, large-biomes e amplified para verificar a estrutura sem a soma implícita.


## Incremento — estados fonte de água e lava (24/11/2026)

- Registrados os tipos de fluido fonte WATER e LAVA, mantendo EMPTY como primeiro ID do registry e adicionando os dois tipos na ordem esperada para este slice.
- Estados fonte têm quantidade 8, isSource=true, altura própria amount / 9, altura 1 quando o mesmo fluido está acima e shape calculado pela altura contextual.
- Registrados os blocos líquidos WATER e LAVA, com estado de fluido correspondente, propriedades liquid/replaceable/no-collision e shape de bloco vazio.
- Testes cobrem holders e IDs dos tipos/estados, source/amount/height/shape e a associação BlockState para FluidState.
- Escopo explícito: apenas variantes fonte estão portadas. Estados fluidos, espalhamento, ticks e conversão de fonte ainda não foram portados; estes registros fecham as dependências de estado necessárias ao próximo port do NoiseBasedAquifer, não implementam a simulação completa de fluidos.


## Incremento — NoiseBasedAquifer do Minecraft Java 26.3 (24/11/2026)

- Portado o contrato `Aquifer`: `FluidPicker`, `FluidStatus.at`, `createDisabled`, `Config.create`, `computeSubstance` e `shouldScheduleFluidUpdate`.
- Implementado `NoiseBasedAquifer` com grade 16×12×16, posições pseudoaleatórias por célula, seleção dos quatro centros mais próximos, caches de localização/status e cache de superfície em coordenadas quart.
- Portadas as regras de fluido global, exceção de lava, inundação total/parcial, superfície aleatorizada, seleção de lava subterrânea, barreira água/lava e pressão interpolada com ruído de barreira lazy.
- Portados para `Mth` os helpers usados pelo algoritmo (`Map`, `ClampedMap`, `ClampedLerp` e `Quantize`), preservando o quantize por floor inclusive para valores negativos.
- Testes cobrem limites de `FluidStatus`, aquifer desativado, densidade positiva, fluido global de lava, fast path acima do limite de superfície, sampling normal determinístico e os helpers numéricos.
- O caminho de geração de fluido de depuração do `SharedConstants.DEBUG_DISABLE_FLUID_GENERATION` foi omitido: Asteria ainda não possui a flag global de depuração equivalente e o caminho normal 26.3 é preservado.
- Esta classe agora pode produzir estados de material/fluido para uma etapa de geração, mas ainda não há `NoiseChunk` nem a execução da pipeline `NoiseBasedChunkGenerator`; portanto ainda não há mundo jogável nem integração visual.


## Incremento — núcleo serializável de MaterialRule (24/11/2026)

- Portados os contratos `MaterialRule` e `RuleEvaluator`, incluindo codecs distintos para regra inline, holder fixo e wrapper `HolderHolder` da referência Java 26.3.
- Adicionados os registries `worldgen/material_rule` e `worldgen/material_rule_type`; o segundo registra os codecs de regra por nome, sem misturar valores e tipos.
- Portadas `BlockRule` (`result_state`) e `SequenceRule` (`sequence`), incluindo compilação para avaliadores, caminho de uma única regra e retorno da primeira regra que produz estado não nulo.
- Adicionada a estrutura mutável inicial do `MaterialRuleContext`, preservando coordenadas e contadores de atualização XZ/Y usados pelas condições lazy. O contexto ainda será completado com serviços de biome, ruído, superfície e densidades.
- Testes cobrem type registry, codec dispatch, serialização inline e round-trip de BlockRule/SequenceRule, tipo desconhecido e execução first-match.
- Delimitação: as condições, `OreVeinRule`, serviços completos do `MaterialRuleContext`, `MaterialSystem` e o dataset de regras vanilla ainda não foram portados. Este incremento estabelece o núcleo de regra serializável/compilável; não materializa a superfície do chunk por si só.


## Incremento — MaterialCondition e ConditionRule (24/11/2026)

- Adicionados os registries `worldgen/material_condition` e `worldgen/material_condition_type`, com dispatch por codecs e holders inline/referenciados.
- Portadas `NotCondition` (`invert`), `SteepCondition` (limiar X/Z -4/+4) e `HoleCondition` (`surfaceDepth <= 0`), com IDs e ordem do bootstrap 26.3.
- Portada `ConditionRule` (`if_true`, `then_run`), incluindo compilação do predicado uma vez e avaliação do ramo apenas quando a condição é verdadeira.
- `MaterialRuleContext` ganhou `surfaceDepth` e caches lazy de condições XZ/Y, invalidados pelos contadores vanilla de atualização.
- Testes cobrem codecs namespaced, holders, singleton codecs, inversão, cache e invalidação XZ, HoleCondition e integração serializada/compilada de ConditionRule.
- Permanecem pendentes condições dependentes de biome, noise e serviços completos de superfície, além de MaterialSystem e do dataset vanilla.


## Incremento — condições de água, superfície preliminar e profundidade (24/11/2026)

- Portadas `WaterCondition` (`offset`, `surface_depth_multiplier`, `add_stone_depth`) e seu predicado lazy por Y, incluindo o caso sem água acima e validação -20…20.
- Portada `AbovePreliminarySurfaceCondition`, comparando `blockY >= minSurfaceLevel` conforme 26.3.
- Portados `CaveSurface` (`ceiling`/`floor`) e `StoneDepthCondition`, incluindo profundidade acima/abaixo, `surfaceDepth`, faixa secundária mapeada de -1…1 e cache lazy Y.
- O contexto aceita `surfaceSecondary` e `minSurfaceLevel` na atualização de coluna; seu cálculo permanece responsabilidade do MaterialSystem, ainda não portado.
- O registry atualiza a ordem relativa vanilla dos seis tipos implementados: water, steep, not, hole, above_preliminary_surface e stone_depth. Tipos anteriores que exigem biome, noise ou vertical anchors seguem pendentes.
- Testes cobrem codecs, limites, comparações de floor/ceiling e atualizações das caches XZ/Y.


## Incremento — NoiseGeneratorSettings e registry de noise settings (24/11/2026)

- Portado `NoiseGeneratorSettings` com os campos `noise`, `default_block`, `default_fluid`, `noise_router`, `material_rule`, `spawn_target` obrigatório, `sea_level`, `aquifers`, `legacy_random_source` e `debug_functions`.
- `material_rule` usa holder codec registry-aware com suporte a definição inline; `aquifers` e `debug_functions` são opcionais, e `spawn_target` omisso decodifica para lista vazia.
- Adicionados `Registries.NOISE_SETTINGS` (`worldgen/noise_settings`) e `BuiltInRegistries.NoiseGeneratorSettings`; o valor built-in `minecraft:overworld` foi carregado no incremento de dados subsequente.
- `GetRandomSource()` seleciona LEGACY ou XOROSHIRO de acordo com `use_legacy_random_source`.
- O componente depreciado `disable_mob_generation` foi excluído da API e do codec conforme a decisão de manter sistemas de mobs fora do escopo.
- Este incremento completa o objeto de configuração, mas ainda não cria presets de dimensões nem executa `NoiseChunk`; a integração com a geração permanece pendente.


## Incremento — NoiseChunk e contexto de amostragem por chunk (24/11/2026)

- Portado o coordenador `NoiseChunk` com `DensityVolume`, `Aquifer`, `DensitySamplerSet` e lifecycle `Close`/`Dispose`, seguindo o construtor 26.3 com `RandomState`, Beardifier opcional, settings, fluid picker e blender opcional.
- O contexto único do chunk ativa os caches do `SamplerContext`, utiliza o `DensityBufferPool` emprestado de `RandomState`, injeta `Beardifier.EMPTY` como fallback e só registra blender/alpha/offset quando o blender não está vazio.
- O aquifer é criado a partir de `NoiseGeneratorSettings.Aquifers`; sem configuração, é usado `Aquifer.CreateDisabled`. A factory posicional nomeada `aquifer` é obtida de `RandomState`.
- Estendido `IChunkBlender` para fornecer o buffer de alpha/offset do volume e portado `BlenderOutputBuffer` para amostragem quart-grid; o blender vazio mantém os fallbacks 1.0/0.0 sem registrar campos no contexto.
- `SamplerContext` agora é descartável e fecha os buffers persistentes dos cache cells, permitindo que `NoiseChunk.Close` retorne as leases ao pool antes de devolvê-lo ao `RandomState`.
- Testes cobrem fallbacks de alpha/offset/Beardifier, injeção de parâmetros do blender, seleção de aquifer ativo/desativado, volume cacheado e rejeição de amostragem após close.
- Delimitação: a implementação concreta de `Blender` (coleta de dados de chunks antigos e cálculo de blending) não foi portada. `NoiseChunk` consome o contrato e um `IChunkBlender` futuro; sem um blender real e sem `NoiseBasedChunkGenerator`/materialização, ainda não há mundo jogável.


## Incremento — preenchimento base de ProtoChunk por NoiseBasedChunkGenerator (24/11/2026)

- Portado `NoiseBasedChunkGenerator.CreateFluidPicker`: seleciona lava abaixo de `min(-54, sea_level)` e o fluido padrão abaixo do nível do mar, respeitando o limite exclusivo de `FluidStatus`.
- Portado `FillFromNoise`: intersecta `NoiseSettings` com a altura do chunk, cria um `DensityVolume` 16×altura×16, amostra `NoiseRouter.final_density` por volume e consulta o aquifer para cada bloco.
- O retorno nulo do aquifer seleciona `default_block`; retornos explícitos selecionam água/lava/ar. Estados de ar não são gravados. Posições com atualização de fluido pendente são marcadas para pós-processamento no `ProtoChunk`.
- Os dois heightmaps de worldgen são preparados antes da escrita; ao final são primados os quatro heightmaps finais. O preenchimento mantém a ordem Z/X/Y descendente do fluxo vanilla.
- Testes cobrem default block em densidade positiva, fronteiras do picker lava/água/ar, coordenadas de chunks deslocados e os seis heightmaps.
- Delimitação: esta etapa materializa `final_density`/aquifer. `MaterialSystem`/surface rules e carvers ainda precisam executar depois na pipeline TERRAIN; não é ainda um mundo completo jogável e não adiciona sistemas de mobs.


## Incremento — VerticalAnchor, WorldGenerationContext e condições de altura (24/11/2026)

- Portados `WorldGenerationContext` e `VerticalAnchor` com as quatro variantes serializáveis: absolute, above_bottom, below_top e relative_to_sea_level; bounds numéricos seguem DimensionType 26.3.
- `MaterialRuleContext` agora pode receber contexto de geração e `RandomState`; expõe `ResolveAnchorY` e `GetOrCreateRandomFactory` sem inventar valores quando esses serviços não foram fornecidos.
- Portada `YCondition` (`y_above`) com limite inclusivo, multiplicador de surface depth e opção de somar stone depth above; predicado usa cache lazy Y.
- Portada `VerticalGradientCondition` com anchors capturados na compilação, factory posicional nomeada e probabilidade interpolada por Y; os extremos true/false são inclusivos e a zona intermediária é determinística por coordenada.
- Os codecs e o bootstrap de material conditions seguem a ordem relativa 26.3 para `vertical_gradient` e `y_above` antes de `water`.
- Testes cobrem codecs de anchors e condições, bounds, interseção de altura e determinismo posicional.
- Permanecem pendentes condições de biome, noise threshold e temperature, além da execução do MaterialSystem que popula os dados do contexto durante a passada de superfície.


## Incremento — biome, noise threshold e temperature conditions (24/11/2026)

- Adicionada a chave canônica `Registries.BIOME` e implementada `BiomeCondition` com `biome_is`, holder sets, cache lazy de biome por posição Y e otimização `possibleBiomes` para resultado constante verdadeiro/falso.
- Implementada `NoiseThresholdCondition` com resource key `noise`, limites inclusivos `min_threshold`/`max_threshold` e campo opcional `is_3d`; o contexto reutiliza `NormalNoise` e cacheia amostras 2D por XZ e 3D por Y.
- Implementada `TemperatureCondition` como singleton, consultando `Biome.ColdEnoughToSnow` na posição atual e no sea level da dimensão.
- Atualizada a ordem dos codecs no bootstrap de 26.3: biome, noise_threshold, vertical_gradient, y_above, water, temperature, steep, not, hole, above_preliminary_surface, stone_depth.
- Testes cobrem holder-set serializado em RegistryOps, cache e invalidação de biome, pré-computação por biomas possíveis, codecs 2D/3D, limites de ruído e temperatura em baixa/alta altitude.
- O cálculo de `surfaceDepth`, `surfaceSecondary`, `minSurfaceLevel`, `possibleBiomes` e o preenchimento do contexto durante a passada de superfície ainda dependem do `MaterialSystem`, que permanece o próximo port.


## Incremento — passada genérica de MaterialSystem (24/11/2026)

- Adicionado MaterialSystem com os ruídos surface e surface_secondary, cálculo posicional de surfaceDepth, derivação do limiar de superfície preliminar e passada genérica por coluna baseada no heightmap.
- MaterialRuleContext agora recebe o volume esperado e os samplers do mesmo NoiseChunk; o sampler de chunk_surface_level é pré-amostrado em um volume 16×1×16 sob demanda, com fallback pontual fora do volume.
- A passada percorre as colunas de cima para baixo, mantendo stoneDepthAbove, stoneDepthBelow e waterHeight, e aplica o avaliador de regras compilado uma vez por chunk. Estados fluidos aplicados pela regra são marcados para pós-processamento.
- RandomState passa a possuir uma única instância de MaterialSystem vinculada às configurações de geração. NoiseBasedChunkGenerator.BuildTerrain compartilha o mesmo NoiseChunk entre o preenchimento de densidade/aquifer e a passada genérica de materiais; FillFromNoise permanece disponível sem aplicar materiais.
- Testes adicionados para aplicação de BlockRule na superfície e para AbovePreliminarySurfaceCondition, incluindo o caminho de pré-amostragem do density sampler.
- Delimitação: este incremento não implementa ainda as extensões específicas de Eroded Badlands e Frozen Ocean, nem geração/aplicação das clay bands; os registries atuais ainda não contêm os blocos e biomas exigidos por esses ramos vanilla. Carvers e integração desta etapa com o carregamento real de biomas também permanecem pendentes.


## Incremento — fonte multi-noise e preenchimento das paletas de bioma (24/11/2026)

- Portada a seleção genérica MultiNoiseBiomeSource sobre Climate.ParameterList: resolução usa o resultado do R-tree climático 7D e a fonte expõe o conjunto distinto de biomas possíveis.
- A fonte cria um BiomeResolver por chunk a partir do Climate.Sampler ligado aos seis samplers do NoiseRouter, preservando amostragem em coordenadas quart.
- NoiseBasedChunkGenerator.BuildTerrain agora preenche os containers 4×4×4 de todas as seções antes da geração de densidade e da passada de superfície. As regras dependentes de bioma consultam o mesmo resolver e o mesmo sampler climático usados para preencher as paletas.
- O overload de biomeGetter permanece para fontes fixas/customizadas e é adaptado à interface quart-resolution; a nova sobrecarga MultiNoiseBiomeSource usa PossibleBiomes para as otimizações de condições.
- Testes cobrem seleção entre Plains/Desert por seis eixos climáticos, conjunto de biomas possíveis e preenchimento de todas as células quart de todas as seções de um chunk.
- Delimitação: o registry local ainda contém somente Plains e Desert; o dataset completo OverworldBiomeBuilder, demais definições de bioma e presets/codec da BiomeSource continuam pendentes. Não há seleção climática integrada a dimensões ou carregamento de mundo nesta etapa.


## Incremento — faixas climáticas vanilla do OverworldBiomeBuilder (24/11/2026)

- Portadas as faixas de temperatura (5), umidade (5), erosão (7), continentalidade (7), peaks-and-valleys (5) e weirdness (2) de Minecraft Java 26.3, incluindo os intervalos congelado/não congelado e as subdivisões de continentalidade costeira e interior.
- Adicionada a classificação diagnóstica por dimensão. Temperatura, umidade, erosão e continentalidade quantizam a coordenada climática antes de aplicar os limites superiores exclusivos; peaks-and-valleys compara o valor bruto com as fronteiras da função vanilla, sem quantização adicional.
- Reutilizada a função TerrainProvider.PeaksAndValleys já portada, evitando duplicação da fórmula.
- Testes cobrem todos os intervalos e fronteiras exatas, inclusive os rótulos de diagnóstico.
- Delimitação: este commit porta o contrato de intervalos e diagnóstico do builder. A emissão da tabela completa de ParameterPoint (oceanos, costas, slices de terreno e biomas subterrâneos), as definições dos biomas ausentes e sua resolução para holders ainda não estão implementadas. Nenhum placeholder de bioma é criado.
- Referência: [OverworldBiomeBuilder — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/biome/overworldbiomebuilder/).


## Incremento — distribuição completa de biomas Overworld 26.3 (24/11/2026)

- OverworldBiomeBuilder agora emite a tabela integral de Climate.ParameterPoint com resource keys vanilla, seguindo a ordem de geração 26.3: off-coast, slices inland e biomas subterrâneos.
- Portadas as matrizes OCEANS, MIDDLE_BIOMES, MIDDLE_BIOMES_VARIANT, PLATEAU_BIOMES, PLATEAU_BIOMES_VARIANT e SHATTERED_BIOMES, incluindo os picks de praia, badlands, savanna, slope, peaks e shattered variants.
- A superfície emite dois parameter points por região (depth 0 e depth 1); caves usam depth 0.2–0.9 e Deep Dark usa depth 1.1 com erosion nos dois primeiros índices.
- A API BuildParameterList<T> resolve cada resource key por callback antes de construir Climate.ParameterList<T> e sua R-tree; a emissão de chaves não cria holders ou definições de biomas inexistentes.
- Harness verifica 7.594 entradas, 3.795 pares de superfície em cada profundidade, as três regiões subterrâneas e a entrada bottom-only, zero offset e resultados da R-tree em Mushroom Fields, Deep Frozen Ocean e Deep Dark.
- Limites: o registry atual do Asteria2 ainda não contém a maioria das chaves vanilla emitidas, portanto o callback de resolução completa requer o port das definições e registros dos biomas. O builder de tabela não ativa a seleção completa no mundo ainda. O modo de debug special-case, spawnTarget/deepDarkRegion e conteúdos de geração dos biomas continuam etapas separadas.
- Referência de código: `0khs/minecraft_source_26.3/net/minecraft/world/level/biome/OverworldBiomeBuilder.java` (Minecraft Java 26.3).


## Incremento — registry dos 56 biomas Overworld 26.3 (24/11/2026)

- Registradas as 56 chaves vanilla usadas pelo OverworldBiomeBuilder, agora resolvíveis como holders reais no namespace `minecraft`; Plains e Desert preservam os IDs iniciais 0 e 1.
- Portados dos dados JSON 26.3 exclusivamente os campos de clima e efeitos visuais do domínio Biome: precipitação, temperatura base, modificador frozen, downfall, cores de água/folhagem/folhagem seca/grama e modificadores de cor da grama.
- Nenhum campo de natural mob spawns, entidade, carver, placed feature ou configuração de geração de bioma foi importado; o escopo de conteúdo segue sem mobs.
- O default key do registry mudou de `asteria:plains` para `minecraft:plains`, alinhando o defaulted registry às chaves emitidas pela tabela oficial.
- MultiNoiseBiomeSource.CreateVanillaOverworld agora resolve todas as chaves contra o registry e rejeita holders ausentes ou não ligados, produzindo o R-tree climático completo de holders vanilla.
- Testes verificam as 56 definições, a correspondência integral entre a tabela e as chaves registradas, seleção real por R-tree para Mushroom Fields/Deep Frozen Ocean/Deep Dark, modificadores térmicos/visuais e largura global dinâmica da palette com 56 holders.
- Delimitação: os registros ainda contêm apenas dados climáticos e efeitos visuais; BiomeGenerationSettings, carvers, placed features, atributos ambientais/sons e demais domínios específicos continuam separados. A seleção está disponível via CreateVanillaOverworld, mas o carregamento de mundo ainda precisa construir/usar essa fonte por dimensão.
- Dados baseados em `mj41/mc26-data`, branch `mc-26.3`, arquivos `data/minecraft/worldgen/biome/*.json`; foram lidos exclusivamente os campos descritos acima.


## Incremento — blocos de material requeridos pelo Overworld 26.3 (24/11/2026)

- Adicionados 27 blocos citados nas material rules oficiais de `minecraft:overworld`: bedrock, copper ore, granite, raw copper, gravel, deepslate iron ore, raw iron, tuff, powder snow, sand/sandstone, cinnabar, sulfur, coarse dirt, terracottas, red sand/sandstone, deepslate, ice/packed ice, snow block, mud, mycelium, podzol e calcite.
- Os 34 blocos atuais (incluindo air/void_air, stone, dirt, grass block, water e lava) agora usam chaves de entrada `minecraft:*`; o registry continua identificado como `asteria:block`, mas seu default element é `minecraft:air`.
- Preservada a ordem inicial de IDs dos sete blocos existentes; os 27 materiais novos são anexados depois. O harness valida as 34 entradas e as chaves representativas usadas pelas regras vanilla. BlockStateRegistry passa a conter 34 estados de produção e seis estados auxiliares dos testes.
- Propriedades portadas nesta fatia incluem MapColor, hardness/resistance, RequiresCorrectToolForDrops, friction, random ticks, dynamic shape e no-occlusion sempre que representáveis no BlockProperties atual.
- Delimitação: este commit registra estados de bloco usados por worldgen. Comportamentos especializados como areia/cascalho caindo, gelo derretendo, mycelium espalhando, snow/powder-snow e drops/XP continuam dependentes dos ports de FallingBlock/IceBlock/PowderSnowBlock/DropExperienceBlock e do sistema de ticks/entidades. Nenhuma funcionalidade de mobs foi adicionada.
- Dependências conferidas nos arquivos `data/minecraft/worldgen/material_rule/**/*.json` do dataset `mj41/mc26-data`, branch `mc-26.3`; foram coletadas apenas as chaves de estado usadas nos resultados de material rules.


## Correção de namespace dos registries de worldgen (24/11/2026)

- Os IDs de entrada para material-rule types, material-condition types e density-function types agora usam o namespace vanilla `minecraft`, sem renomear os registries locais (`asteria:worldgen/...`).
- Os holders de NormalNoise e DensityFunction usados pelo Overworld também passaram a usar resource keys `minecraft:*`, permitindo que as referências do NoiseRouter e das regras materiais 26.3 apontem diretamente para as entradas registradas.
- Testes de codecs foram atualizados para exigir os discriminadores e referências vanilla; namespaces genéricos de identificadores e streams posicionais customizados permanecem independentes.
- Esta correção alinha os IDs públicos dos dados de worldgen. A registry de material_rule ainda precisa receber os arquivos vanilla e seus holders; a árvore Overworld completa ainda não está em execução no carregamento de mundo.


## Incremento — condições nomeadas de material do Overworld 26.3 (24/11/2026)

- Registradas as oito condições vanilla referenciadas por chave na árvore de material do Overworld: `on_floor`, `under_floor`, `deep_under_floor`, `very_deep_under_floor`, `on_ceiling`, `under_ceiling`, `not_underwater` e `not_under_deep_water`.
- As condições são holders reais na registry `asteria:worldgen/material_condition`, com resource IDs `minecraft:*`; seus valores reutilizam os ports StoneDepthCondition e WaterCondition sem duplicar lógica.
- Parâmetros correspondem a VanillaMaterialConditions 26.3: profundidade secundária 0/6/30 nos floor checks, ceiling floor type apropriado e water checks (-1,0,false)/(-6,-1,true).
- BuiltInRegistries agora registra esses valores durante o bootstrap. Testes verificam todos os oito holders, os construtores exatos e limites de classificação de piso/água.
- Próximo passo: completar rule types como bandlands/ore_vein, os restantes registries de noises/density functions usados pelos veios e as regras de material referenciadas, antes de construir o holder `minecraft:overworld`.


## Incremento — clay bands e BandlandsRule vanilla (24/11/2026)

- Registrados os quatro blocos de terracota colorida usados pela distribuição badlands: yellow, brown, red e light_gray, com MapColor e propriedades de mineração vanilla.
- MaterialSystem agora gera os 192 estados de clay bands usando o fluxo posicional derivado do hash `minecraft:clay_bands`, com a mesma sequência de larguras, quantidades e probabilidades de faixas da referência 26.3.
- Adicionado o ruído `clay_bands_offset` à amostragem do MaterialSystem; GetBand aplica o offset ruidoso arredondado à coordenada Y e consulta a tabela circular de 192 entradas.
- MaterialRuleContext delega GetBand ao MaterialSystem, e BandlandsRule foi adicionada como singleton serializável `minecraft:bandlands` que consulta o material por coordenada.
- MaterialRuleCodecBootstrap inclui bandlands na posição da ordem vanilla entre block e sequence.
- Testes cobrem codec unitário/singleton, uso efetivo da regra no BuildTerrain, período vertical 192, determinismo via seed posicional e presença das variantes coloridas.
- O registry de blocos agora contém 38 entradas; os 38 estados de produção mais seis estados de fixture compõem 44 IDs no harness.
- Delimitação: o port ainda não adiciona os demais domínios badlands (pillar/roof surface extension) nem a árvore completa `minecraft:overworld`; o suporte a clay bands está pronto para a próxima parte da regra de superfície.


## Incremento — extensões de superfície Eroded Badlands e Frozen Ocean (24/11/2026)

- MaterialSystem agora carrega os seis ruídos específicos de badlands e icebergs já registrados nos dados de worldgen.
- Na entrada de cada coluna, consulta o bioma na altura superficial inicial. Para Eroded Badlands, aplica pilares antes de recalcular heightmap/gradientes; o topo usa os ruídos badlands_surface, badlands_pillar e badlands_pillar_roof com escalas/amplitudes vanilla.
- A extensão de Badlands só preenche ar acima do bloco padrão e aborta se encontrar água antes de alcançar o bloco-base; writes continuam restritos ao build height por meio do contrato de ChunkAccess.
- Após o scan genérico e avaliação das material rules, Frozen Ocean e Deep Frozen Ocean executam a extensão de iceberg. O port mantém as escalas/amplitudes dos três ruídos, a redução de dois blocos quando o bioma indica derretimento, os limites de sea level/minSurfaceLevel, a ordem dos dois draws iniciais e a avaliação condicional dos draws por bloco (1% no ar, 15% na água).
- Os icebergs produzem packed ice e uma camada superior limitada de snow block conforme o fluxo aleatório posicional por coluna.
- Testes end-to-end com seed fixa cobrem tops derivados de ruído dos pilares de Eroded Badlands, prevenção de pilares através de água e geração de packed ice/snow block em Frozen Ocean.
- Referência: [MaterialSystem — Minecraft Java 26.3](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/material/materialsystem/).

## Incremento — OreVeinRule vanilla 26.3 (24/11/2026)

- Adicionada a regra OreVeinRule, com os sete campos da especificação: ore_block, raw_ore_block, filler_block, raw_ore_chance, density, richness e filler_gap. O codec valida raw_ore_chance no intervalo [0, 1] e round-tripa estados de bloco e density functions inline/registradas.
- O tipo ore_vein foi adicionado ao registry de codecs depois de condition, preservando a ordem de bootstrap vanilla.
- Compile pré-amostra density e richness na extensão do volume do chunk; filler_gap é consultada apenas após o draw de richness. A avaliação por bloco usa factory posicional nomeada ore, recriada por coordenada, mantendo a ordem dos três draws e os short-circuits de Minecraft Java 26.3.
- Adicionados factories para as variantes Copper (copper_ore/raw_copper_block/granite) e Iron (deepslate_iron_ore/raw_iron_block/tuff), ambas com raw ore chance 0.02.
- Os testes de codec cobrem os sete campos, os limites de chance e as duas variantes. Testes end-to-end comparam os 256 blocos da camada de uma chunk contra os draws da factory posicional e cobrem density não positiva, minério, minério bruto, filler por baixa riqueza e filler_gap positivo.
- Adaptação local: a chave de random factory usa ResourceLocation.WithDefaultNamespace("ore"), portanto o namespace padrão do jogo local (asteria) substitui o default namespace da referência externa. Não são adicionadas dependências nem strings de namespace externo no algoritmo.
- Limites: as flags de debug globais de minério ainda não existem; a regra normal não depende delas. A rule foi registrada e pode ser serializada/compilada, mas ainda não foi inserida automaticamente numa configuração Overworld construída pelo runtime: o bootstrap dos NoiseGeneratorSettings/dados do Overworld e o carregamento de mundo continuam pendentes.
- Referência: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/material/rule/oreveinrule/

## Incremento — registry completo de material rules do Overworld 26.3 (24/11/2026)

- O bootstrap do registry `worldgen/material_rule` carrega os 37 recursos nomeados do conjunto Overworld 26.3: bedrock_floor, overworld root, ore veins, superfície, ramificações de superfície por bioma, under-biome surface e regras subterrâneas.
- O root registry é vinculado primeiro; os registries de blocos, biomas, noises, density functions, tipos de rule/condition e demais dependências são congelados antes do decode de material rules. A registry `MaterialRule` permanece aberta durante a resolução dos codecs e é congelada imediatamente após registrar os 37 valores.
- Os JSONs foram empacotados em três catálogos embutidos sem alterar os objetos das regras ou os seus resource keys. A inicialização reserva primeiro todos os holders; em seguida faz decode com `RegistryOps` sobre os registries built-in; por fim registra os valores, permitindo referências forward e verificando chaves ausentes no freeze.
- A ordem de bootstrap dos codecs foi ajustada para que MaterialRuleType e MaterialConditionType estejam registradas antes de decodificar os valores do registry material_rule. A registry de blocos, biomas, noises e density functions permanece anterior ao carregamento das regras.
- Testes verificam os 37 valores vinculados, as definições de Copper/Iron e a sequência raiz do Overworld, incluindo a ordem e identidade dos quatro holders nomeados e o ramo de superfície.
- Delimitação: `bedrock_roof`/`overworld_caves` não fazem parte deste catálogo; a árvore de material foi conectada à configuração built-in `minecraft:overworld` no incremento seguinte. O carregamento de mundo e a execução dessa configuração pelo runtime continuam pendentes.
- Fonte dos dados: `mj41/mc26-data`, branch `mc-26.3`, `data/minecraft/worldgen/material_rule/` e seu subdiretório `overworld/`.

## Incremento — configuração built-in NoiseGeneratorSettings do Overworld 26.3 (24/11/2026)

- A registry `worldgen/noise_settings` agora carrega `minecraft:overworld` do JSON oficial 26.3. O loader decodifica a configuração completa com `RegistryOps` após a registry de material rules e as dependências de density/noise terem sido congeladas.
- A configuração preserva `noise` min_y=-64/height=384, stone/water, router de oito canais, `material_rule=minecraft:overworld`, dois spawn targets do jogador, sea level 63, aquifers e nove debug functions. O campo `disable_mob_generation` do JSON de referência é ignorado e não foi modelado/importado.
- Corrigido o codec de paridade: `spawn_target` é obrigatório, inclusive quando vazio, e a chave booleana usa o nome `legacy_random_source` da fonte 26.3. `aquifers` e `debug_functions` continuam opcionais com defaults vanilla.
- Testes validam a entrada built-in, os oito IDs do NoiseRouter, o holder da árvore de material, os dois spawn targets, o aquifer e os limites/nomes dos campos required/optional.
- Limite atual: a configuração fica registrada e acessível a `NoiseBasedChunkGenerator`, mas ainda não há `Level/WorldLoader` que instancie um mundo, gere a região inicial e a exponha ao runtime Godot. `bedrock_roof`, `overworld_caves`, carvers/features, persistência e rendering continuam em etapas separadas.
- Fonte dos dados: `mj41/mc26-data`, branch `mc-26.3`, `data/minecraft/worldgen/noise_settings/overworld.json`; campo legado conferido em `NoiseGeneratorSettings.java` de `0khs/minecraft_source_26.3`.

## Incremento — OverworldWorld e cache síncrono de chunks (24/11/2026)

- Adicionado `OverworldWorld`, proprietário de um `NoiseBasedChunkGenerator` seeded e de um cache em memória indexado por `ChunkPos`.
- `GetChunk` gera sob demanda uma `ProtoChunk` e só a publica no cache depois que a etapa `TERRAIN` termina; solicitações concorrentes da mesma posição são serializadas por lock e retornam a mesma instância. O mesmo lock protege lookup e unload.
- `TryGetChunk`, `LoadedChunkCount` e `UnloadChunk` oferecem a primeira superfície de gestão do cache. Coordenadas horizontais fora dos limites de worldgen são rejeitadas.
- O construtor inicializa registries e usa automaticamente os limites Overworld -64/384 quando não é fornecido um `LevelHeightAccessor`. O overload de altura é mantido para harnesses e futuros contextos de dimensão.
- Testes cobrem cache inicialmente vazio, geração paralela idempotente por posição, status/bioma após geração, isolamento de posições distintas, unload e rejeição de coordenadas inválidas.
- Delimitação: este serviço retém apenas `ProtoChunk` até `TERRAIN`; não há `LevelChunk`, scheduler de `ChunkPyramid`, generation features/carvers/light, save/load nem integração Godot/rendering. O generation lock é deliberadamente síncrono para proteger o `RandomState` compartilhado; paralelismo por chunk depende de separar/validar o estado de amostragem.

## Incremento — consultas de bloco e fluido em OverworldWorld (24/11/2026)

- OverworldWorld agora implementa BlockGetter, fornecendo GetMinY/GetHeight e consultas GetBlockState/GetFluidState por BlockPos.
- Uma consulta dentro dos limites verticais resolve ChunkPos.Containing(position) e utiliza GetChunk, gerando sob demanda ou reutilizando a entrada do cache.
- Fora da altura construída, GetBlockState devolve VOID_AIR e GetFluidState devolve Fluids.EMPTY sem criar chunks.
- Testes verificam delegação para chunk em cache, geração por consulta na coordenada vizinha e short-circuit fora do build height.
- Delimitação: o mundo ainda não cobre block entities, ticks globais, lighting updates, colisão/raycast integrado nem LevelChunk; consultas de bloco/fluido geram chunks até TERRAIN.

## Incremento — consultas de bioma em OverworldWorld (24/11/2026)

- OverworldWorld agora expõe GetBiome(BlockPos), resolvendo a chunk sob demanda e consultando sua palette quart com as coordenadas convertidas por QuartPos.FromBlock.
- O retorno mantém a identidade do Holder<Biome> registrado na palette; o acesso não cria uma segunda fonte ou recalcula clima fora da etapa de geração.
- Teste compara a consulta do mundo com a consulta direta da mesma chunk e confirma que a entrada existente do cache é reutilizada.

## Incremento — consultas de heightmap em OverworldWorld (24/11/2026)

- OverworldWorld agora expõe GetHeight(Heightmap.Types, blockX, blockZ), resolve a chunk pela coordenada X/Z e delega à heightmap já mantida por ela.
- O teste compara a resposta do mundo com a mesma heightmap da ProtoChunk em cache e garante que não há um segundo caminho de cálculo.

## Incremento — snapshot das chunks carregadas de OverworldWorld (24/11/2026)

- Adicionado GetLoadedChunks, que retorna uma cópia read-only ordenada por coordenadas X/Z sob o lock do cache; consumidores percorrem o snapshot depois que o lock é liberado.
- Snapshots anteriores permanecem estáveis após novas cargas e unloads, sem expor o dicionário privado do mundo.
- Testes cobrem conteúdo inicial, ordenação determinística, estabilidade do snapshot e atualização de novas consultas após unload.

## Incremento — escrita de blocos em OverworldWorld (24/11/2026)

- OverworldWorld expõe SetBlockState(BlockPos, BlockState, flags=3), encaminhando flags e mutação à ProtoChunk da posição correspondente.
- A chamada devolve o estado anterior; se a identidade do estado mudar, a chunk é marcada como não salva para sinalizar dados mutados.
- Escritas fora da altura construída devolvem VOID_AIR sem sintetizar chunk. O teste confirma retorno do estado anterior, leitura do novo estado, dirty flag e short-circuit vertical.
- Delimitação: ProtoChunk ainda não despacha atualizações de vizinhos ou eventos de bloco; esse comportamento depende de LevelChunk/Level e do scheduler de ticks.

## Incremento — validação da altura completa do Overworld (24/11/2026)

- Adicionado um teste de integração usando o construtor padrão de OverworldWorld, sem LevelHeightAccessor reduzido.
- A verificação gera uma ProtoChunk com as 24 seções do intervalo vanilla -64..319 e consulta blocos e biomas nos dois extremos verticais.
- O teste confirma o status TERRAIN e a retenção da chunk no cache. Isso valida a altura completa de geração, mas ainda não mede o custo em runtime nem implementa o pipeline acima de TERRAIN.

## Incremento — tags vanilla de bloco para o registry suportado (24/11/2026)

- O bootstrap agora vincula as tags `blocks_motion_in_heightmap`, `blocks_motion_in_heightmap_no_leaves` e `causes_suffocation` de acordo com a composição vanilla 26.3, projetada sobre os blocos já registrados em Asteria.
- A projeção inclui os membros suportados das tags `blocks_motion_no_leaves`, `dirt`, `grass_blocks`, `sand`, `terracotta`, `ice` e `ores`, além dos valores diretos; leaves e fall_damage_resetting permanecem vazias porque ainda não há membros correspondentes no registry local.
- O harness confirma os membros incluídos e excluídos antes de exercer o ciclo de reload de tags. Os checks de worldgen não precisam mais depender de uma tag sintética contendo somente STONE.
- Fonte: `mj41/mc26-data`, branch `mc-26.3`, `data/minecraft/tags/block/blocks_motion*.json`, `dirt.json`, `grass_blocks.json`, `sand.json`, `terracotta.json`, `ice.json`, `ores.json`, `copper_ores.json` e `iron_ores.json`.

- Incremento adicional: os checks de NoiseBasedChunkGenerator deixaram de substituir as tags por uma lista sintética contendo somente STONE. O harness agora roda com o mesmo conjunto de tags inicializado pelo bootstrap; o teste de reload de tags restaura o conjunto built-in ao terminar.

## Incremento — inicialização do Overworld no runtime Godot (24/11/2026)

- `Main._Ready` mantém as verificações de bootstrap e então cria um `OverworldWorld` com seed fixa de desenvolvimento `132456789`, gerando a chunk `[0, 0]` até `TERRAIN`.
- A referência do mundo fica exposta na propriedade `Main.World`; o log registra seed, posição, total de seções, seções não vazias e índice de status.
- README atualizado para distinguir geração real de chunk do estado ainda não jogável. A cena ainda não renderiza blocos nem cria jogador/física.


## Incremento — visualização 3D diagnóstica da chunk de origem (24/11/2026)

- Adicionado `ChunkDebugMeshBuilder`, independente de Godot, para produzir uma triangle list de blocos cúbicos completos com render shape `MODEL`. A varredura usa o caminho de blocos não vazios de `ChunkAccess.FindBlocks`.
- Faces compartilhadas com blocos sólidos são omitidas. Para bordas horizontais, o consumidor pode fornecer apenas estados de chunks já carregadas; a função não provoca geração de vizinhas. Sem vizinha carregada, a borda permanece aberta.
- Cada vértice contém posição local à chunk, normal externa e RGB de `MapColor`. A ordem dos triângulos segue a convenção de faces frontais horárias da Godot, mantendo as normais externas. O uso de map color é exclusivamente diagnóstico e não representa texturas ou materiais finais.
- O harness verifica chunk vazia, cubo isolado, culling entre blocos adjacentes e verticais, fluidos não tratados como cubos sólidos, consulta de vizinha de borda e orientação de normais.
- `ChunkDebugRenderer` converte os dados em `ArrayMesh` via `SurfaceTool` e cria colisão estática trimesh para a geometria de diagnóstico; `Main` adiciona uma câmera e luz direcionais e mostra apenas a chunk [0, 0].
- Limitações intencionais: sem modelos de bloco, texturas/atlas, fluidos, transparência, animação, meshing greedy, vizinhança carregada pelo runtime, iluminação voxel, chunks adicionais, jogador ou interação. Não substitui o renderer definitivo nem altera os dados do mundo.


## Incremento — controlador em primeira pessoa e região de teste (24/11/2026)

- O runtime gera a origem e as oito chunks vizinhas imediatas (região 3×3), todas até `TERRAIN`, antes de gerar as malhas. Isso permite consultar somente vizinhas já carregadas para remover faces internas nas bordas.
- `ChunkDebugRenderer` aceita um callback de consulta que não gera chunks; a mesma malha diagnóstica produz o corpo estático trimesh por meio do helper da Godot.
- Adicionado `PlayerController` temporário com `CharacterBody3D`, cápsula, gravidade, aceleração no solo/ar, salto e mouse look. O `Main` registra WASD/setas e espaço; Esc libera o mouse e clique o captura novamente.
- A posição inicial procura uma superfície sólida seca próxima ao centro da chunk de origem, com espaço para a cápsula; se não existir, usa a altura sólida do centro.
- Essa região fixa não implementa streaming ou carregamento por distância. A colisão deriva apenas dos cubos completos representados na malha de diagnóstico; blocos não cúbicos, líquidos, modelos e interação ainda não têm representação física/visual.
- Não há inventário, quebra/colocação, respawn, persistência ou lifecycle completo do jogador. O controlador é somente uma etapa de validação do terreno e da física básica, não declara o mundo jogável completo.


## Incremento — streaming da região de renderização ao redor do jogador (24/11/2026)

- `ChunkStreamingController` calcula a chunk atual pela posição X/Z do jogador e atualiza a região quando cruza um limite de chunk. O raio inicial continua 1 (3×3); `ChunkRenderRegion` fornece ordem determinística do centro para fora e omite coordenadas inválidas de worldgen.
- As posições pendentes são processadas com orçamento de uma chunk por frame. A geração continua síncrona e usa o gerador/caches existentes; isto evita disparar nove gerações na mesma chamada de inicialização, mas ainda pode causar hitch no frame de uma geração cara.
- Chunks que saem do raio perdem somente seus nós `ChunkDebugRenderer` e colisões. Nenhuma chamada a `OverworldWorld.UnloadChunk` é feita: `ProtoChunk` e edições permanecem no cache em memória até existir save/load local e uma política de descarregamento segura.
- Novas chunks visíveis são registradas antes da construção da malha. Os renderers cardinais adjacentes são reconstruídos quando uma vizinha entra ou sai da região, fazendo o culling de faces acompanhar a vizinhança efetivamente renderizada, sem carregar chunks através do callback.
- O harness verifica que uma chunk modificada continua no cache e conserva o estado alterado depois que a nova região deixa de incluí-la. O teste de `ChunkRenderRegion` cobre região 1×1, 3×3, ordem centro-primeiro, coordenadas negativas, borda de worldgen e raio inválido.
- Limitações: o cache de dados cresce com a exploração; não há geração assíncrona/scheduler real, evicção, persistência, chunks completas/lighting ou renderer definitivo. O orçamento é por contagem, não por milissegundos.


## Incremento — interação direta com blocos no runtime de teste (24/11/2026)

- `PlayerController` usa `BlockGetter.Clip` com `ClipContext.Block.Outline` e sem seleção de fluidos para obter o bloco e a face atingida até 5 blocos à frente da câmera.
- Clique esquerdo remove instantaneamente o bloco atingido, exceto estados com destroy speed negativo; a mutação passa por `OverworldWorld.SetBlockState` e marca a chunk como não salva.
- Clique direito tenta colocar `Blocks.DIRT` na posição adjacente à face atingida. A posição precisa estar dentro da altura construída, ser ar/substituível e não intersectar o volume aproximado da cápsula do jogador.
- Após cada mutação, `ChunkStreamingController.RefreshAfterBlockChange` reconstrói a malha da chunk alterada e somente as vizinhas cardinais quando a alteração toca uma borda horizontal.
- O raycast reutiliza o DDA/clip de formas já portado e testado em `ChunkChecks`; não há um raycast de voxel paralelo dentro do controlador.
- Limitações: mineração instantânea, colocação fixa de terra, sem inventário/seleção de item, ferramentas, drops, partículas, animação, alcance configurável, cooldown ou atualizações de bloco completas. Continua sendo uma ferramenta de teste, não o sistema final de interação.


## Correção arquitetural — retirada do runtime diagnóstico (24/11/2026)

- Removidos `ChunkDebugMeshBuilder`, `ChunkDebugRenderer`, `ChunkRenderRegion`, `ChunkStreamingController` e `PlayerController`. A geometria cúbica com faces omitidas, o material PBR colorido por `MapColor`, o sol direcional, a movimentação, a interação simplificada e o gerenciamento próprio de região não eram ports dos sistemas equivalentes.
- A cena inicial agora contém somente o nó raiz; `Main` limita-se a inicializar os registries de conteúdo como bootstrap do Godot.
- Os testes exclusivos do mesh diagnóstico e do planejador de região foram removidos. Permanecem os testes dos subsistemas portados, incluindo formas voxel e clipping de blocos.
- Não há atualmente renderização de terreno nem iluminação visual no runtime. Isso é preferível a apresentar a luz de cena e a malha de diagnóstico como se fossem equivalentes ao jogo.
- Próxima cadeia de port: completar o motor de luz de bloco e céu, armazenamento por seção e integração com os dados reais das chunks; depois, portar a compilação/renderização de seções e sua ligação ao cliente.


## Incremento — DataLayer de iluminação 26.3 (24/11/2026)

- Portada `DataLayer` de `net.minecraft.world.level.chunk.DataLayer`: 16 camadas, 128 bytes lógicos por camada, 2.048 bytes, 4.096 valores de 4 bits, índice `y << 8 | z << 4 | x`, nibble baixo antes do alto, representação homogênea lazy, materialização, preenchimento, cópia e formatação de diagnóstico.
- A construção a partir de `byte[]` mantém a referência ao array recebido, valida o comprimento exato e a cópia da representação materializada duplica os bytes, preservando a semântica da referência.
- Testes de paridade cobrem defaults lazy, preenchimento, coordenadas e ordem dos nibbles, mascaramento do valor, alias/cópia de array, formatação e entrada inválida.
- Isso porta apenas a camada de dados. Propagação SKY/BLOCK, armazenamento por seção, fila de atualizações e ligação às chunks continuam pendentes.


## Incremento — DataLayerStorageMap de iluminação 26.3 (24/11/2026)

- Portada a estrutura genérica de mapa de seções de luz com cache de duas entradas e cópia polimórfica.
- Preservados os detalhes de 26.3: consulta somente armazena entradas encontradas; `SetLayer` e `RemoveLayer` não invalidam cache por conta própria; `CopyDataLayer` clona o `DataLayer`, substitui o valor e limpa o cache; `DisableCache` desativa as consultas em cache sem reconstruir o mapa.
- Testes cobrem lookup/cache de duas entradas, eviction, retorno de valores ausentes, semântica de invalidação, cópia de layer, cópia independente do mapa e cache desativado.
- Próxima dependência: `LayerLightSectionStorage` e as especializações SKY/BLOCK.


## Incremento — contratos de chunk para iluminação 26.3 (24/11/2026)

- Adicionados `LightLayer` com a ordem `SKY`, `BLOCK`, `LightChunk` e `LightChunkGetter` seguindo os contratos de acesso da referência.
- `ChunkAccess` implementa `LightChunk` usando seus métodos existentes de enumeração de fontes emissoras e armazenamento `ChunkSkyLightSources`; nenhum scanner paralelo foi criado.
- `OverworldWorld` implementa `LightChunkGetter`; a consulta retorna apenas chunks já presentes no cache e nunca aciona geração como efeito colateral. `GetLevel` expõe o próprio mundo como `BlockGetter`; a notificação permanece no método default sem efeito, conforme o contrato da interface.
- Testes cobrem enumeração vazia, identidade do armazenamento skylight, lookup/miss por coordenada, level associado e ordem dos canais.
- Ainda não há engines SKY/BLOCK nem processamento de propagação; estes contratos são a superfície de integração necessária para as próximas classes.


## Incremento — LayerLightSectionStorage 26.3 (24/11/2026)

- Portada a máquina de estados de armazenamento por seção: mapa de dados em atualização e snapshot publicado, cópia sob escrita, vizinhança 3×3×3, contagem de até 26 seções vizinhas, dados recebidos antes da inicialização, remoção adiada e retenção de dados por coluna.
- `SwapSectionMap` publica um mapa independente sem cache e encaminha as notificações de seções afetadas para `LightChunkGetter` usando o canal `LightLayer` correto.
- O mapa de fila usa `ConcurrentDictionary` como adaptação thread-safe do mapa sincronizado Java; as estruturas de estados e conjuntos de atualização permanecem confinadas ao ciclo do motor, como no fluxo de referência.
- Testes verificam codificação/limites do byte de estado, copy-on-write, isolamento do snapshot, endereçamento packed dos blocos, conjunto de seções afetadas, transições de seção e vizinhança, processamento de inconsistências, dados enfileirados/retidos, flags por coluna e publicação de notificações.
- As classes especializadas SKY/BLOCK e o algoritmo de propagação ainda serão portados em incrementos seguintes; a camada de armazenamento não faz cálculo de luz por conta própria.


## Incremento — BlockLightSectionStorage 26.3 (24/11/2026)

- Portada a especialização de armazenamento de luz emitida por blocos: consulta somente o snapshot publicado, retorna zero quando a seção não possui camada de dados e usa coordenadas locais X/Y/Z para ler o nibble.
- O mapa concreto clona o dicionário na publicação/cópia, compartilhando inicialmente as referências das `DataLayer`, como no mapa de armazenamento da referência.
- Testes cobrem seção ausente, isolamento entre dados em atualização e dados publicados, leitura após publicação, posições negativas e limites locais 0/15.
- Fonte consultada: `BlockLightSectionStorage.java` de Minecraft Java 26.3. O cálculo/propagação de luz e a ligação com o motor continuam pendentes.


## Incremento — SkyLightSectionStorage 26.3 (24/11/2026)

- Portada a especialização SKY: rastreamento de `currentLowestY` global e seção superior por coluna, leitura de luz em snapshots visíveis/atualização, retorno 15/0 acima das camadas armazenadas conforme `lightOnInSection`, e projeção vertical a partir do primeiro `DataLayer` disponível.
- A criação de dados preserva os dados enfileirados; abaixo da seção superior replica a primeira camada de 128 bytes em todas as 16 fatias Y, mantendo o caminho lazy para camadas homogêneas. Fora dessa região inicia com 15 quando a coluna tem fonte de céu ativa, ou 0 caso contrário.
- `onNodeAdded`/`onNodeRemoved` atualizam a seção superior por coluna, inclusive varrendo para baixo até encontrar a próxima camada ainda armazenada.
- Testes cobrem defaults de luz publicada/em atualização, flags por coluna, top/bottom section, projeção de luz, repetição de fatia, armazenamento homogêneo lazy, dados enfileirados e remoção da seção superior.
- Fonte consultada: `SkyLightSectionStorage.java` de Minecraft Java 26.3. A propagação de céu propriamente dita ainda depende de `SkyLightEngine` e do motor comum de atualização.


## Incremento — motor comum de filas de luz 26.3 (24/11/2026)

- Portados os contratos `LightEventListener`/`LayerLightEventListener` e o listener dummy sem luz. O adaptador não genérico `LightEngine` mantém os dois hooks já usados por `ProtoChunk`; o motor genérico concentra o fluxo de propagação e implementa a interface de listener.
- O motor comum mantém conjunto de blocos a verificar, filas FIFO de pares (posição/dados) para redução e aumento, cache de duas chunks incluindo resultados ausentes, fallback de consulta para bedrock quando a chunk não está disponível, e a ordem 26.3: verificar blocos, propagar reduções, limpar cache, processar inconsistências, publicar snapshots e então propagar aumentos.
- Portada a codificação compacta `QueueEntry`: bits de nível, seis direções, flags de shape vazio e origem de emissão; também os construtores para redução/aumento por direções e fontes SKY. Os auxiliares de shape de luz incluem `GetLightDampeningInto`, `GetOcclusionShape` e comparação de propriedades relevantes.
- Testes cobrem máscara e truncamento de nível, todos os bits de direção, omissão de uma direção, flags de emissão/shape, direções de fonte SKY, listener dummy e auxiliares de oclusão.
- As especializações BLOCK/SKY ainda precisam ligar suas transições de bloco e algoritmos próprios a este núcleo; o port comum não apresenta terreno nem produz iluminação visual por conta própria.


## Incremento — BlockLightEngine 26.3 (24/11/2026)

- Portada a especialização de propagação BLOCK: revalidação de nós, emissão condicionada à luz habilitada na coluna, filas de remoção e aumento, propagação em seis direções, atenuação mínima 1, regras de oclusão de faces e recuperação de emissores durante reduções.
- `PropagateLightSources` habilita o canal por coluna e enumera fontes diretamente da `LightChunk` existente; consulta de vizinhança permanece não geradora e usa bedrock para chunks ausentes, conforme o motor comum.
- Teste de integração cria um emissor de estado com nível 14, verifica valores 14/13/12 e remove a fonte para confirmar propagação da redução até zero; há verificações de borda ausente e coluna com BLOCK desabilitado.
- Fonte: `BlockLightEngine.java` de Minecraft Java 26.3. A especialização SKY e o agregador `LevelLightEngine` permanecem pendentes.


## Incremento — SkyLightEngine 26.3 (24/11/2026)

- Portado o motor SKY com atualização de fontes por coluna, remoção de fontes verticais obsoletas, adição de fontes até a seção superior, consulta às quatro colunas vizinhas, propagação com oclusão, redução/aumento, transmissão através de seções vazias no limite horizontal e prefill de seções vazias ao habilitar skylight.
- A rotina de fontes usa `ChunkSkyLightSources` de cada `LightChunk`; vizinhos ausentes usam a estrutura vazia do motor, sem forçar chunk generation. O motor compartilha as filas e o cache comum já portados.
- Testes de integração inicializam as fontes de céu em chunks abertas e verificam luz 15 no corpo, nas seções inferiores e acima do topo; também cobrem coordenadas negativas e vizinhos ausentes.
- Fonte: `SkyLightEngine.java` de Minecraft Java 26.3. A agregação SKY/BLOCK e o lifecycle que alimenta os engines ainda precisam ser concluídos.


## Incremento — LevelLightEngine 26.3 (24/11/2026)

- Portado o coordenador que reúne `BlockLightEngine` e `SkyLightEngine`, despacha os eventos de bloco/seção/coluna, executa as atualizações na ordem BLOCK e SKY e oferece listeners reais ou dummy por canal.
- Portada a API de integração: debug por canal, tipos de seção, filas de dados serializados, retenção de dados, brilho bruto como máximo entre bloco e céu após sky dampening, verificação de coluna ativa e faixa de seções com padding vertical de um nível em cada extremo.
- Para representar `LayerLightSectionStorage.SectionType` sem duplicar tipos genéricos em C#, a enumeração foi extraída para `LightSectionType`; os valores e a semântica permanecem `Empty`, `LightOnly`, `LightAndData`.
- Testes cobrem a instância `EMPTY`, canais desabilitados, listeners dummy, bounds/padding, operações no-op, combinação SKY/BLOCK, brilho bruto e atenuação, debug state, light column e dados de seção enfileirados.
- Ainda não existe pipeline de cliente/renderização consumindo `LightSection`/`DataLayer`; esta etapa completa o coordenador comum do sistema de iluminação sem reinstalar um renderer improvisado.

## Incremento — tarefas de lifecycle de iluminação por chunk 26.3 (24/11/2026)

- Portada a sequência síncrona equivalente de `ChunkStatusTasks.initializeLight` e `ChunkStatusTasks.light` em `ChunkLightingTasks`.
- `InitializeLight` exige status `FEATURES`, atualiza `ChunkSkyLightSources`, conecta o `LevelLightEngine` ao `ProtoChunk`, registra as seções não vazias e executa o lote PRE antes de aplicar `SetLightEnabled` e `RetainData(false)` no passo POST.
- `LightChunk` exige `INITIALIZE_LIGHT`, marca inicialmente `LightCorrect=false`, propaga as fontes quando a iluminação ainda não é válida, executa o lote de atualização e só então marca `LightCorrect=true`. A promoção do status persistido para `LIGHT` continua pertencendo ao scheduler, não à tarefa.
- O caller deve satisfazer a dependência de raio 1 definida em `ChunkPyramid.GenerationPyramid`: o anel 3×3 precisa ter chegado a `INITIALIZE_LIGHT` antes da iluminação do centro. As tarefas não geram vizinhos como efeito colateral.
- A adaptação para o mundo local single-player mantém a ordem PRE → um lote `RunLightUpdates` → POST do `ThreadedLevelLightEngine.runUpdate`, sem introduzir executor/threading que o runtime ainda não possui.
- Testes usam nove `ProtoChunk` reais e cobrem pré-condições de status, registro de seções, coluna inicialmente desabilitada, atualizações de blocos após `INITIALIZE_LIGHT`, propagação BLOCK/SKY e a separação entre `LightCorrect` e a promoção de status.
- A integração automática em `OverworldWorld` permanece pendente: seu gerador atual termina em `TERRAIN` e ainda não implementa `FEATURES`. Não se promove um chunk artificialmente para `FEATURES` apenas para acionar luz.
- Referência de comportamento: Minecraft Java 26.3, `ThreadedLevelLightEngine.initializeLight`, `ThreadedLevelLightEngine.lightChunk`, `ThreadedLevelLightEngine.runUpdate` e `ChunkStatusTasks.initializeLight/light`.

## Incremento — saída e máscara de carving 26.3 (24/11/2026)

- Portados os contratos `CarverOutput` e `CarvingMask`. A máscara marca posições por índice linear `y - minY + (z + (x << 4)) * height`, preservando a ordem Y/Z/X da referência.
- `Visit` agrupa bits adjacentes em segmentos e os divide por coluna, entregando X/Z e os limites Y inclusivos de cada intervalo. `IsEmpty` permanece O(1), sem percorrer toda a máscara.
- A representação usa `.NET BitArray` com expansão dinâmica para reproduzir o crescimento de `java.util.BitSet`; não existe limite de coordenadas adicional em `Carve`, portanto o mapeamento linear e seus aliases permanecem iguais ao contrato da referência. Índices negativos são rejeitados pela API de armazenamento .NET.
- Testes cobrem limites verticais inclusivos, máscara vazia, segmentos verticais contíguos, divisão de um run na borda entre colunas, indexação linear e rejeição de índice negativo.
- Esta etapa apenas registra a geometria. Ela não decide o estado que preencherá uma cavidade: a integração posterior deve reutilizar `Aquifer.ComputeSubstance`, nem deve marcar `ChunkStatus.TERRAIN` como concluído antes de rodar os carvers.
- Próxima dependência real do estágio `TERRAIN`: implementações `CaveWorldCarver`/`CanyonWorldCarver`, seleção de carvers pelas configurações de bioma e aplicação da máscara via aquifer. Só depois disso o port deve avançar para `FEATURES`.
- Referência: Minecraft Java 26.3, `CarverOutput.java` e `CarvingMask.java` (https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/chunk/carvingmask/).

## Incremento — geometria compartilhada de WorldCarver 26.3 (24/11/2026)

- Portada a interface `WorldCarver`: alcance padrão 4, método de carving por contexto/random/chunk de destino/chunk de origem/saída, teste de chunk inicial e codec de parâmetros.
- Portado `CarveEllipsoid`: early-out pela distância ao centro da chunk, cálculo e clamp dos índices X/Z, clamp vertical à `CarverOutput`, normalização em coordenadas elipsoidais e consulta ao `CarveSkipChecker` antes de marcar cada posição. Foram mantidas a expressão de borda e a iteração vertical estrita `worldY > minY` da referência.
- Portado `CanReach`, que compara a distância horizontal ao centro da chunk com o raio restante derivado de `totalSteps - currentStep` e da espessura.
- A adaptação C# expõe `MapCodec<WorldCarver>` no contrato em vez do wildcard Java `MapCodec<? extends WorldCarver>`; implementações concretas ainda fornecerão seus codecs e o registro dispatch. Nenhuma implementação fictícia foi adicionada.
- Testes cobrem ponto central, limites X/Z, clamp Y inclusivo no máximo e exclusivo no mínimo, skip predicate, rejeição por alcance e early-out para elipsoide distante.
- O estágio `TERRAIN` continua incompleto: ainda faltam `CaveWorldCarver`, `CanyonWorldCarver`, registries/dados de carvers, lista de carvers por bioma e a aplicação da máscara consultando `Aquifer.ComputeSubstance`.
- Referência: Minecraft Java 26.3, `WorldCarver.java` (https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/level/levelgen/carver/worldcarver/).

## Incremento — FloatProvider de worldgen 26.3

- Portado o contrato de `FloatProvider` e as quatro implementações de contexto livre da geração: `ConstantFloat`, `UniformFloat`, `ClampedNormalFloat` e `TrapezoidFloat`.
- Portados os codecs de cada tipo e o despacho `FloatProviders.CODEC`, incluindo a forma numérica abreviada para constantes e os validadores de faixa `Codec(min)` e `Codec(min, max)`.
- Registrado `worldgen/float_provider_type` com os identificadores vanilla `constant`, `uniform`, `clamped_normal` e `trapezoid`. O registry local usa o namespace de registry Asteria, enquanto as entradas preservam o namespace `minecraft`.
- A família é a de providers *context-free* usada por worldgen. Não é o registry `context_float_provider` introduzido em 26.3 para operações contextuais.
- Adicionados testes de codec, round-trip, validação de limites, rejeição de tipos desconhecidos e consumo determinístico do RNG. Os testes foram registrados no harness, mas ainda precisam ser executados em ambiente com .NET SDK.
- Este incremento entrega a dependência de float providers para os carvers, mas não os conecta ainda ao pipeline: `IntProvider`, `HeightProvider`, `CaveWorldCarver`, `CanyonWorldCarver`, settings de carver e aplicação da máscara continuam pendentes. `TERRAIN` permanece incompleto.
- Referência: `FloatProvider`, `FloatProviders`, `ConstantFloat`, `UniformFloat`, `ClampedNormalFloat` e `TrapezoidFloat` de Minecraft Java 26.3.

## Incremento — IntProvider de worldgen 26.3

- Portado o contrato `IntProvider` e os oito tipos context-free de Minecraft 26.3: `ConstantInt`, `UniformInt`, `BiasedToBottomInt`, `VeryBiasedToBottomInt`, `ClampedInt`, `WeightedListInt`, `ClampedNormalInt` e `TrapezoidInt`.
- Portados o despacho `IntProviders.CODEC`, o shorthand numérico para constantes, `NON_NEGATIVE_CODEC`, `POSITIVE_CODEC`, os validadores de faixa e os oito codecs específicos.
- `WeightedListInt` reutiliza os ports já existentes de `Weighted` e `WeightedList`, incluindo codec de distribuição não vazia e seleção por peso antes da amostragem do provider escolhido.
- Registrado `worldgen/int_provider_type`; os oito IDs dos providers permanecem no namespace `minecraft`. Esse registry de worldgen é distinto de `context_int_provider`, introduzido para operações contextuais.
- Adicionados testes de codecs, limites inclusivos, shorthand, distribuição ponderada, validação e sequência exata de chamadas ao RNG. Os testes estão registrados no harness, mas ainda precisam rodar em ambiente com .NET SDK.
- Com os increments de `FloatProvider` e `IntProvider`, estão disponíveis os dois contratos de números context-free exigidos pelos carvers. A implementação ainda não inclui `HeightProvider`, `CaveWorldCarver`, `CanyonWorldCarver`, settings/listas de carvers por bioma nem a aplicação de `CarvingMask` via aquifer. `TERRAIN` segue incompleto.
- Referência: interfaces e codecs `IntProvider`/`IntProviders` e as oito implementações de Minecraft Java 26.3.

## Incremento — HeightProvider de worldgen 26.3

- Portado o contrato contextual `HeightProvider`, reutilizando `VerticalAnchor` e `WorldGenerationContext` já existentes. O codec principal aceita tanto um anchor inline quanto uma variante registrada, reproduzindo `CONSTANT_OR_DISPATCH_CODEC`.
- Portadas as seis variantes de `HeightProviderType`: `ConstantHeight`, `UniformHeight`, `BiasedToBottomHeight`, `VeryBiasedToBottomHeight`, `TrapezoidHeight` e `WeightedListHeight`.
- Portados os campos exatos: `value`; `min_inclusive`/`max_inclusive`; `inner` opcional com default 1 nas duas variantes biased; `plateau` opcional com default 0; e `distribution` não vazia para listas ponderadas.
- As amostragens preservam as chamadas e a ordem do RNG 26.3: duas etapas para `BiasedToBottomHeight`, três para `VeryBiasedToBottomHeight`, dois intervalos inclusivos para `TrapezoidHeight`, e seleção ponderada antes da amostragem do provider interno.
- `UniformHeight` mantém a supressão por par de anchors das mensagens de intervalo vazio; as outras variantes mantêm o aviso por amostragem inválida. O comportamento de intervalo invertido retorna o mínimo resolvido conforme a referência.
- Registrado `worldgen/height_provider_type` com os seis IDs vanilla. `HeightProvider.CODEC` despacha por esse registry e usa `VerticalAnchor.CODEC` como forma abreviada de `ConstantHeight`.
- Adicionados testes de registry, shorthand, round-trip dos codecs, valores default, distribuição ponderada, sequências determinísticas de RNG e ranges inválidos. Os testes estão no harness, mas ainda precisam ser executados em ambiente com .NET SDK.
- Com `FloatProvider`, `IntProvider` e `HeightProvider`, as três famílias de providers numéricos/verticais necessárias aos carvers estão disponíveis. Ainda faltam `CaveWorldCarver`, `CanyonWorldCarver`, settings e associação de carvers por bioma e aplicação de `CarvingMask` via aquifer. Portanto `TERRAIN` ainda não está concluído.
- Referência: classes `HeightProvider`, `HeightProviderType` e as seis implementações em Minecraft Java 26.3.

## Incremento — CaveWorldCarver e CanyonWorldCarver 26.3

- Portados `CaveWorldCarver` e `CanyonWorldCarver` com codecs de configuração e implementação geométrica, usando os `FloatProvider`, `IntProvider` e `HeightProvider` adicionados nas fatias anteriores.
- `CaveWorldCarver` porta contagem de túneis, criação ocasional de salas, bifurcação no ponto médio, túneis íngremes, weird-thickness bias opcional, fatores de raio vertical/horizontal e corte por floor level. A ordem de amostragem do RNG e das sementes de túneis segue o fluxo 26.3.
- `CanyonWorldCarver` porta a forma configurável, perfil de largura por Y, suavização vertical, modulação de raio horizontal, atualização de raio vertical e poda por alcance. Os sete campos do `Shape` mantêm os nomes e tipos 26.3.
- Adicionado `worldgen/carver_type` com os codecs vanilla `minecraft:cave` e `minecraft:canyon`, mais `WorldCarvers.DIRECT_CODEC` para dispatch por `type`.
- Os codecs foram compostos internamente em grupos de até quatro campos devido ao limite atual do `RecordCodecBuilder`; a composição mantém um único objeto JSON com a mesma lista e ordem de campos da configuração 26.3.
- Adicionados testes de registry, defaults opcionais da caverna, round-trip dos dois carvers, validação de probability/count/thickness/width_smoothness e determinismo das posições escavadas para sementes e chunks iguais. Os testes foram registrados no harness, mas não foram executados sem o .NET SDK.
- Os carvers ainda não estão ligados ao `NoiseBasedChunkGenerator`: faltam `BiomeGenerationSettings` e a lista de carvers associada aos biomas, iteração vanilla da região 17×17 de chunks de origem, `CarvingMask` por etapa e aplicação da máscara pelo aquifer, incluindo `UNCARVABLE`, post-processing de fluidos e reparo de material sob grass/mycelium. Portanto `TERRAIN` continua incompleto e o status não foi promovido por este incremento.
- Referência: `CaveWorldCarver`, `CanyonWorldCarver`, `CanyonWorldCarver.Shape`, `WorldCarverTypes` e `WorldCarver` de Minecraft Java 26.3.

## Incremento — registry de carvers configurados do Overworld 26.3

- Adicionado `Registries.CARVER` e `BuiltInRegistries.Carver`, distinto de `CarverType`: o primeiro guarda os valores configurados; o segundo guarda os codecs de tipos concretos.
- Portados os três valores de dados oficiais de `Carvers.java` 26.3: `minecraft:cave`, `minecraft:cave_extra_underground` e `minecraft:canyon`. Probabilidades, anchors verticais, distribuições, parâmetros de forma e flags foram transcritos para os providers já portados, sem substituir as distribuições por constantes ou aproximações.
- A ordem de registro do bootstrap agora inicializa os registries de codecs e providers, registra os carvers configurados e só então registra os biomas. Isso permite que o próximo incremento associe holders canônicos aos dados de geração dos biomas.
- Adicionados testes para existência dos três registros, resolução chave-valor e todos os parâmetros configurados de Cave, Cave Extra Underground e Canyon. Os testes estão registrados no harness, mas não foram executados porque o ambiente continua sem .NET SDK.
- Naquele incremento, associação e execução dos carvers ainda estavam pendentes; ambas foram concluídas na sequência e estão registradas na seção de integração do `NoiseBasedChunkGenerator` abaixo.
- Referência: `Carvers.java` de Minecraft Java 26.3 para os três IDs e seus parâmetros configurados.

## Incremento — carvers por BiomeGenerationSettings 26.3

- Adicionada a parte de carvers de `BiomeGenerationSettings`: holders ordenados, conjunto vazio e `PlainBuilder.AddCarver/Build`. A implementação não inventa `PlacedFeature`: o componente de features, o codec completo e as APIs dependentes seguem pendentes até o port dessa classe.
- `Biome` agora armazena `GenerationSettings`. Construtores sem configuração explícita recebem `BiomeGenerationSettings.EMPTY`, enquanto os biomas embutidos recebem sua configuração Overworld.
- A factory `CreateDefaultOverworld` resolve os holders canônicos do registry configurado e acrescenta `CAVE`, `CAVE_EXTRA_UNDERGROUND`, `CANYON` na ordem vanilla. Isso corresponde a `BiomeDefaultFeatures.addDefaultCarversAndLakes` chamado por `OverworldBiomes.globalOverworldGeneration`; `deepDark` adiciona explicitamente os mesmos três carvers.
- Testes cobrem EMPTY, PlainBuilder, identidade/ordem dos holders e todos os biomas presentes no registry embutido. Os testes foram registrados no harness, mas ainda não executados sem .NET SDK.
- A associação passou a ser consumida pelo `NoiseBasedChunkGenerator` no incremento subsequente. O restante da integração foi concluído: 17×17 chunks de origem, seed de large feature por índice de carver, uma `CarvingMask` por chunk-alvo, aplicação via aquifer, tag `UNCARVABLE`, post-processing de fluidos e reparo de grass/mycelium. Consulte o incremento `TERRAIN` abaixo.
- Referência: `BiomeGenerationSettings.java`, `BiomeDefaultFeatures.addDefaultCarversAndLakes` e `OverworldBiomes.globalOverworldGeneration/deepDark` em Minecraft Java 26.3.

## Incremento — execução dos carvers no estágio TERRAIN 26.3

- `NoiseBasedChunkGenerator.BuildTerrainCore` agora executa `ApplyCarvers` após as regras de material de superfície e antes de persistir `ChunkStatus.TERRAIN`. O status não é promovido antes da conclusão da escavação e aplicação da máscara.
- O pass visita a região completa de 17×17 chunks de origem (-8…8 em X/Z), consulta o bioma de origem na coordenada quart `Y=0`, percorre os holders de `BiomeGenerationSettings.Carvers` na ordem de registro e reinicia `WorldgenRandom` com `setLargeFeatureSeed(seed + carverIndex, sourceX, sourceZ)` para cada carver. O RNG usado para essa semeadura é o `LegacyRandomSource` vanilla, independentemente do algoritmo do noise router.
- O resolver de biomas dos carvers é criado separadamente, sem reutilizar o resolver/cache que preenche a paleta do chunk. Uma `CarvingMask` reúne os volumes marcados pelos carvers do chunk-alvo; seus limites seguem `minGenY + 1` até `minGenY + genDepth - 1 - 7`.
- A aplicação consulta `BlockTags.Uncarvable` e chama o aquifer com densidade zero. Só grava o estado quando o aquifer retorna um valor não nulo; `null` preserva o bloco original. Estados fluidos são marcados para post-processing quando o aquifer solicita a atualização. A máscara mantém a regra lower-exclusive de `CarveEllipsoid`.
- Adicionado o port `MaterialSystem.TopMaterial` para recomputar a camada de material sob grass/mycelium após a abertura de uma cavidade, além da tag `uncarvable` e do vínculo atualmente suportado de `bedrock`.
- Teste integrado em `NoiseBasedChunkGeneratorChecks` reconstrói a máscara com as mesmas 17×17 seeds, consulta um aquifer ativo em cada posição marcada e verifica diretamente os dois comportamentos: retorno nulo preserva stone; retorno fluido não nulo é gravado. Também verifica os limites verticais protegidos, o avanço a `TERRAIN` após a etapa e a tag `Uncarvable` (bedrock sim, stone não).
- A aplicação da máscara foi comparada novamente com a fonte verbatim 26.3: o retorno nulo do aquifer não escreve bloco, e o sinalizador `hasGrass` é mantido durante o restante do segmento vertical visitado. Ambos os comportamentos estão preservados no código atual.
- Os testes permanecem registrados, mas não foram executados neste ambiente: .NET SDK/CLI não está instalado nem disponível no PATH. O status combinado do último commit consultado não retornou checks; isso não confirma uma execução bem-sucedida de CI.
- Com esta integração, o estágio de geração base `TERRAIN` (biomas, density/aquifer fill, material rules e carvers) está conectado em código para geração sem blending. O filtro de carving do `Blender` ainda não está representado no Asteria. Isso não completa a geração do mundo jogável: `FEATURES`, scheduler dos chunks, integração das tarefas de iluminação e renderer do cliente continuam pendentes.
- Referência: `NoiseBasedChunkGenerator.generateCarvers/applyCarvingMask`, `MaterialSystem.topMaterial`, `CarvingMask` e `WorldCarver` de Minecraft Java 26.3.

## Incremento — FeatureSorter, PlacedFeature e placement modifiers iniciais (26.3)

- Adicionados os contratos de `Feature`, `PlacedFeature`, `PlacementModifier`, `PlacementFilter` e `RepeatingPlacement`, com codecs de dispatch e holders para os registries Asteria de feature, placed feature e placement modifier.
- `BiomeGenerationSettings` agora armazena os grupos de `HolderSet<PlacedFeature>` por etapa de `GenerationStep.Decoration`, serializa os campos `carvers` e `features`, oferece `HasFeature` por identidade da instância e preenche grupos vazios ao adicionar uma feature em etapa posterior.
- Portado `FeatureSorter.BuildFeaturesPerStep`: índices estáveis por identidade de `PlacedFeature`, dependências entre features adjacentes declaradas pelos biomas, ordenação topológica por etapa e detecção/redução de ciclos com diagnóstico das fontes envolvidas.
- `NoiseBasedChunkGenerator.CreateOverworld` valida e memoiza a ordem global de features antes de retornar o gerador. Ciclos agora falham durante a configuração do gerador, não no meio da geração de chunks.
- Portado o fluxo LIFO de `FeaturePlacer`, com entrada comum e `PlaceWithBiomeCheck`. Os sete modificadores atualmente implementados são `InSquarePlacement`, `BiomeFilter`, `RarityFilter`, `CountPlacement`, `HeightRangePlacement`, `HeightmapPlacement` e `OffsetPlacement`, incluindo ordem de draws de RNG, validação de ranges e codecs.
- Os IDs dos tipos de placement modifier são registrados no namespace `asteria` (`asteria:in_square`, `asteria:biome`, `asteria:rarity_filter`, `asteria:count`, `asteria:height_range`, `asteria:heightmap`, `asteria:offset`) conforme a regra de namespace do repositório; os dados usam os nomes de campos da 26.3.
- Testes adicionados para merge/ordem/ciclo do sorter, limites e round-trip dos codecs, ordem X→Z do InSquare, sequência determinística da raridade/contagem, coordenadas de heightmap/height range, ordem RNG X→Y→Z do Offset, execução LIFO do FeaturePlacer e filtro de biome-check.
- O primeiro algoritmo concreto, `SimpleBlockFeature`, está registrado como `asteria:simple_block`, com provider, validação de sobrevivência e tick opcional. `NoiseBasedChunkGenerator.ApplyBiomeDecoration` executa o pass explícito de placed features, usando a janela 3×3 de biomas, a ordem global do sorter, `WorldgenRandom` sobre Xoroshiro e as seeds por step/índice. A operação ainda não está conectada ao scheduler/status pipeline de chunks; também não há `PlacedFeature` configuradas nos biomas Overworld.
- Os testes estão registrados no harness, mas continuam sem execução local: o ambiente não disponibiliza .NET SDK/CLI. O último status combinado consultado não trouxe checks, portanto não há validação de CI confirmada.
- Referência: `BiomeGenerationSettings`, `FeatureSorter`, `PlacedFeature`, `FeaturePlacer`, `PlacementFilter`, `RepeatingPlacement`, `InSquarePlacement`, `BiomeFilter`, `RarityFilter` e `CountPlacement` de Minecraft Java 26.3.

## Incremento — modificadores de altura e offset 26.3

- Adicionados `HeightRangePlacement`, `HeightmapPlacement` e `OffsetPlacement`, elevando o registry de modificadores de placement para sete tipos.
- `HeightRangePlacement` usa o codec contextual de `HeightProvider`, aceita a forma abreviada `VerticalAnchor` e amostra contra os limites de geração calculados pelo `PlacementContext`.
- `HeightmapPlacement` usa `Heightmap.Types.CODEC`, consulta o maior Y ocupado pelo heightmap selecionado e só emite a posição quando esse Y é estritamente maior que `minY`.
- `OffsetPlacement` porta os campos 26.3 `x`, `y`, `z`, cada um limitado a -16…16, e consome os providers nessa ordem. Os helpers horizontal, vertical, triangular, por direção e acima preservam as distribuições e defaults vanilla.
- Adicionados testes para dispatch dos sete IDs Asteria, shorthand de HeightProvider, codec de Heightmap.Types, limite dos offsets, resolução de altura contextual e ordem RNG X/Y/Z.
- O pass de decoração por placed features existe como chamada explícita e os contratos/codec de `SimpleBlockFeature` estão portados. A etapa `FEATURES` ainda não é executada pelo lifecycle de chunks: a pirâmide exige `TERRAIN` nos anéis 0–1 e `STRUCTURE_STARTS` nos anéis 2–8, mas a geração de starts e o scheduler que monta essa janela ainda não foram portados. Também faltam dados de placed features por bioma, algoritmos concretos adicionais e modificadores necessários para a distribuição completa do Overworld.
- Os testes continuam registrados, porém não executados localmente por ausência do .NET SDK/CLI; não há resultado de CI confirmado.
- Referência: `HeightRangePlacement`, `HeightmapPlacement`, `OffsetPlacement`, `HeightProvider`, `Heightmap.Types` de Minecraft Java 26.3.

## Estado atual — decoração de biomas e primeira feature concreta (24/11/2026)

- `FeatureSorter`, `BiomeGenerationSettings` e `PlacedFeature` agora compõem a lista ordenada de features por `GenerationStep.Decoration`. Os cycles entre biomas são rejeitados durante `CreateOverworld`.
- `FeaturePlacer` executa a cadeia de modificadores em ordem LIFO. Sete tipos de placement modifier estão registrados: quadrícula, filtro de bioma, raridade, contagem, range de altura, heightmap e offset.
- `SimpleBlockFeature` é o primeiro algoritmo concreto registrado. O ramo comum usa `BlockStateProvider`, `BlockState.CanSurvive`, escrita com flags 2 e, opcionalmente, agenda o bloco que efetivamente ficou na posição para `gameTime + 1`. Os ramos especiais de double plant e mossy carpet continuam pendentes porque essas famílias de blocos não existem no Asteria.
- `ApplyBiomeDecoration` implementa a operação explícita de decoração: recolhe os biomas da janela 3×3, escolhe os índices globais ordenados, usa `WorldgenRandom` sobre `XoroshiroRandomSource` e executa a seed de decoração e a seed por feature/etapa. Esse RNG é Xoroshiro em 26.3, independentemente do algoritmo usado para amostragem de terreno. Ainda não foi conectado ao scheduler de chunks.
- `WorldGenRegion` fornece a janela de dependências da etapa, permite leituras apenas de chunks com status suficiente e limita escrita ao raio da etapa. O `BiomeManager` aplica o zoom fuzzy 3D e a seed obfuscada tanto na região quanto em `OverworldWorld.GetBiome`; o acesso quart direto das palettes continua separado.
- Limite estrutural que impede promover `FEATURES`: `ChunkPyramid` exige `STRUCTURE_STARTS` até raio 8, mas o gerador de starts e o scheduler de `ChunkStep` ainda não existem. Não se falsificam esses status nem se executa a etapa fora da janela de dependências.
- Os testes de `FeatureSorter`, modifiers, `WorldGenRegion`, decoração vazia, `BiomeManager` e `SimpleBlockFeature` estão registrados em `BlockRegistryChecks`. Não foram executados: o ambiente não tem .NET SDK/CLI; o último status de CI consultado estava vazio, sem confirmação de execução.
- Permanecem fora desta fatia: placed features de produção nos biomas, mais algoritmos de feature/modifiers, `StructureStarts`, scheduler da pirâmide, integração automática de iluminação e renderer do cliente.


## Incremento — separação de BIOMES/TERRAIN e regressões de iluminação (24/11/2026)

- `NoiseBasedChunkGenerator.CreateBiomes` agora preenche a paleta quart e confirma `ChunkStatus.BIOMES` sem materializar terreno. `BuildTerrainCore` preserva uma paleta produzida por uma etapa BIOMES anterior e só executa o preenchimento/avanço quando o chunk ainda não alcançou esse status.
- `OverworldWorld.GetChunk` executa explicitamente `CreateBiomes` antes de `BuildTerrain`. A geração permanece síncrona, sem promover status de FEATURES, INITIALIZE_LIGHT, LIGHT ou FULL artificialmente.
- `NoiseBasedChunkGeneratorChecks` verifica que BIOMES pode ser concluído enquanto as seções ainda estão vazias, que todas as 64 posições quart por seção recebem holders válidos e que a construção posterior do terreno preserva a mesma paleta.
- Corrigida a publicação de iluminação em `LightEngine.RunLightUpdates`: as filas de reduções e aumentos são processadas antes de limpar o cache, marcar inconsistências e trocar o mapa visível. O teste combinado confirma que um lote deixa os dois canais sem trabalho pendente.
- O teste de carving agora usa floodedness espacialmente variável para exercitar ambos os resultados do aquifer (retorno nulo preserva o bloco e resultado fluido é gravado). A comparação de biomas nos limites verticais também foi ajustada para testar separadamente fuzzy zoom de `World.GetBiome` e lookup quart direto de `ChunkAccess.GetNoiseBiome`.
- Validação GitHub Actions: execução `45520784631` no commit `df35a4016e405655e01e7d60241df8272fdbdfdc` concluída com sucesso; `dotnet run --configuration Release --project tests/BlockRegistryChecks/BlockRegistryChecks.csproj` e `dotnet build --configuration Release Asteria2.csproj` passaram. O .NET local não foi executado nesta sessão; a validação acima foi realizada no CI.
- Bloqueio seguinte: `ChunkStep`/`ChunkPyramid` ainda não executam tarefas; não há scheduler/holders que montem janelas válidas de dependências. Também não há geração de `STRUCTURE_STARTS` e referências de estruturas, necessárias no raio 8 antes de FEATURES. Por isso a decoração, a inicialização/propagação automática da iluminação e a promoção a FULL permanecem desconectadas do fluxo de geração. O runtime Godot continua bootstrap-only, sem mundo visível ou cena jogável.

## Incremento — contrato de tarefas de ChunkStep (26/11/2026)

- Adicionado `ChunkStatusTask` e `ChunkStep.Apply`. A etapa chama a tarefa configurada e só promove o `ProtoChunk` retornado ao status-alvo depois que a tarefa termina com sucesso. Se o chunk de entrada já alcançou o alvo, a tarefa continua sendo executada, mas a etapa não repete a promoção.
- `ChunkStep.Builder.SetTask` permite associar a tarefa por etapa. Os passos estáticos de `ChunkPyramid` ainda não recebem delegates enquanto a implementação correspondente (especialmente estruturas e carregamento) não existir. `Apply` falha explicitamente quando o delegate está ausente; não há pass-through implícito que marque etapas não implementadas como concluídas.
- Criado `WorldGenContext` para expor os serviços já existentes do runtime local: `OverworldWorld`, `NoiseBasedChunkGenerator` e `LevelLightEngine`. Ele materializa `WorldGenRegion` a partir de um snapshot retangular de chunks já resolvidos.
- `OverworldWorld` agora é dono de uma única instância de `LevelLightEngine`, exposta ao contexto de worldgen. Isso ainda não liga automaticamente chunks às tarefas de iluminação: `ChunkLightingTasks.InitializeLight/LightChunk` continuam exigindo uma chamada explícita na etapa correta.
- Adaptação documentada: no runtime single-player síncrono atual, `ChunkStatusTask` recebe `StaticCache2D<ChunkAccess?>` com chunks já resolvidos, em vez de `StaticCache2D<GenerationChunkHolder>` com futures/claims do servidor. O modelo completo de holder, contagem de referências, cancelamento/resolução de futures e scheduler não está implementado; não se deve tratar este snapshot como substituto desse sistema.
- Testes em `ChunkPyramidChecks` cobrem execução e contexto recebidos, criação de `WorldGenRegion`, promoção após sucesso, execução sem promoção quando o chunk já alcançou o alvo, propagação de falha sem promoção e rejeição de etapas sem tarefa.
- Validação GitHub Actions: execução `45521912978` no commit `b6f2c928def5b83a38b852bdd19cba86106077e4` concluída com sucesso. O harness `BlockRegistryChecks` e `dotnet build --configuration Release Asteria2.csproj` passaram. O .NET local não foi executado nesta sessão.
- Próxima dependência: portar o equivalente de `GenerationChunkHolder` e o scheduler de `ChunkGenerationTask` para construir janelas válidas e executar tarefas uma única vez. A geração de `STRUCTURE_STARTS`/`STRUCTURE_REFERENCES` e seu modelo de estruturas continuam ausentes; por isso os delegates da pirâmide permanecem deliberadamente sem registro, e FEATURES/LIGHT/FULL não foram conectados ao fluxo do mundo.

## Incremento — resultado tipado de geração de chunks (26/11/2026)

- Adicionado `ChunkResult<T>` com resultados `Success<T>` e `Failure<T>`, fábricas `Of`/`Error`, avaliação lazy do fornecedor de erro, `IsSuccess`, `OrElse`, `GetError`, `IfSuccess`, `Map` e `OrElseThrow`.
- O tipo preserva a distinção entre operação concluída com valor e operação que falhou/foi descarregada, sem obrigar o holder a converter todas as falhas em exceções. O texto de erro continua lazy e é reavaliado a cada consulta, como na referência.
- `ChunkResultChecks` cobre sucesso, mapeamento, consumo condicional, fallback, erro lazy, propagação da falha pelo `Map`, exceção fornecida e fallback para valor nulo. O harness executa os testes junto às verificações de chunk.
- Validação GitHub Actions: execução `45522245929` no commit `7e77eec090e04e456787fec97db9437c3ec20423` concluída com sucesso; `BlockRegistryChecks` e `dotnet build --configuration Release Asteria2.csproj` passaram. O .NET local não foi executado nesta sessão.
- Próxima etapa: portar `GenerationChunkHolder` e `ChunkGenerationTask` usando esse resultado tipado, preservando futures por status, aquisição atômica de etapa, contagem de claims e cancelamento/reagendamento. O sistema de tickets do servidor deve ser avaliado separadamente para uma adaptação single-player explícita; não foi substituído por um scheduler fictício nesta etapa.

## Incremento — núcleo de estado de GenerationChunkHolder (24/11/2026)

- Adicionado o núcleo de estado de `GenerationChunkHolder`: validação de posição, teto de status permitido, um future por status da cadeia local, resultado compartilhado de chunk descarregado e consulta ordenada de todos os futures.
- A criação do future usa compare-and-exchange e verifica novamente o teto após publicar o novo future. Isso fecha a corrida entre solicitar um status e o scheduler reduzir o status permitido.
- Portados os comportamentos centrais de conclusão e descarte: concluir um future já existente, rejeitar uma segunda conclusão bem-sucedida, aguardar a remoção de um future que foi concluído como descarregado e limpar o slot para permitir uma nova solicitação. O intervalo de limpeza é exclusivo no limite inferior e inclusivo no superior.
- Portada a aquisição atômica do trabalho por status: somente a transição a partir do pai imediato pode iniciar uma etapa; uma etapa já iniciada ou anterior não inicia novamente, e uma transição que pula o pai falha explicitamente.
- Incluídas as consultas de chunk presente, chunk mais recente, status persistido e status mais recente, preservando o fallback para o pai quando a etapa reivindicada ainda não publicou um chunk.
- A contagem de referências de geração cria uma dependência de save no primeiro claim, compartilha essa dependência entre claims adicionais e a completa no último release. O estado inicial permanece concluído; liberar mais referências do que foram adquiridas lança erro.
- `GenerationChunkHolderChecks` cobre posição fora da borda, futuro bloqueado pelo teto, identidade por status, concorrência na criação, publicação e consulta, conclusão duplicada, descarte e substituição de futures, aquisição de status e sincronização por contagem de referências.
- Adaptação explicitamente limitada: `AddSaveDependency(Task)` é o ponto abstrato de integração com o mecanismo de salvamento. `SetHighestAllowedStatus` recebe um teto já calculado; a derivação por ticket e a atualização coordenada com o scheduler ainda não existem. O holder ainda não implementa `scheduleChunkGenerationTask`, `applyStep`, associação/cancelamento de `ChunkGenerationTask`, reagendamento, substituição por `ImposterProtoChunk`, `ChunkLevel` ou níveis de fila/ticket.
- A classe é, portanto, apenas o núcleo de estado e concorrência do holder. Ela ainda não executa a pirâmide nem transforma o runtime em um gerador jogável. O próximo port é o ciclo de vida de `ChunkGenerationTask` e sua interface `GeneratingChunkMap`, seguido da integração concreta com o mundo e com a política de tickets single-player.
- Validação GitHub Actions: execução `45523251910` no commit `270c5f4c930967b9f7018a3ce05f3d2a644a54f8` concluída com sucesso; `BlockRegistryChecks` e `dotnet build --configuration Release Asteria2.csproj` passaram. O ambiente local não foi usado para executar o .NET nesta sessão.
- Referência: `GenerationChunkHolder.java` e `ChunkResult.java` de Minecraft Java 26.3.

## Incremento — ChunkGenerationTask e contrato GeneratingChunkMap (24/11/2026)

- Adicionado o contrato `GeneratingChunkMap` com os cinco métodos da interface 26.3: aquisição/liberação de claims de geração, aplicação de etapa, criação de tarefa e despacho de tarefas pendentes. O retorno assíncrono usa `Task` como adaptação de `CompletableFuture`.
- O holder agora guarda a tarefa ativa e implementa `ScheduleChunkGenerationTask`: devolve o future existente, evita nova tarefa quando a solicitação já está coberta e substitui a tarefa quando chega uma solicitação para status superior.
- Adicionado `UpdateHighestAllowedStatus` que recebe um teto de status já calculado. Quando ele cai, falha/remove os futures do intervalo acima do novo teto e, se houver tarefa ativa, solicita ao mapa uma nova tarefa para o maior future ainda pendente; a tarefa anterior é marcada para cancelamento.
- `GenerationChunkHolder.ApplyStep` impede status não permitidos, usa o claim atômico pai→filho e compartilha o future existente quando outro chamador já reivindicou a etapa. Sucesso publica o chunk no future por status.
- Exceções de `ApplyStep` são armazenadas como `ChunkResult.Error(exception.ToString())`, para que a camada receba falha tipada e entre no caminho de cancelamento/liberação. Adaptação ainda incompleta: o projeto não tem o mecanismo `CrashReport` + `BlockableEventLoop.relayDelayCrash` do servidor; o erro .NET permanece serializado em texto no resultado, sem replicar essa infraestrutura de crash report.
- Adicionado `ChunkGenerationTask`: adquire previamente a janela quadrada calculada pela pirâmide de geração; percorre status em camadas com X externo/Z interno; escolhe a pirâmide de loading ou generation por chunk e camada a partir do status persistido; testa a possibilidade de carregar sem geração com as dependências da pirâmide de loading; aguarda futures da camada em ordem LIFO; cancela ao receber falha; e libera cada claim ao chegar ao alvo ou ser cancelada.
- Testes verificam o passo EMPTY imediato, yield/resume com future incompleto, falha tipada e liberação, janela FULL de 23×23 (529 holders), ordem X→Z, expansão para status superior, queda do teto de status, limpeza de futures e cancelamento/release das tarefas substituídas.
- CI GitHub Actions: execução `45524186625` no commit `1c400bea6547f15b40c41b4e72c78d27c8e390c9` concluída com sucesso; `BlockRegistryChecks` e build Godot passaram.
- Limites atuais: `GeneratingChunkMap` ainda é apenas interface, e o mapa de teste usado pelo harness não é um mapa de runtime. Não há implementação real de aquisição a partir de um mapa de chunks, armazenamento/carregamento do estágio EMPTY, despacho/reentrada de tarefas, executor priorizado ou tickets. O cálculo de `highestAllowedStatus` continua externo; `UpdateHighestAllowedStatus` aceita o resultado já calculado. As tarefas reais de `ChunkPyramid` continuam sem delegates e portanto `ChunkStep.Apply` falha explicitamente. Consequentemente, esse incremento porta a máquina de coordenação, mas ainda não faz o mundo gerar ou carregar chunks jogáveis.
- Próxima dependência para execução real: implementar o mapa single-player que satisfaça `GeneratingChunkMap` com armazenamento e política explícita de solicitação/status; integrar tarefas reais para EMPTY, STRUCTURE_STARTS, STRUCTURE_REFERENCES, BIOMES, TERRAIN, FEATURES, INITIALIZE_LIGHT, LIGHT e FULL. O sistema de tickets do servidor não deve ser substituído por níveis inventados; a adaptação single-player precisa expor claramente como o alvo e o teto permitido são definidos.
- Referência: Minecraft Java 26.3 `ChunkGenerationTask.java`, `GenerationChunkHolder.java`, `GeneratingChunkMap` e `ChunkMap.applyStep`.

## Incremento — tarefa de status BIOMES (24/11/2026)

- Adicionado `ChunkStatusTasks.GenerateBiomes`, adaptação síncrona da tarefa `ChunkStatusTasks.generateBiomes` de Minecraft Java 26.3. Ela valida o status-alvo, exige que a cache contenha a instância exata do chunk central e exige um `ProtoChunk`; em seguida chama `NoiseBasedChunkGenerator.CreateBiomes` e devolve o mesmo chunk.
- A tarefa foi ligada somente a `GenerationPyramid.BIOMES`. A execução real popula a paleta quart de biomas e os heightmaps que pertencem a esse estágio; a promoção do status permanece sob responsabilidade do gerador/contrato de `ChunkStep.Apply`.
- `LoadingPyramid.BIOMES` continua sem tarefa: a leitura de chunks persistidos, upgrade/parse e caminho de carga de `EMPTY` ainda não foram implementados. Não foi usado pass-through implícito para fazer um chunk carregado parecer pronto.
- Testes `ChunkStatusTasksChecks` verificam o delegate ligado apenas na pirâmide de geração, a execução positiva através de `ChunkStep.Apply`, entradas efetivamente preenchidas na paleta quart e a rejeição de uma cache que não contém o chunk central exato.
- Limite de integração: esta tarefa ainda não pode ser alcançada pelo scheduler de runtime, porque `STRUCTURE_STARTS` e `STRUCTURE_REFERENCES` anteriores continuam sem tarefas. `TERRAIN` também não foi ligado: a tarefa vanilla depende do contexto de estruturas/Beardifier e do conjunto de biomas coletado na região; a infraestrutura de estruturas ainda não foi portada. `FEATURES`, iluminação e `FULL` permanecem igualmente bloqueados pelas dependências reais descritas acima.
- Validação GitHub Actions: execução `45524855223` no commit `e4eddcd80ea5baa800e3a554a91c79e808e53089` concluída com sucesso; `BlockRegistryChecks` e build Godot passaram.
- Referência de implementação: Minecraft Java 26.3 `ChunkStatusTasks.generateBiomes` e `ChunkGenerator.createBiomes`.

## Incremento — tarefa de status BIOMES (24/11/2026)

- Adicionado `ChunkStatusTasks.GenerateBiomes`, adaptação síncrona da tarefa `ChunkStatusTasks.generateBiomes` de Minecraft Java 26.3. Ela valida o status-alvo, exige que a cache contenha a instância exata do chunk central e exige um `ProtoChunk`; em seguida chama `NoiseBasedChunkGenerator.CreateBiomes` e devolve o mesmo chunk.
- A tarefa foi ligada somente a `GenerationPyramid.BIOMES`. A execução real popula a paleta quart de biomas e os heightmaps que pertencem a esse estágio; a promoção do status permanece sob responsabilidade do gerador/contrato de `ChunkStep.Apply`.
- `LoadingPyramid.BIOMES` continua sem tarefa: a leitura de chunks persistidos, upgrade/parse e caminho de carga de `EMPTY` ainda não foram implementados. Não foi usado pass-through implícito para fazer um chunk carregado parecer pronto.
- Testes `ChunkStatusTasksChecks` verificam o delegate ligado apenas na pirâmide de geração, a execução positiva através de `ChunkStep.Apply`, entradas efetivamente preenchidas na paleta quart e a rejeição de uma cache que não contém o chunk central exato.
- Limite de integração: esta tarefa ainda não pode ser alcançada pelo scheduler de runtime, porque `STRUCTURE_STARTS` e `STRUCTURE_REFERENCES` anteriores continuam sem tarefas. `TERRAIN` também não foi ligado: a tarefa vanilla depende do contexto de estruturas/Beardifier e do conjunto de biomas coletado na região; a infraestrutura de estruturas ainda não foi portada. `FEATURES`, iluminação e `FULL` permanecem igualmente bloqueados pelas dependências reais descritas acima.
- Validação GitHub Actions: execução `45524855223` no commit `e4eddcd80ea5baa800e3a554a91c79e808e53089` concluída com sucesso; `BlockRegistryChecks` e build Godot passaram.
- Referência de implementação: Minecraft Java 26.3 `ChunkStatusTasks.generateBiomes` e `ChunkGenerator.createBiomes`.
