# Atributos ambientais

## Referência

Base de comparação: Minecraft Java 26.3.

- EnvironmentAttribute: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/environmentattribute/
- EnvironmentAttributeMap: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/environmentattributemap/
- AttributeType: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/attributetype/
- AttributeTypes: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/attributetypes/
- AttributeRange: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/attributerange/
- LerpFunction: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/lerpfunction/
- AttributeModifier: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/modifier/attributemodifier/
- BooleanModifier: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/modifier/booleanmodifier/
- FloatModifier: https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/attribute/modifier/floatmodifier/

## Runtime portado neste incremento

- BuiltInRegistries registra environment_attribute e attribute_type no root, nessa ordem. O bootstrap registra boolean, float e angle_degrees e 21 definições ambientais genéricas dentro do escopo do produto.

- AttributeRange<T> valida e sanitiza valores; os ranges float reproduzem os intervalos unitário, unitário com epsilon e não negativo.
- LerpFunction<T> modela interpolação float e os limiares usados pelos quatro contextos de tipos não interpolados.
- AttributeType<T> contém as bibliotecas de operações permitidas, políticas de keyframe/state/spatial/partial-tick interpolation e conversões numéricas opcionais.
- EnvironmentAttribute<T> mantém tipo, default, range e as flags syncable, positional, spatiallyInterpolated e fullResolutionBiomes.
- EnvironmentAttributeMap mantém entradas tipadas por atributo, aplica o modificador sobre o valor recebido, usa override para Set e substitui entradas anteriores por entradas posteriores em PutAll.
- BooleanModifier, FloatModifier, IntegerModifier e ColorModifier foram portados com suas operações e ordem de operandos. As cores usam interpolação vetorial direta por canal, conforme ARGB.srgbLerp(Vector3fc/Vector4fc) em 26.3.
- Mapas construídos são snapshots imutáveis; alterações posteriores no builder não alteram o mapa já criado.

## Limites deste incremento

- O projeto ainda não possui infraestrutura de codecs. Os codecs de valor e modificador, CODEC, NETWORK_CODEC e CODEC_ONLY_POSITIONAL não foram simulados; serão portados junto ao pipeline de dados/save-load local.
- AttributeTypes contém boolean, float, angle_degrees, rgb_color, argb_color, integer e moon_phase. RGB/ARGB usam System.Numerics.Vector3/Vector4 e as cinco operações de modificador da referência. INTEGER usa Int32, interpolação por piso e as cinco operações aritméticas da referência. MOON_PHASE usa os oito estados, índices, nomes serializados e início de fase da referência, sem interpolação ou modificadores. `LerpFunction<T>.OfListCrossFade` agora reproduz a composição ordenada e a escala complementar de cada elemento para cross-fade de listas; `PARTICLE` e `AMBIENT_PARTICLES` continuam bloqueados pela ausência de `ParticleOptions`/`ParticleTypes` e de codecs locais. `ACTIVITY` depende do registry e do domínio de schedules/AI, ainda não portado; as definições de atividade específicas de villagers permanecem fora do escopo atual. Áudio, bed rules e atributos vinculados a blocos/entidades específicos estão fora do escopo, não são pendências.
- Os registries built-in environment_attribute e attribute_type foram ligados ao root e ao bootstrap. As 21 definições ambientais genéricas dentro do escopo foram registradas por chave. Atributos de áudio, regras de cama e atributos de blocos/entidades específicos não são requisitos; outros tipos ainda não implementados foram omitidos, portanto não se afirma paridade dos IDs numéricos.
- Biome ainda não armazena nem resolve EnvironmentAttributeMap; a integração com fontes de ambiente e resolução espacial será outro incremento.
- Camadas de dimensão, timeline, clima e resolução de ambiente por posição/tempo continuam pendentes.
- Packet/network codec, sincronização remota e multiplayer estão fora do escopo. Persistência local permanece no escopo futuro.


## Incremento — tipos e definições ambientais built-in

- AttributeTypes agora registra boolean, float, angle_degrees, rgb_color, argb_color e integer, moon_phase na ordem relativa dos tipos mantidos no escopo.
- LerpFunction.OfDegrees reproduz o wrap angular e o limite de 90 graus da interpolação de partial tick.
- Registradas 21 definições EnvironmentAttributes genéricas; defaults, ranges, syncability, positional e spatial interpolation foram copiados da tabela 26.3.
- RGB/ARGB agora usam vetores System.Numerics, interpolação por canal e bibliotecas de modificadores portadas. Partículas, listas e activities permanecem pendentes. Áudio, bed rules e atributos de blocos/entidades específicos foram excluídos do produto. Não há claim de equivalência dos IDs numéricos das definições após entradas omitidas.
- EnvironmentAttributeMap ainda não está integrado ao Biome. Os codecs locais seguem pendentes; codecs de rede e multiplayer estão fora do escopo.


## Incremento — cross-fade genérico de listas

- Portado `LerpFunction.ofListCrossFade` de 26.3: alpha zero/um retorna a lista de origem/destino sem alocação; valores intermediários concatenam primeiro os elementos de origem escalados por `1 - alpha`, depois os elementos de destino escalados por `alpha`.
- O resultado intermediário é um snapshot somente leitura; não há clamp de alpha nem deduplicação, preservando a ordem e as chamadas ao scaler da referência.
- Isso prepara a interpolação de `AMBIENT_PARTICLES`, mas não registra um tipo incompleto: `PARTICLE` e `AMBIENT_PARTICLES` aguardam o domínio real de partículas e os codecs locais necessários.


## Incremento — modificador genérico de listas

- Portado `ListModifier.append` de 26.3: argumento vazio devolve a lista subject original; subject vazio devolve o argumento original; duas listas não vazias produzem uma nova lista concatenada em ordem subject → argumento.
- A biblioteca genérica de listas expõe somente `append`, com o identificador serializado `append`.
- A infraestrutura está pronta para `AMBIENT_PARTICLES`, mas o tipo ainda não foi registrado: `ParticleOptions`, `ParticleTypes.CODEC` e o pipeline local de codecs continuam ausentes.


## Incremento — AmbientParticle e codecs de atributo (21/11/2026)

- `AttributeType<T>` agora expõe `ValueCodec` opcional. Nesta etapa os tipos `PARTICLE` e `AMBIENT_PARTICLES` têm codecs; não se afirma que os codecs dos demais tipos built-in já estejam portados.
- `AmbientParticle` tem os campos obrigatórios `particle` e `probability`; o segundo usa validação inclusiva [0, 1].
- `CanSpawn` consome um `NextFloat` e retorna `random.NextFloat() <= probability`. `LegacyRandomSource` e `WorldgenRandom` produzem o float com 24 bits da referência.
- O tipo `AMBIENT_PARTICLES` usa `AmbientParticle.CODEC.listOf()`, cross-fade de listas que escala apenas a probabilidade e o modificador `append`.
- Registrado `visual/ambient_particles` com lista vazia e flag `syncable`.
- Atributos de partícula ligados a blocos (como o default dripstone particle) permanecem fora do escopo.
