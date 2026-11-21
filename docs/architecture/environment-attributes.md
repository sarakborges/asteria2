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

- BuiltInRegistries registra environment_attribute e attribute_type no root, nessa ordem. O bootstrap registra boolean, tri_state, float e angle_degrees e 32 definições ambientais compatíveis com esses domínios.

- AttributeRange<T> valida e sanitiza valores; os ranges float reproduzem os intervalos unitário, unitário com epsilon e não negativo.
- LerpFunction<T> modela interpolação float e os limiares usados pelos quatro contextos de tipos não interpolados.
- AttributeType<T> contém as bibliotecas de operações permitidas, políticas de keyframe/state/spatial/partial-tick interpolation e conversões numéricas opcionais.
- EnvironmentAttribute<T> mantém tipo, default, range e as flags syncable, positional, spatiallyInterpolated e fullResolutionBiomes.
- EnvironmentAttributeMap mantém entradas tipadas por atributo, aplica o modificador sobre o valor recebido, usa override para Set e substitui entradas anteriores por entradas posteriores em PutAll.
- BooleanModifier e FloatModifier foram portados com suas operações e ordem de operandos.
- Mapas construídos são snapshots imutáveis; alterações posteriores no builder não alteram o mapa já criado.

## Limites deste incremento

- O projeto ainda não possui infraestrutura de codecs. Os codecs de valor e modificador, CODEC, NETWORK_CODEC e CODEC_ONLY_POSITIONAL não foram simulados; serão portados junto ao pipeline de dados/save-load local.
- AttributeTypes contém boolean, tri_state, float e angle_degrees. RGB/ARGB, inteiro, moon phase, partículas, listas, sons, bed rules, activities e mob spawn dependem de domínios de valor ainda não portados.
- Os registries built-in environment_attribute e attribute_type foram ligados ao root e ao bootstrap. As 32 definições ambientais com tipos já implementados foram registradas por chave; as entradas que dependem de domínios ausentes foram omitidas, portanto não se afirma paridade dos IDs numéricos após essas lacunas.
- Biome ainda não armazena nem resolve EnvironmentAttributeMap; a integração com fontes de ambiente e resolução espacial será outro incremento.
- Camadas de dimensão, timeline, clima e resolução de ambiente por posição/tempo continuam pendentes.
- Packet/network codec, sincronização remota e multiplayer estão fora do escopo. Persistência local permanece no escopo futuro.


## Incremento — tipos e definições ambientais built-in

- Implementado TriState com TRUE/FALSE/DEFAULT, conversão de boolean, fallback e nome serializado.
- AttributeTypes agora registra boolean, tri_state, float e angle_degrees na ordem da referência.
- LerpFunction.OfDegrees reproduz o wrap angular e o limite de 90 graus da interpolação de partial tick.
- Registradas as 32 definições EnvironmentAttributes cujos tipos de valor estão implementados; defaults, ranges, syncability, positional e spatial interpolation foram copiados da tabela 26.3.
- Os tipos de cor, inteiro, moon phase, partículas, listas, sons, bed rules, activities e mob spawn permanecem pendentes. Não há claim de equivalência dos IDs numéricos das definições após entradas omitidas.
- EnvironmentAttributeMap ainda não está integrado ao Biome. Os codecs locais seguem pendentes; codecs de rede e multiplayer estão fora do escopo.
