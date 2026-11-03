# PROJETO ZETTA — REDESIGN VISUAL AUTÔNOMO
## Direção B: assistente espacial sofisticado

Você é o engenheiro responsável pelo redesign visual do projeto Zetta, uma assistente virtual de inteligência artificial espacial para Meta Quest 3.

**IMPORTANTE: o desenvolvedor ficará ausente durante a execução. Você tem autorização para analisar, planejar e implementar as melhorias deste documento de forma autônoma, sem pedir aprovação a cada etapa.**

Objetivo: entregar uma evolução visual significativa, mantendo integralmente a arquitetura e as funcionalidades existentes.

---

# 1. OBJETIVO PRINCIPAL

Transformar a interface atual da Zetta numa experiência holográfica sofisticada, no estilo de assistente de IA de laboratório futurista, com **visual original** (sem reproduzir interfaces, telas ou efeitos específicos de filmes).

A identidade deve transmitir:
- Inteligência artificial avançada.
- Tecnologia espacial de última geração.
- Elegância e minimalismo.
- Interface holográfica realista.
- Integração natural com o ambiente físico.
- Interação intuitiva por gestos e voz.

Não queremos cyberpunk genérico com excesso de neon. Queremos uma interface premium, tecnológica e cinematográfica.

Referência visual: `Docs/referencia-visual.png` (ver item 15 para como interpretá-la).

# 2. REGRA FUNDAMENTAL: PRESERVAR O PROJETO

Antes de alterar qualquer arquivo:
1. Analise a estrutura do projeto.
2. Leia o PLANO-ZETTA.md.
3. Leia o PROGRESSO.md.
4. Analise o estado atual do Git.
5. Identifique os sistemas existentes e suas dependências.
6. Identifique quais componentes controlam a interface visual.
7. Identifique quais componentes controlam funcionalidades e interações.

Não reconstrua o projeto do zero. Não substitua sistemas funcionais por implementações alternativas.

## Tecnologias que devem ser preservadas
Unity 6.6, template Mixed Reality, OpenXR, pacote da Meta, AR Foundation, XR Interaction Toolkit, XR Hands, Gemini Live, Passthrough Camera API, glTFast, Hugging Face, FLUX, TRELLIS.2, GitHub Pages.

O Meta Interaction SDK não está instalado. Não introduza essa dependência.

Não altere a arquitetura de voz, a comunicação com o Gemini, o reconhecimento de mãos, as câmeras, as âncoras ou o carregamento de modelos 3D para fazer o redesign.

**Priorize a separação entre apresentação visual e lógica de funcionamento.**

# 3. IDENTIDADE VISUAL OFICIAL

| Elemento | Cor |
|---|---|
| Azul tecnológico | #42C8FF |
| Azul profundo | #102C46 |
| Ciano luminoso | #8BEAFF |
| Dourado | #E8B95E |
| Branco holográfico | #EAF6FF |
| Roxo secundário | #947BFF |
| Fundo translúcido | Azul escuro com transparência |

- O dourado continua sendo a identidade da esfera da Zetta.
- O azul predomina nos painéis e controles.
- O roxo só em situações especiais ou quando o usuário pedir.

# 4. ESFERA DA ZETTA

A esfera dourada é o elemento central da identidade.

NÃO substitua a esfera. NÃO remova seus componentes. NÃO simplifique sua estrutura sem necessidade.

Preserve: os 10 anéis, os arcos rotativos, os fragmentos de dados, as faíscas orbitais, o núcleo em espiral, as mudanças de estado, a mudança de cor por voz e a persistência da cor escolhida.

Melhore só o necessário para harmonizá-la com a nova identidade (animações, iluminação, partículas, transições). Mantenha os estados: espera, ouvindo, pensando, falando, erro. Evite aumentar o custo de renderização.

A esfera continua **pequena no canto inferior direito da visão**, seguindo o olhar. Não mova para o centro.

# 5. REDESIGN DOS PAINÉIS HOLOGRÁFICOS

