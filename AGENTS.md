# Instruções do repositório

## Contexto
Asteria 2 é uma nova implementação, iniciada do zero em Godot .NET. O código do repositório antigo `asteria` não deve ser copiado ou reaproveitado automaticamente.

## Direção do projeto
- Tratar o código-fonte de Minecraft Java 26.3 como especificação para os sistemas equivalentes: buscar port fiel, não apenas semelhança arquitetural ou comportamental.
- Preservar algoritmos, estruturas de dados, responsabilidades, constantes, ordem de operações, condições de contorno, efeitos colaterais e comportamento de erros.
- Portar a cadeia real de dependências necessária; não substituir sistemas ausentes por stubs, aproximações ou implementações simplificadas.
- Adaptar somente o inevitável para C#, .NET e Godot, documentando cada divergência.
- Minecraft é apenas referência externa: nunca introduzir namespaces, identificadores, strings, dependências ou referências de runtime com namespace `minecraft` no código de Asteria.
- Exigir testes de paridade, incluindo limites e casos de erro; compilação bem-sucedida não comprova paridade.
- Não antecipar mecânicas, sistemas ou decisões arquiteturais que ainda não tenham sido solicitados ou definidos.

## Escopo explicitamente excluído
- Não implementar nem portar mobs até solicitação explícita.
- Não manter categorias de mobs, dados/tabelas de spawn de mobs, spawners, geração de mobs, comportamento/IA de mobs ou outros sistemas exclusivos de mobs.
- Não criar dependências indiretas para esses sistemas nem reintroduzi-los em etapas futuras sem autorização explícita.
- Sistemas genéricos que apenas tenham "spawn" no nome não são automaticamente sistemas de mobs; avaliar sua função real antes de removê-los.

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
