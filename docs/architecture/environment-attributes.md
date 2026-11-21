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

- BuiltInRegistries registra o registry attribute_type no root e inicializa boolean e float durante o bootstrap, usando identificadores asteria próprios.

- AttributeRange<T> valida e sanitiza valores; os ranges float reproduzem os intervalos unitário, unitário com epsilon e não negativo.
- LerpFunction<T> modela interpolação float e os limiares usados pelos quatro contextos de tipos não interpolados.
- AttributeType<T> contém as bibliotecas de operações permitidas, políticas de keyframe/state/spatial/partial-tick interpolation e conversões numéricas opcionais.
- EnvironmentAttribute<T> mantém tipo, default, range e as flags syncable, positional, spatiallyInterpolated e fullResolutionBiomes.
- EnvironmentAttributeMap mantém entradas tipadas por atributo, aplica o modificador sobre o valor recebido, usa override para Set e substitui entradas anteriores por entradas posteriores em PutAll.
- BooleanModifier e FloatModifier foram portados com suas operações e ordem de operandos.
- Mapas construídos são snapshots imutáveis; alterações posteriores no builder não alteram o mapa já criado.

## Limites deste incremento

- O projeto ainda não possui infraestrutura de codecs. Os codecs de valor e modificador, CODEC, NETWORK_CODEC e CODEC_ONLY_POSITIONAL não foram simulados; serão portados junto ao pipeline de dados/save-load local.
- AttributeTypes contém apenas boolean e float nesta etapa. Tipos de cor, inteiro, ângulo, listas, sons, partículas, regras e mob spawns dependem de seus domínios de valor.
- O registry built-in attribute_type foi ligado ao bootstrap com as entradas asteria:boolean e asteria:float. O registry de EnvironmentAttribute e suas definições concretas ainda não foram portados.
- Biome ainda não armazena nem resolve EnvironmentAttributeMap; a integração com fontes de ambiente e resolução espacial será outro incremento.
- Camadas de dimensão, timeline, clima e resolução de ambiente por posição/tempo continuam pendentes.
- Packet/network codec, sincronização remota e multiplayer estão fora do escopo. Persistência local permanece no escopo futuro.
