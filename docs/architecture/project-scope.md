# Escopo do produto

## Objetivo

Asteria é um jogo exclusivamente single-player, offline e local. Não existe requisito de multiplayer nem de jogar em rede.

## Fora de escopo

- **Redstone:** circuitos de redstone, níveis de potência, fios, repetidores, comparadores, sensores e propagação/atualização de sinais.
- **Multiplayer externo:** publicar o mundo para LAN/internet, escutar sockets externos, conectar a servidores remotos, matchmaking, lobbies e presença online. O canal interno local do Integrated Server, usado só entre o cliente e o servidor no mesmo processo, continua no escopo para manter o modelo da referência.
- **Áudio:** música, sons ambientes e demais sistemas/atributos sonoros.
- **Conteúdo exclusivo de um único item, bloco ou entidade:** registros, partículas, atributos e comportamentos cuja existência depende de um objeto individual. Exemplos: Geyser, Eyeblossom, Golden Dandelion (incluindo suas partículas `pause_mob_growth` e `reset_mob_growth`), partículas de blocos especiais, partículas de mobs específicos, ovos de tartaruga, âncoras de renascimento, piglins, abelhas, gatos, snow golems e creakings. Atributos associados a raids/illagers e spawn de slimes também ficam excluídos.
- **Mobs:** categorias, spawn settings/tabelas, spawners, condições de spawn e IA/comportamentos exclusivos de mobs. Não implementar nem tratar como pendências futuras sem solicitação explícita.
- **Regras de cama:** atributos e regras específicos de cama.

Não portar conteúdo específico de um único objeto apenas porque existe na referência. Não portar sistemas da referência apenas porque são necessários para multiplayer ou redstone. Quando uma classe compartilhada contém lógica exclusiva desses domínios, documentar a exclusão; não adicionar stubs ou APIs vazias.

## Requisitos que continuam válidos no modo local

- Mundo e dimensões locais, chunks, geração de terreno e lifecycle.
- Simulação local, scheduler e atualizações para blocos/fluidos que não sejam redstone.
- Entidades, física, colisões, raycasts, rendering e interface.
- Save/load local e codecs internos/de disco necessários à persistência.
- Registries, estados, paletas, geometria e algoritmos portáveis que sejam usados pelos sistemas locais.

Serialização de save não significa protocolo de rede: persistência local permanece no escopo; network serialization e sincronização remota não.

## Regra de paridade

A referência continua sendo Minecraft Java 26.3 para algoritmos e comportamento equivalentes **dentro do escopo local**. A paridade não é um compromisso de reproduzir recursos excluídos (redstone e multiplayer/network play).

## Arquitetura single-player local

A ausência de multiplayer externo não elimina o lado servidor nem o canal local do jogo. O runtime deve manter a arquitetura real do Integrated Server:

- O processo local hospeda `IntegratedServer` e o cliente como componentes distintos. O servidor mantém a autoridade sobre o mundo e o lifecycle de chunks.
- O `ClientLevel` mantém sua própria visão do nível e recebe atualizações pelo canal interno de pacotes locais da referência (`LocalConnection`/`Connection`). Esse canal não abre sockets externos nem publica o mundo para LAN/internet.
- O caminho de geração, loading, lighting, scheduler e save/load segue os sistemas server-side equivalentes. Não condensar esses papéis em um gerador próprio que sincroniza e publica chunks manualmente.
- A renderização segue o pipeline client-side da referência. Godot fornece o backend gráfico; não substitui visibilidade, compilação de seções, modelos, atlas, culling ou cálculo de geometria.

## Primeiro marco visual

O primeiro marco verificável é um Overworld gerado com seed aleatória e terreno corretamente renderizado ao redor de uma câmera estática. Para esse marco, são prioridade:

1. Port dos blocos/estados e dos modelos/texturas necessários para representar o terreno gerado.
2. Atlas e pipeline de modelos, regiões de leitura, culling e compilação de meshes/seções.
3. Renderização correta de blocos opacos, não opacos e variantes com forma parcial.
4. Lighting e renderização de água/lava, incluindo a geometria das superfícies e laterais.
5. Chunk loading/streaming e atualização das seções renderizadas conforme necessário.

Features de decoração, estruturas, movimentação e interação do jogador ficam depois desse marco. Isso não autoriza simplificar os algoritmos dos sistemas prioritários.

## Seeds e persistência

A criação de um mundo novo usa uma seed aleatória por padrão. Seeds explícitas continuam disponíveis para testes de paridade e criação intencional de mundos específicos. Ao salvar e reabrir um mundo, a seed persistida deve ser reutilizada; randomizar novamente no carregamento mudaria o mundo e não é permitido.