Reformule visualmente todos os painéis existentes:
- Aparência de vidro holográfico (translucidez + borda + leve gradiente; sem desfoque real).
- Transparência real, deixando ver a sala.
- Bordas finas em azul-ciano.
- Iluminação suave.
- Cantos discretamente arredondados.
- Tipografia moderna e legível.
- Ícones minimalistas.
- Animações suaves de entrada e saída.
- Profundidade espacial.

Evite painéis excessivamente brilhantes.

Padronize: painéis de informações, notas holográficas, timers, aplicativos, menus, controles de objetos 3D, indicadores de sistema e mensagens da assistente. Todos devem parecer do mesmo sistema operacional espacial.

# 6. INTERFACE PRINCIPAL (CENTRAL DA ZETTA)

Apresentação mais sofisticada da central, **sob demanda** (ver item 14):

- **Cabeçalho:** identidade ZETTA, relógio, data, indicador de conexão, status do sistema.
- **Painel de aplicativos:** só funções existentes (ver item 15), em apresentação elegante.
- **Área dos painéis:** espaço onde os painéis aparecem quando chamados; a esfera NÃO vem para cá.
- **Painéis informativos:** sem ocupar desnecessariamente o campo de visão.
- **Barra de comandos:** melhor apresentação dos controles de voz e das interações (microfone ao centro, status "Ouvindo..." etc.).

Não invente funcionalidades. Use só funções já implementadas ou elementos visuais que não dependam de novos sistemas.

# 7. HOLOGRAMAS E OBJETOS 3D

Preserve integralmente: cubos, esferas, cones, pirâmides, anéis, sistema solar, moléculas, globo, desenho no ar, mesa holográfica, objetos presos ao braço e modelos do TRELLIS.2.

Melhore a apresentação de seleção, materialização e desaparecimento. Ao selecionar, mantenha a cor original com destaque luminoso sutil (nunca o efeito antigo de deixar branco). Preserve a pegada por qualquer ponto, inclusive bordas.

# 8. EXPERIÊNCIA DE REALIDADE AUMENTADA

Este projeto é AR, não VR.
- A sala real permanece visível.
- Não crie fundos artificiais que substituam o passthrough.
- Painéis nunca ocupam todo o campo de visão.
- Respeite as âncoras e posicionamentos existentes.
- Preserve a lixeira na altura da cintura, à direita, acompanhando o usuário.
- Tamanho e distância confortáveis para leitura no Quest 3.

# 9. DESEMPENHO

O Quest 3 é o dispositivo de destino. Não faça efeitos que só funcionam bem no editor.

Avalie: partículas, transparências, overdraw, shaders, materiais, draw calls, iluminação, animações, objetos ativos simultâneos, alocação de memória.

Sem pós-processamento caro. Shaders simples e eficientes. Sem aumentar polígonos à toa. Compare o desempenho antes e depois quando possível. Não declare desempenho aprovado no Quest sem teste no dispositivo real.

# 10. EXECUÇÃO AUTÔNOMA POR FASES

- **Fase 1 — Auditoria:** documentação, scripts, componentes visuais, dependências, erros existentes, estado inicial do Git. Relatório curto de auditoria.
- **Fase 2 — Sistema visual:** sistema reutilizável de identidade (cores, materiais, transparências, tipografia, bordas, brilhos, animações), sem valores visuais espalhados pelos scripts.
- **Fase 3 — Painéis:** redesign dos painéis e controles, preservando referências, eventos, componentes e funcionalidades.
- **Fase 4 — Esfera:** ajustes visuais sem reconstruir o funcionamento.
- **Fase 5 — Hologramas:** padronizar materiais e efeitos dos objetos 3D.
- **Fase 6 — Animações:** transições de abertura, fechamento, materialização e interação, sem prejudicar a interação com as mãos.
- **Fase 7 — Otimização:** revisar desempenho e eliminar custos visuais desnecessários.
- **Fase 8 — Validação:** erros de compilação, referências ausentes, prefabs quebrados, materiais ausentes, shaders incompatíveis, componentes duplicados, problemas de inicialização, funcionalidades preservadas. O que não der para validar sem o óculos, registre claramente.

# 11. REGRAS DE SEGURANÇA

