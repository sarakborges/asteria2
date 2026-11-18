# Instruções do repositório

## Contexto

Asteria 2 é uma nova implementação, iniciada do zero em Godot .NET. O código do repositório antigo `asteria` não deve ser copiado ou reaproveitado automaticamente.

## Direção do projeto

- Usar Minecraft como referência principal de arquitetura e comportamento para os sistemas equivalentes.
- Priorizar paridade com o comportamento de referência em vez de criar soluções próprias sem necessidade.
- Não antecipar mecânicas, sistemas ou decisões arquiteturais que ainda não tenham sido solicitados ou definidos.

## Regras de trabalho

- Ler estas instruções antes de alterar o projeto.
- Fazer mudanças pequenas, isoladas e verificáveis.
- Preservar alterações existentes e não sobrescrever trabalho alheio.
- Trabalhar em branches de tarefa. Não fazer merge na `main` sem solicitação explícita.
- Não adicionar dependências, plugins ou ferramentas sem necessidade concreta.
- Ao concluir uma tarefa, informar arquivos alterados e validações executadas, incluindo validações que não puderam ser feitas.

## Base técnica

- Godot 4.7.2 .NET.
- .NET 8.
- Lógica de jogo em C#, com cenas e recursos nativos do Godot.
