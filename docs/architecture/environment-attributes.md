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

- BuiltInRegistries registra environment_attribute e attribute_type no root, nessa ordem. O bootstrap registra boolean, float e angle_degrees e 19 definições ambientais genéricas dentro do escopo do produto.

- AttributeRange<T> valida e sanitiza valores; os ranges float reproduzem os intervalos unitário, unitário com epsilon e não negativo.
- LerpFunction<T> modela interpolação float e os limiares usados pelos quatro contextos de tipos não interpolados.
- AttributeType<T> contém as bibliotecas de operações permitidas, políticas de keyframe/state/spatial/partial-tick interpolation e conversões numéricas opcionais.
- EnvironmentAttribute<T> mantém tipo, default, range e as flags syncable, positional, spatiallyInterpolated e fullResolutionBiomes.
- EnvironmentAttributeMap mantém entradas tipadas por atributo, aplica o modificador sobre o valor recebido, usa override para Set e substitui entradas anteriores por entradas posteriores em PutAll.
- BooleanModifier, FloatModifier, IntegerModifier e ColorModifier foram portados com suas operações e ordem de operandos. As cores usam interpolação vetorial direta por canal, conforme ARGB.srgbLerp(Vector3fc/Vector4fc) em 26.3.
- Mapas construídos são snapshots imutáveis; alterações posteriores no builder não alteram o mapa já criado.

## Limites deste incremento

- O codec do argumento do modificador está exposto para operações booleanas, inteiras, float, cor e append de listas com codec de elemento fornecido. Os argumentos de cor usam os codecs hexadecimais RGB/ARGB da referência (com representação vetorial alternativa); `blend_to_gray` usa `brightness` e `factor` limitados a [0, 1]. `FloatWithAlpha` aceita forma escalar compacta quando alpha é 1 e forma expandida com alpha opcional limitado a [0, 1]. O codec completo do mapa ambiental e `CODEC_ONLY_POSITIONAL` foram portados; codecs de rede permanecem fora do escopo.
- AttributeTypes contém boolean, float, angle_degrees, rgb_color, argb_color, integer e moon_phase. RGB/ARGB usam System.Numerics.Vector3/Vector4 e as cinco operações de modificador da referência. INTEGER usa Int32, interpolação por piso e as cinco operações aritméticas da referência. MOON_PHASE usa os oito estados, índices, nomes serializados e início de fase da referência, sem interpolação ou modificadores. `LerpFunction<T>.OfListCrossFade` agora reproduz a composição ordenada e a escala complementar de cada elemento para cross-fade de listas; `PARTICLE` e `AMBIENT_PARTICLES` já possuem codecs locais e usam o catálogo de partículas genéricas mantido no escopo. `ACTIVITY` depende do registry e do domínio de schedules/AI, ainda não portado; as definições de atividade específicas de villagers permanecem fora do escopo atual. Áudio, bed rules, raid/illager, slime e atributos vinculados a blocos/entidades específicos estão fora do escopo, não são pendências.
- Os registries built-in environment_attribute e attribute_type foram ligados ao root e ao bootstrap. As 19 definições ambientais genéricas dentro do escopo foram registradas por chave. Atributos de áudio, regras de cama, raid/illager, slime e atributos de blocos/entidades específicos não são requisitos; outros tipos ainda não implementados foram omitidos, portanto não se afirma paridade dos IDs numéricos.
- `EnvironmentAttributeResolver` aplica mapas de fontes em ordem explícita de prioridade baixa para alta, iniciando pelo default registrado ou por um valor base fornecido pelo chamador. A ordem de 26.3 é dimensão → bioma → timeline → clima.
- A resolução espacial/temporal ainda depende de provedores reais de dimensão, amostragem de biomas, timeline e clima. Este resolver não inventa essas fontes nem implementa interpolação entre biomas ou seleção de clocks; a amostragem temporal de keyframes passa a ser responsabilidade de `KeyframeTrackSampler`.
- Packet/network codec, sincronização remota e multiplayer estão fora do escopo. Persistência local permanece no escopo futuro.


## Incremento — tipos e definições ambientais built-in

