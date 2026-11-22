# Escopo do produto

## Objetivo

Asteria é um jogo exclusivamente single-player, offline e local. Não existe requisito de multiplayer nem de jogar em rede.

## Fora de escopo

- **Redstone:** circuitos de redstone, níveis de potência, fios, repetidores, comparadores, sensores e propagação/atualização de sinais.
- **Multiplayer/network play:** transporte de rede, protocolo de jogo, servidor/cliente autoritativo, replicação de estado, sincronização remota, matchmaking, lobbies e presença online.
- **Áudio:** música, sons ambientes e demais sistemas/atributos sonoros.
- **Conteúdo exclusivo de um único item, bloco ou entidade:** registros, partículas, atributos e comportamentos cuja existência depende de um objeto individual. Exemplos: Geyser, Eyeblossom, Golden Dandelion (incluindo suas partículas `pause_mob_growth` e `reset_mob_growth`), partículas de blocos especiais, partículas de mobs específicos, ovos de tartaruga, âncoras de renascimento, piglins, abelhas, gatos, snow golems e creakings. Atributos associados a raids/illagers e spawn de slimes também ficam excluídos.
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