1. Nunca execute git reset --hard.
2. Nunca execute git clean -fd.
3. Nunca apague arquivos do usuário para resolver conflitos.
4. Nunca sobrescreva alterações preexistentes sem entender sua finalidade.
5. Não remova funcionalidades para simplificar o redesign.
6. Não altere configurações de build sem necessidade.
7. Não modifique tokens, credenciais ou configurações de acesso.
8. Não exponha o token do Hugging Face nem o do GitHub.
9. Não publique nada no GitHub Pages.
10. Não faça push para o GitHub.
11. Não instale dependências desnecessárias.
12. Não faça alterações fora do escopo do redesign.

Se algo puder comprometer funcionalidades existentes, preserve o código e registre para revisão.

# 12. GIT E DOCUMENTAÇÃO

Commits locais separados por fase. Antes de cada commit: verifique o diff, confira que não há arquivos indevidos e que a alteração pertence à fase.

Atualize o PLANO-ZETTA.md e o PROGRESSO.md com: o que mudou, arquivos modificados e novos, componentes visuais criados, problemas, testes e resultados, limitações e próximas atividades.

# 13. ENTREGA FINAL

Relatório no topo do PROGRESSO.md com:
1. Resumo do redesign.
2. Melhorias visuais.
3. Melhorias na esfera.
4. Melhorias nos painéis.
5. Melhorias nos hologramas.
6. Arquivos criados.
7. Arquivos modificados.
8. Commits.
9. Resultado da compilação.
10. Testes realizados.
11. Problemas pendentes.
12. O que testar no Meta Quest 3, em ordem.

Gere capturas de tela no editor e salve em `Docs/capturas-redesign/`. Não afirme que funciona perfeitamente no óculos sem teste físico.

# 14. AJUSTES FINAIS (têm prioridade sobre o restante)

1. Use `Docs/referencia-visual.png` como direção de estilo; o visual da Zetta é original, sem reproduzir interfaces, telas ou efeitos de filmes.
2. A esfera continua pequena no canto inferior direito, seguindo o olhar.
3. Cabeçalho, painel de aplicativos e barra de comandos aparecem sob demanda ("Zetta, abra a central" / "Zetta, feche a central", e também pinça/toque na esfera) e se recolhem sozinhos após ~15 s sem interação. Em repouso, só a esfera e o status ficam visíveis.
4. Vidro holográfico = translucidez + borda + leve gradiente. Sem desfoque real (grab pass/blur) e sem pós-processamento.
5. Tipografia: pode importar UMA fonte gratuita de licença aberta (OFL), por exemplo Inter, Rajdhani ou Exo 2, gerando o TMP Font Asset com acentos do português. Registre a licença no PROGRESSO.md.
6. Uma fase por vez; depois de cada uma: Console sem erros, build de conferência (sem instalar no Quest), commit local e anotação no PROGRESSO.md. Se uma fase quebrar o build e não der para consertar em 3 tentativas, reverta só os arquivos daquela fase (git restore ou git revert do próprio commit) e siga para a próxima.
7. Se o contexto ficar grande, resuma e continue guiado pelo PROGRESSO.md.

# 15. LEITURA DA REFERÊNCIA (Docs/referencia-visual.png)

- É uma prancha conceitual: os quadros da faixa de baixo são exemplos de estilo, não telas a construir.
- NÃO crie avatar humano/personagem. O avatar está nas "Ideias futuras" do PLANO-ZETTA.md e será uma fase própria. A presença da Zetta continua sendo só a esfera dourada no canto.
- O painel "Aplicativos" só leva funções que já existem: Sites (Internet), Objetos 3D/Modelos (Projetos 3D), Notas, Timers, Desenhar, Informações (clima/notícias). Não crie "Mídia", "Configurações" nem agenda/"Próximos eventos".
- "Notas rápidas" usa as notas existentes (sem caixas de marcar).
- O painel "Informações" usa os dados reais de clima/notícias quando chamados.
- Nunca mostre todos os painéis ao mesmo tempo; siga a central sob demanda do item 14.
- O brilho da imagem é de cinema; no Quest, sem pós-processamento, busque o efeito com materiais aditivos leves, sem perseguir o brilho exato da imagem.
