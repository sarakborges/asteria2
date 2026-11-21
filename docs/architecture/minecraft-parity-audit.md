# Auditoria de paridade — Asteria 2 / Minecraft Java 26.3

## Escopo e baseline

- Repositório: `sarakborges/asteria2`
- Branch auditada: `feature/block-registry`
- Commit-base: `968aa709c4c20e440b5af246044056b0c0d3a822`
- Inventário: 71 arquivos versionados, incluindo 50 arquivos C# de produção e um arquivo C# de testes.
- Referência: Minecraft Java Edition 26.3, usando o código-fonte decompilado e os contratos observáveis das classes equivalentes.

Esta auditoria cobre todo o conteúdo atualmente existente no repositório. “Paridade confirmada” significa que o comportamento que Asteria implementa foi comparado com o membro correspondente da referência; não significa que Asteria já implemente o jogo inteiro.

## Resultado executivo

Os algoritmos centrais já portados — identificadores, registries, holders, estados genéricos, estados de bloco e fluido, mapas de IDs, paletas e armazenamento compactado — correspondem ao comportamento observado no Minecraft 26.3 no escopo atualmente modelado. Os domínios de fluido e seção de chunk ainda são parciais; os limites estão descritos abaixo.

A principal lacuna deixou de ser a ausência total de fluidos: o núcleo de `Fluid`/`FluidState` e os contadores de seção já existem. Ainda faltam simulação, mundo, biomas, geração, iluminação, rendering, física, entidades, rede e persistência.

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
| `Direction` / `Direction.Axis` / `Plane` | `Direction` | Parcial: seis direções, vetores inteiros, IDs 2D/3D, eixo, plano, enumeração, conversão de yaw, rotações ortogonais e nearest-direction. APIs que dependem de entidade, matriz, RNG, vetor real e codecs permanecem pendentes. O modelo C# usa objetos singleton por identidade em vez de enum, para preservar os membros com dados. |
| `Position` / `Vec3` | `Position` / `Vec3` | Parcial: factories por `Vec3i`, aritmética, produto escalar/vetorial, distância, normalização, rotações com tabela Mth, alinhamento, projeção, igualdade/hash e finitude. Codec, rede, RNG, `Vec2` e JOML permanecem pendentes; `Position` tem nomes de acesso adaptados ao C#. |

| `Mth` | `Mth` | Parcial: tabela trigonométrica SIN de 65536 entradas, `Sin`, `Cos`, `Floor`, `Lerp`, `Square`, `LengthSquared` e `Length`, cobrindo a dependência portável de `Direction`/`Vec3`. O restante da utilitária numérica não foi introduzido. |
| `ChunkSection` | `LevelChunkSection` | Parcial: estados de bloco, contagem non-air, `FluidCount`, `TickingFluidCount`, `HasFluid`, lookup de fluido, ticking agregado, cópia e recálculo. Biomas e codecs de seção ainda ausentes. |
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
- Modelo de biomas, quart storage, biome resolver e armazenamento de biomas em seções.
- Lighting engine, sky/block light e propagação.
- Block shapes, colisão, raycast, física e integração com o servidor de jogo.
- Renderização de blocos, culling, meshing, materiais e atlas.
- Tick scheduler, block ticks, fluid ticks, random tick loop e redstone.
- Entidades, AI, jogadores, itens, inventário, crafting, menus e drops.
- Áudio/partículas em runtime, comandos, networking/protocol, client/server e save/load.
- Data packs/resource packs, codecs, NBT e serialização de rede/disco.

O `ChunkSection` já possui os contadores de fluido correspondentes à referência. Não deve ganhar armazenamento ou APIs de bioma fictícios antes da criação do modelo e do container reais.

## Lacunas de integração e manutenção

1. **Conteúdo de teste no runtime (resolvido em 21/11/2026):** `TEST_BLOCK` e suas propriedades agora existem somente no harness de testes. O catálogo de runtime registra apenas `AIR`, e `Main._Ready` inicializa os registries e valida o bloco padrão, sem executar assertions de fixtures. O loop de jogo continua sendo trabalho futuro.
2. **Relatórios de concorrência:** o detector preserva o protocolo de exclusão, mas precisa de uma camada de crash report/logging Asteria para reproduzir a forma de diagnóstico do jogo.
3. **Persistência e rede:** as paletas têm a lógica em memória, mas ainda não têm codecs nem leitura/escrita de buffer/arquivo. Isso é uma etapa de integração, não uma falha nas transições de paleta implementadas.
4. **Testes:** o harness cobre as áreas atuais, mas está concentrado num único `Program.cs`. Separar por subsistema é uma melhoria de manutenção a considerar quando o conjunto de testes crescer.

## Validação desta baseline

- Workflow [Block registry — run 44907097382](https://github.com/sarakborges/asteria2/actions/runs/44907097382): concluído com sucesso.
- Etapas: testes do registro de blocos e build do projeto Godot.
- Não houve merge para `main` nem deploy.

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