- AttributeTypes agora registra boolean, float, angle_degrees, rgb_color, argb_color e integer, moon_phase na ordem relativa dos tipos mantidos no escopo.
- LerpFunction.OfDegrees reproduz o wrap angular e o limite de 90 graus da interpolação de partial tick.
- Registradas 19 definições EnvironmentAttributes genéricas; defaults, ranges, syncability, positional e spatial interpolation foram copiados da tabela 26.3.
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


## Incremento — resolução ordenada de fontes (22/11/2026)

- Portado o fold de modificadores em ordem crescente de prioridade para `EnvironmentAttributeResolver`: começa no default do atributo (ou em valor-base explícito) e aplica cada `EnvironmentAttributeMap` sequencialmente.
- A ordem documentada para 26.3 é dimensão → bioma → timeline → clima; o chamador fornece apenas as fontes que já existem e mantém essa ordem.
- Testes cobrem composição override + multiply + add, override pela fonte mais alta, default sem fontes, valor-base explícito, mapa vazio e rejeição de fontes nulas.
- Não foram criados objetos falsos para dimensão, timeline ou clima. Amostragem espacial de biomas e interpolação temporal/espacial permanecem pendentes.


## Incremento — codecs de argumentos dos modificadores (22/11/2026)

- `AttributeModifier<TSubject, TArgument>` agora expõe `ArgumentCodec` opcional, mantendo compatibilidade com modificadores ainda sem codec.
- Modificadores booleanos e inteiros usam `Codecs.BOOL` e `Codecs.INT`; operações float usam `Codecs.FLOAT`.
- `FloatWithAlpha` usa codec de registro com campos obrigatórios `value` e `alpha`.
- Testes cobrem round-trip de todos os argumentos booleanos, inteiros e float, além do argumento de alpha blend.
- Este incremento não afirma que o codec de `EnvironmentAttributeMap` está pronto: despacho por atributo, despacho de modificador, validação posicional e codecs de argumentos de cor/lista ainda precisam ser conectados.


## Incremento — codecs de argumentos de cor (22/11/2026)

- Os modificadores RGB e ARGB agora declaram codecs tipados para os argumentos de 3 e 4 canais, conforme o tipo de cada operação.
- `blend_to_gray` serializa os campos obrigatórios `brightness` e `factor`.
- Testes cobrem round-trip para alpha blend, add, subtract, multiply e blend-to-gray em RGB/ARGB, além da rejeição de vetores com número incorreto de canais.
- Os codecs de argumentos de listas e o codec de `EnvironmentAttributeMap` permanecem pendentes; codec de rede continua fora do escopo.


## Incremento — codec de argumentos de listas (22/11/2026)

- `ListModifier<T>.CreateLibrary` agora recebe o codec do elemento e declara o codec de argumento como lista desse elemento.
- `AMBIENT_PARTICLES` conecta o modificador `append` ao `AmbientParticle.CODEC`; listas não recebem um codec genérico inventado quando não há codec de elemento.
- Teste verifica round-trip de uma lista com partícula e probabilidade, além da integração com a biblioteca do atributo ambiental.
- O codec do mapa ambiental, despacho atributo/modificador e validação positional permanecem pendentes. Codec de rede continua fora do escopo.


## Incremento — dispatch de modificadores e fidelidade dos codecs (22/11/2026)

- `AttributeType<T>` agora expõe um codec de identificador para os modificadores permitidos, incluindo `override`; nomes desconhecidos e instâncias não registradas são rejeitados.
- Os codecs de argumentos de cor foram alinhados aos codecs hexadecimais RGB/ARGB da referência, preservando a alternativa vetorial.
- `BlendToGray` valida brightness/factor em [0, 1].
- `FloatWithAlpha` agora reproduz a forma compacta escalar quando alpha é 1, a forma expandida com alpha opcional e default 1, e a validação de alpha em [0, 1].
- O codec do mapa ambiental ainda precisa conectar o registry de atributos ao codec de cada entrada; `CODEC_ONLY_POSITIONAL` depende desse codec. Network codec permanece fora do escopo.


## Codec de EnvironmentAttributeMap

- `EnvironmentAttributeMap.CODEC` serializa mapas como objetos cujas chaves são identificadores resolvidos por `BuiltInRegistries.EnvironmentAttribute`.
- Entradas `override` usam o valor direto e compacto. Outros modificadores usam objeto com `modifier` e `argument`, selecionando o codec do argumento a partir da biblioteca permitida pelo `AttributeType`.
- O codec do valor direto e o argumento de `override` aplicam o `AttributeRange` da definição do atributo; os argumentos dos demais modificadores usam os codecs específicos declarados por cada operação.
- `CODEC_ONLY_POSITIONAL` valida o mapa completo e rejeita atributos com `IsPositional == false`, tanto na decodificação quanto na codificação.
- A resolução do codec da entrada é memoizada por definição de atributo. O codec de rede não foi portado porque a execução remota/sincronização entre jogadores está fora do escopo local.
- A infraestrutura genérica `Codecs.DispatchedMap` faz despacho por chave registrada e mantém a acumulação de erros/parciais de `DataResult` e os contratos de `DynamicOps`.


## Incremento — keyframe tracks e attribute tracks (22/11/2026)

- Portados `Keyframe<T>`, `KeyframeTrack<T>` e `KeyframeTrackSampler<T>`: codec de keyframe (ticks não negativos e value tipado), lista de keyframes validada e ordenada, easing opcional com default linear, validação de faixa do período e amostragem por segmentos.
- O sampler preserva os limites de cada segmento, transições instantâneas com keyframes no mesmo tick e repetição periódica por floor-mod, incluindo o segmento que atravessa o fim/início do período.
- Portados os 32 easing types built-in e Bézier cúbica. O codec aceita identificadores simples ou `{ "cubic_bezier": [x1, y1, x2, y2] }`; somente x1 e x2 são limitados a [0, 1], como na referência.
- `AttributeTrack<TValue, TArgument>` escolhe o codec de keyframes a partir do modifier e do atributo. O campo `modifier` é opcional e usa override como padrão. A interpolação dos argumentos usa `ArgumentKeyframeLerp(attribute)`, preservando o interpolador específico do modifier.
- `AttributeTrackSampler` reproduz o cache por `cacheTickId` e aplica o modifier ao valor-base após amostrar o argumento.
- Adaptação explícita: como Asteria ainda não tem `WorldClock`/ `ClockManager`, o sampler recebe `totalTicks` no método `ApplyTimeBased`; não foi criado relógio fictício.
- Ainda não portados: registro de clocks, `ClockManager`, registry de `Timeline`, markers e conexão de timelines às fontes de dimensão. Este incremento completa a camada genérica de tracks e amostragem, mas não declara o sistema de timelines inteiro concluído.
- Codec de rede e sincronização remota permanecem fora do escopo local.

## Incremento — clocks e time markers (22/11/2026)

- Portados `WorldClock`, `ClockInstance` e o contrato `ClockManager`. O registry local `world_clock` registra `asteria:overworld` e `asteria:the_end`; o codec de holder usa somente chaves registradas e o codec direto do relógio é um mapa vazio, como na referência.
- `ClockTimeMarker` preserva a associação ao holder do clock, o tick, o período opcional e a flag de exibição. Foram portados `GetRepetitionCount`, `ResolveTimeToMoveTo` e `OccursAt`, incluindo a semântica de módulo e a resolução para a próxima ocorrência quando o tick atual coincide com o marcador.
- `ClockTimeMarkers` mantém somente os identificadores genéricos DAY, NOON, NIGHT e MIDNIGHT. Marcadores ligados a sono ou a mecânicas de raid não entram no produto.
- `AttributeTrackSampler` agora recebe `Holder<WorldClock>` e `ClockManager`, lê `TotalTicks` ao atualizar o cache e mantém a amostragem por `cacheTickId`, reproduzindo o contrato de 26.3. O teste usa um clock controlado em memória, sem criar implementação de relógio de produção.
- Ainda não portados: implementação de avanço/persistência de clocks, registry e codec de `Timeline`, builder de tracks/time markers e validação de duplicidade de markers por clock. `ClockManager` permanece como contrato para a camada de mundo local que será integrada em seguida; não foi adicionado relógio simulado de produção.
- Referências: [WorldClock](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/clock/worldclock/), [WorldClocks](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/clock/worldclocks/), [ClockInstance](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/clock/clockinstance/), [ClockTimeMarker](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/clock/clocktimemarker/) e [AttributeTrackSampler](https://mc-packet-reference.netlify.app/26.x/source/net/minecraft/world/timeline/attributetracksampler/).
