# PROGRESSO

# 🌟 RESUMO FINAL — RODADA 6 (7 tarefas)
**Estado:** T1–T7 feitas e commitadas localmente (`f280c1e`, `195faea`, `f605091`, `08070d9`). Console sem erros; 22/22 testes automáticos; build de conferência Android em `Builds/Android/Conferencia.apk` (**NÃO instalado** no Quest; nenhum push). Tudo conferido só no Editor; nada com mãos reais, TRELLIS real ou medição no Quest.
**Arquivos novos:** `ObjetoInterativo.cs`, `FerramentasPartes.cs`, `EstiloCientifico.cs`, `DnaHolo.cs`, `ExpressaoMatematica.cs`, `MatematicaHolo.cs`, `ResolucaoCalculadora.cs`, `GeometriaHolo.cs`. **Capturas:** `Docs/capturas-redesign/r6-*.png`.
**Desempenho (só Editor, Quest não medido):** materiais Sprites/Default, sem pós-processamento; custo maior = cubo com 6 partes, grades/nervuras do estilo científico (~1300 arestas por parte), DNA (~26 degraus + 2×140 segmentos). Medir no Quest com vários objetos; se pesar, reduzir `maxObjetos`.
**Testar no Quest 3, em ordem** (`adb install -r Builds/Android/Conferencia.apk`):
1. Central: tocar/pinçar a esfera (abre uma vez só), olhar para ela e esperar >15 s (não fecha), dizer "Zetta, feche a central".
2. "Crie um cubo vermelho"; "pinte de azul mais escuro"; "cada face de uma cor"; "base dourada e resto azul".
3. Pegar com uma mão, escalar com duas; "abra" / "junte"; pegar uma parte separada; pinçar uma parte (nome aparece); "gire" / "pare de girar".
4. Sistema solar e .glb do TRELLIS: "abra", "pinte <parte>", "tire <parte>".
5. Forma matemática (toro, Klein): estilo científico, "Zetta, modo estrutura".
6. "Mostre o DNA", "mostre as bases", "explique o DNA", "desenrole o DNA".
7. "Desenhe z = sen(x)*cos(y)"; "mostre os gráficos de sen(x) e cos(x)".
8. "Resolva 2x + 3 = 11" (etapas em sequência); "abra a calculadora" (tocar teclas).
9. "Mostre um triângulo interativo" (arrastar vértices, medidas mudam).
10. Reiniciar o app e conferir que cores/estado voltam com as âncoras.

# 🔧 RODADA 6 (7 tarefas) — detalhes por tarefa
## 1. Central abre e fecha sozinha — feita (só Editor)
- Causas achadas: (a) `PincaNoAnel` só rodava com a Zetta calada, então `pincaAnterior` ficava velho e uma pinça segurada reabria/alternava ao fim da fala; (b) pinça alternava (abria e fechava) e outros módulos (`bom dia`, `modo foco`, código, mapa, painel de info) chamavam `Fechar`; (c) `fechar_central` podia vir de "abra" mal ouvido; (d) recolhia em 15 s mesmo olhando para ela.
- Agora: pinça/toque na esfera só ABRE (com ela aberta só renova o tempo), debounce 0,5 s, `PincaNoAnel` roda sempre (e só acorda quando calada). A central só fecha por "Zetta, feche a central" (a ferramenta `fechar_central` é ignorada se a frase ouvida não contém fech/recolh/esconda/some), botão FECHAR ou **60 s** sem olhar para ela, mão a <0,35 m, voz (ouvindo/pensando/falando) ou toque. Removidos os `Fechar` de outros módulos e dos botões de app (renovam o tempo). O log registra "CentralZetta: fechada, pedido por: <quem>".
- Arquivos: `CentralZetta.cs`, `InterrupcaoPinca.cs`, `TemaZetta.cs`, `AssistenteVoz.cs` (propriedade `UltimaFraseUsuario`), `BriefingFoco.cs`, `CodigoHolo.cs`, `MapaProjeto.cs`, `PainelInfo.cs`. Console sem erros, testes 19/19.

## 2–4. Cores, cores por parte e objetos interativos feitos de partes — feitas (só Editor; commit único porque o código é o mesmo sistema)
- **Sistema novo:** `ObjetoInterativo.cs` (`ObjetoInterativo` na raiz + `ParteHolo` em cada parte + `MalhaPartes` que divide malhas). TUDO que a Zetta cria passa por ele: formas básicas (cubo 6 faces: frente/tras/direita/esquerda/topo/base; pirâmide: base/lados; cone: base/lateral; cilindro: topo/base/lateral; cápsula: topo/meio/base; anel: fora/dentro; esfera: 4 faixas com grupos norte/sul/hemisfério/equador), formas matemáticas (3 regiões ao longo do maior eixo; DNA: fita A/fita B/degraus), modelos prontos (peças nomeadas: Sol, planetas, Oxigênio, Hidrogênio...), .glb (um parte por nó; renderer com vários materiais vira uma parte por submalha; malha única legível é dividida em 3 regiões; se a malha não for legível fica uma parte só) e o resto por descoberta de renderers. Cada parte é um filho com malha, grade e preenchimento próprios (a montagem sobe junta pela caixa do objeto inteiro).
- **Cores (T2):** `editar_objeto` acao=cor funciona em todos os tipos (partes; `.glb` tinge `_BaseColor`/`_Color` numa cópia do material da parte; nota usa a cor da própria nota; desenhos no ar: pintar, mover e escalar o conjunto; gráficos e outros: tinge todos os renderers/linhas). Aceita cores por nome (também feminino/plural e turquesa, bege, vinho, lilás, salmão, prata, ouro...), hexadecimal e **"mais claro/mais escuro"** (também "azul mais escuro"). Ao criar: cor pedida é respeitada; sem cor, a Zetta usa cores variadas da paleta em sequência. Tudo salvo com a âncora (campo novo `estado` no registro; JSON com cores por parte, partes escondidas, girando, aberto e extras do tipo); duplicar leva as cores.
- **Cores por parte (T3):** ferramenta `pintar_parte` (objeto, parte, cor; ou `pares` com `resto`) e `criar_objeto` ganhou `cores` (uma por parte; degradê com `degrade=true`) e `partes_cores` (base dourada e resto azul). `partes_objeto` lista as partes e faz cada uma piscar com o nome flutuando.
- **Interação (T4):** `FerramentasPartes.cs` (`pintar_parte`, `partes_objeto`, `visibilidade_parte` tire/mostre, `girar_objeto` gire/pare, `acao_objeto`). "Abra/separe" e "feche/junte" = `vista_explodida` (agora delega ao sistema de partes). Com o objeto aberto cada parte ganha colisor, corpo cinemático e pegada própria (o colisor da raiz sai do caminho): pegar uma parte move só ela; "junte" devolve todas, removendo as pegadas. Pinçar uma parte (a menos de 5 cm) destaca e mostra o nome. Uma mão pega/move/gira e duas escalam/giram (o `InteracaoMaos` de antes na raiz). DNA: `acao_objeto` desenrolar/enrolar/voltas (regenera só os vértices, animado).
- **Testado no Editor (Play):** criação de todas as formas e suas partes; cores/degradê/`partes_cores`; explodir com 7 pegadas; sistema solar/água/DNA/toro/Möbius/fractal; pintar/tirar/girar/listar pelas ferramentas; DNA voltas e desenrolar; `editar_objeto` cor em modelo e forma; `.glb` de teste gerado por script (esfera + caixa com 2 materiais): 3 partes, cor, explosão e estado; malha única → base/meio/topo. Teste automático novo (20/20). **Não testado:** pegar partes e pinçar com mão real; .glb real do TRELLIS (a malha pode não ser legível).
- **Custo:** mais objetos de renderização (cubo = 6 partes x 2 renderers). Cada parte tem materiais próprios (necessário para pintar); a grade usa o mesmo shader leve. Medir no Quest com vários objetos; se pesar, reduzir `maxObjetos`.

## 5. Estilo "modelo científico" e "Zetta, modo estrutura" — feita (só Editor)
- `EstiloCientifico.cs`: formas matemáticas (completo) e modelos prontos (modo leve) ganham grade azul-ciano translúcida com nervuras internas (arestas da malha, ~1300 por parte, sem diagonais), 2–4 feixes verde/ciano que correm pela superfície com rastro curto, montagem progressiva (2,4 s), etiquetas flutuantes (nome, parâmetros, "cálculo N%" até 100%), 3 marcadores em losango com linhas de chamada e painel de vidro "MODELO CONCLUÍDO" (9 s; não aparece ao restaurar da âncora). Linhas compartilhadas, sem pós-processamento. Ferramenta `modo_estrutura` (ligar/desligar) = "Zetta, modo estrutura". Informações reais por forma no dicionário `Infos`.
- Corrigido junto: etiquetas e marcadores contavam nos limites do objeto (caixa crescia a cada quadro e o objeto sumia na lixeira). Agora `ExcluirDosLimites` + `InteracaoMaos.ContaNosLimites`.
- Captura: `Docs/capturas-redesign/r6-modo-cientifico-klein.png` (e `-toro.png`, primeira versão). Aviso no Console "Concave Mesh Colliders ... dynamic Rigidbody" ao pegar partes (só aviso; colisor não convexo nas partes).
- **Limites:** modo estrutura não é salvo com a âncora; nada testado no headset; custo no Quest não medido.

## 6. Infográfico do DNA — feita (só Editor)
- `DnaHolo.cs`: ferramenta `mostrar_dna` (mostrar / bases / explicar / fechar), fábrica `dna_info` (salva com a âncora). Hélice vertical ciano-azul (2,5 voltas, 26 pares ≈ 10,5 por volta) sobre base de 3 anéis, fitas com nós luminosos (octaedros), pares de bases como degraus de luz (A-T e C-G em partes separadas; `bases` colore A dourado, T laranja, C verde, G rosa), 16 hexágonos translúcidos ao fundo, 3 chamadas em cotovelo ligando a hélice a painéis (FITA, PAR DE BASES, GENE) com info real e curta (2 pontes de H em A-T, 3 em C-G, ~10,5 pares/volta, ~20 mil genes, ~3,1 bilhões de pares) e gráfico "EXEMPLO ILUSTRATIVO" de % de G+C (valores aproximados, rotulado como exemplo). `explicar` acende as chamadas em sequência e devolve os fatos para a Zetta falar ("Zetta, explique o DNA").
- Partes: fita A, fita B, pares A-T, pares C-G, base, hexagonos, chamadas (todas pintáveis, escondíveis, separáveis e salvas com a âncora). `Docs/referencia-dna.png` não existe; o visual é original.
- Captura: `Docs/capturas-redesign/r6-dna-infografico.png`.

## 7. Matemática holográfica — feita (só Editor)
- `ExpressaoMatematica.cs`: parser de descida recursiva, sem eval/reflexão (x, y, t, pi, e; + - * / ^; multiplicação implícita; sen/cos/tan/exp/ln/log/raiz/abs/pot...; limite de 240 caracteres e 48 níveis; erros em português). Teste automático (22/22 testes).
- (a) `desenhar_superficie` (z = f(x,y)): malha 40×40 em luz, cores por altura, eixos numerados, giratória/escalável (`MatematicaHolo.cs`). Captura `r6-superficie-sen-cos.png`.
- (b) `grafico_funcoes`: até 6 curvas sobrepostas em cores diferentes, com legenda.
- (c) `resolver_passo_a_passo` (`ResolucaoCalculadora.cs`): painel com problema, etapas reveladas uma a uma (a Zetta explica e chama `proxima`), ANTERIOR/PRÓXIMA/FECHAR por toque, resposta em destaque. Captura `r6-resolucao-passo-a-passo.png`. O Gemini é quem resolve; o painel só mostra.
- (d) `calculadora_holografica` + `calcular`: 5×6 teclas por toque com o mesmo parser.
- (e) `geometria_interativa` (`GeometriaHolo.cs`): triângulo, quadrilátero e tetraedro com vértices pegáveis e medidas ao vivo (lados em cm, ângulos, área/volume).
- Também: as telas deste grupo e do DNA usam `DestroyImmediate` no colisor das quads (acaba o aviso "Concave Mesh Colliders" com Rigidbody).
- **Limites:** nada validado no headset; calculadora/geometria/resolução abrem na mesma região à frente (se abrir várias ao mesmo tempo se sobrepõem); gráficos de funções/geometria/calculadora não passaram por captura individual; não há âncora para o modo estrutura.

# 🌟 RESUMO FINAL — RODADA 5 (28 tarefas: A1–A5, B6–B17, C18–C22, D23–D28)
**Estado:** as 28 tarefas foram feitas e commitadas localmente (um commit por tarefa/grupo, `778941b` … `fca2084`). Console sem erros; 19/19 testes automáticos passam; build de conferência Android em `Builds/Android/Conferencia.apk` (**NÃO instalado** no Quest; nenhum push; nada publicado). Nada foi validado com o headset: tudo abaixo foi conferido só no Editor (capturas em `Docs/capturas-redesign/`).

## O que entrou
- **A (visual):** vidro mais forte com aura, central em arco com profundidade, botões reais, sons procedurais.
- **B (funções):** formas matemáticas, vista explodida, gráficos 3D com dados reais, escaneamento, bom dia, modo foco, gestos de duas mãos, linha do tempo, etiquetas da sala, portal do clima, apresentação de modelos, sons de criar/apagar.
- **C (qualidade):** `AssistenteVoz` em 5 arquivos parciais (sem mudar comportamento); `EstadoZetta` (máquina de estados única); testes automáticos (`Zetta > Rodar testes` → `Docs/TESTES-ZETTA.md`); painel de desempenho; cache de modelos 3D por descrição.
- **D (ferramentas de desenvolvedor, tudo por voz):** `mostrar_codigo` (código flutuante somente leitura, cores, rolagem por botões/voz/dedo), `mostrar_mapa_projeto` (57 scripts como blocos de luz, 222 fios de dependência; tocar abre o código), `abrir_ajustes`/`ajustar_visual` (sliders de velocidade, brilho, tamanho e cor da esfera; opacidade e borda dos painéis; salvos em PlayerPrefs), `criar_regra`/`mostrar_regras`/`apagar_regra` (programação por blocos: "quando eu disser X, faça Y" só com ferramentas existentes), `mostrar_erros` + alertas vermelhos perto do sistema que falhou, `pedir_ao_pc`/`mostrar_fila_pc` (ponte PC).

## Segurança e decisões
- **Ponte PC (D28):** servidor `Docs/ponte-pc/servidor.py` (Python, só biblioteca padrão). **Não executa nada**: só guarda pedidos em `Docs/pedidos-zetta.md`. Senha de 32 caracteres gerada em `Zetta > Ponte PC > Gerar senha`, gravada só em `Assets/Segredos/ZettaSegredos.asset` e `Docs/ponte-pc/.senha` (ambos no `.gitignore`; confirmado que a senha não está em nenhum commit nem aparece no Console). Exige senha, só aceita IPs da rede local, bloqueia 5 erros por 5 min, 30 pedidos/min. É **HTTP sem criptografia**: só rede doméstica. Testado 12/12 (autenticação, bloqueio, fila, senha fora dos logs) e ponta a ponta Unity → servidor. Como iniciar: `Docs/ponte-pc/LEIA-ME.md`. Usei `HttpClient` (como o `GradioClient`), então **não mudei** `insecureHttpOption` nem o manifest. Para o APK ter a senha/endereço é preciso gerar um build novo depois de gerar a senha (o build de conferência desta rodada já foi feito com a senha gerada).
- **Código empacotado (D23/D24):** `Assets/Resources/Codigo/*.txt` é gerado antes de cada build (`Zetta > Exportar código`; está no `.gitignore`). Só `Assets/*.cs` de topo; `Editor/`, `Segredos/` e a classe `ZettaSegredos` ficam de fora; padrões de token/chave são apagados (`[removido]`). Verificado por busca: nenhum `ghp_`/`hf_`/`AIza` no export.
- **Regras (D26):** só ferramentas já registradas; proibidas `criar_regra`, `apagar_regra`, `mostrar_regras` e `gerar_modelo_3d` (recursão/custo). Disparam só se a Zetta foi chamada no turno ("Zetta, boa noite").
- **Testes (C20):** harness de Editor porque os assemblies de teste da Unity não enxergam o `Assembly-CSharp`.
- **Alertas (D27):** o mesmo erro só avisa a cada 10 s (contador ×N), máx. 4 avisos na tela e 6 novos por 10 s.

## Desempenho (medido só no Editor; Quest não medido)
- Editor: ~211 FPS, 4,7 ms/quadro, esfera 0,075 ms de CPU. Materiais compartilhados (5 no mapa), fios do mapa numa única malha de linhas, sem pós-processamento; ajustes são só 2 globais de shader e 3 multiplicadores.
- No Quest: abrir "Zetta, mostre desempenho" (FPS, ms, memória) e conferir com OVR Metrics.

## Testar no Meta Quest 3, em ordem (instale o APK só quando quiser: `adb install -r Builds/Android/Conferencia.apk`)
1. Esfera pequena no canto inferior direito; resposta da Zetta em branco acima dela; os estados mudam giro/brilho (espera, ouvindo, pensando, falando).
2. "Zetta, abra a central": arco ao redor da cabeça, botões afundam e brilham ao toque; recolhe em ~15 s; pinça na esfera alterna. (Conferir de novo o relato "abre e fecha logo em seguida".)
3. "Zetta, mostre desempenho": FPS ≥ 72 com a central aberta e fechada.
4. "Zetta, crie um nó toroidal / garrafa de Klein / DNA": forma procedural; "vista explodida"; "gráfico da temperatura da semana".
5. "Zetta, bom dia" (hora, notas, timers, clima, notícias); "modo foco" e "sair do modo foco".
6. Gestos: abrir as duas mãos afasta os objetos; pinça dupla longa (~1 s) recolhe.
7. "Zetta, etiquetas da sala" (precisa dos planos da sala do Quest), "portal do clima", "linha do tempo", "me explica o sistema solar".
8. "Zetta, abra os ajustes": arrastar o dedo nas barras (velocidade, brilho, tamanho, cor, opacidade, borda); fechar e reabrir o app mantém os valores; "restaurar o visual".
9. "Zetta, mostre o código da esfera": painel legível, rolar com os botões e arrastando o dedo no texto; "desça".
10. "Zetta, mostre o mapa do projeto": tocar num bloco abre o código dele; "feche o mapa".
11. "Zetta, quando eu disser boa noite, ative o modo foco": depois "Zetta, boa noite" executa; "mostre as regras" (TESTAR/APAGAR).
12. Forçar um erro (ex.: desligar o Wi-Fi) e ver o aviso vermelho perto da esfera; "Zetta, mostre os erros".
13. Ponte PC (opcional): `python Docs/ponte-pc/servidor.py` no PC (mesma rede Wi-Fi; liberar o Python na rede privada), "Zetta, peça ao PC para …" e "mostre a fila do PC".
14. "Zetta, crie um modelo 3D de um gato" duas vezes: a segunda deve aparecer na hora (cache).

## Pendências / limites conhecidos
- Nada com mão real, voz real nem a rede do Quest foi testado. Rolagem do código por dedo, sliders, toque nos blocos do mapa e botões dependem do XR Hands (não existe no Editor).
- O visualizador de código corta linhas em 68 colunas (rolagem só vertical).
- O mapa usa dependências por nome de tipo (pode mostrar ligações a mais quando um nome aparece só em comentário).
- Etiquetas da sala dependem das classificações dos planos do Quest.
- Avisos antigos do Console (CS0184, CS0618, UAC1001) permanecem; não foram introduzidos nesta rodada.

# 🎨 RELATÓRIO FINAL — REDESIGN ZETTA (Direção B: assistente espacial sofisticado)
1. **Resumo:** o visual passou a ter um sistema único (tema + shader de vidro + fonte), os painéis viraram vidro holográfico e nasceu a **central sob demanda** (cabeçalho, aplicativos, barra de comandos). Em repouso só a esfera e o rótulo de status dela aparecem. Nada de lógica foi alterado (voz/Gemini, mãos, câmeras, âncoras, 3D, Meta SDK não instalado). Nenhum push, nada publicado, nada instalado no Quest.
2. **Melhorias visuais:** paleta oficial (`TemaZetta`), vidro com translucidez + borda + gradiente + realce no shader (sem desfoque nem pós-processamento), fonte Rajdhani SemiBold (OFL) com acentos, ícones de traço fino, tempos de animação padronizados.
3. **Esfera:** preservada integralmente (10 anéis, fragmentos, faíscas, espiral, estados, cor por voz e persistência). Só: rótulos (fala/status) na fonte do tema e rajada de faíscas ao abrir a central (`EsferaZetta.Realcar`). Continua pequena no canto inferior direito, seguindo o olhar.
4. **Painéis:** `HoloUtil.CriarPainel` agora gera vidro (afeta Info, Timers e Botões); botões com ícone e brilho ao tocar; Timers piscam dourado ao acabar; PainelInfo com ícone e fecha a central ao abrir. Painéis em arco antigos (hora/data/conexão/assistente) desligados; o diagnóstico foi para a barra "Sistema" da central.
5. **Hologramas:** lógica, seleção (cor original + brilho sutil) e pegada intactos. Paleta: "ciano" (padrão), "dourado" e "roxo/violeta" agora usam as cores oficiais; demais cores nomeadas ficam como estavam.
6. **Arquivos criados:** `Assets/TemaZetta.cs`, `Assets/CentralZetta.cs`, `Assets/IconesZetta.cs`, `Assets/Resources/VidroHolo.shader`, `Assets/Resources/ZettaFonte SDF.asset`, `Assets/Fonts/Rajdhani-SemiBold.ttf` + `OFL-Rajdhani.txt`, `Assets/Editor/CriarFonteZetta.cs` (menu Zetta > Gerar fonte), `Docs/capturas-redesign/*`.
7. **Arquivos modificados:** `HoloUtil.cs`, `BotaoHolo.cs`, `TimersHolo.cs`, `PainelInfo.cs`, `InterrupcaoPinca.cs` (pinça na esfera também alterna a central), `AssistenteVoz.cs` (só a fonte dos dois rótulos), `EsferaZetta.cs` (método `Realcar`), `CriadorHolograma.cs` (3-4 cores da paleta), `SampleScene.unity` (CentralZetta adicionada ao objeto "PaineisHolograficos", que ficou desligado), `PLANO-ZETTA.md`, `PROGRESSO.md`.
8. **Commits:** `b7e22bc` (F1-F2), `fc4dd62` (F3), `d5a59bb` (F4), `ad13964` (F5), `f1d2a7b` (F6) e o de F7-F8 (documentação).
9. **Compilação:** Console sem erros (só warnings antigos do projeto); builds Android de conferência com 0 erros após a F3 e ao final (`Builds/Android/Conferencia.apk`, **não instalado**; o `JarvisMR.apk` anterior não foi tocado).
10. **Testes (só no editor):** central abre/fecha e se recolhe sozinha (testado com 1,5 s), painéis Info e Timer com a fonte nova, hologramas coloridos, capturas em `Docs/capturas-redesign/` (as capturas usam uma câmera auxiliar frontal sobre fundo cinza, não o passthrough). **Fase 7 (otimização):** em repouso há menos objetos que antes (4 painéis em arco removidos); a central fica desativada fechada e não recria materiais; shader de vidro simples (1 passe, sem textura, sem grab pass); ~20 LineRenderers de ícones só enquanto aberta. FPS/GPU no Quest **não medidos** (precisa do óculos, OVR Metrics).
11. **Pendências:** (a) o clique/poke na esfera não abre a central, só a pinça; (b) os botões da central não foram tocados com mão real (BotaoHolo é o mesmo mecanismo de antes, mas a posição/profundidade dos novos botões -0,004 m precisa ser conferida); (c) legibilidade dos textos (alguns pequenos: subtítulo, linha de diagnóstico) sobre a sala clara; (d) notas holográficas mantêm o cartão colorido atual (não foram convertidas em vidro, para preservar a cor escolhida); (e) o texto do card de nota/timer pode precisar de ajuste de tamanho; (f) o asset da fonte é dinâmico (glifos extras entram em tempo de execução).
12. **Testar no Meta Quest 3, em ordem** (instale: `adb install -r Builds/Android/Conferencia.apk` — ou refaça o build normal):
   1. Abrir o app: só a esfera dourada no canto e seu rótulo; nenhum painel em arco.
   2. "Zetta, abra a central": cabeçalho (hora/data/Online), Aplicativos à esquerda, barra de comandos embaixo; texto legível, a sala visível através do vidro.
   3. Esperar ~15 s sem tocar: tudo se recolhe sozinho. Reabrir com pinça perto da esfera; "Zetta, feche a central".
   4. Tocar (poke) em Notas / Timers / Sites / Modelos 3D / Informações: a Zetta pergunta por voz e a central fecha; "Desenhar" liga o modo desenho; PARAR interrompe a fala; FECHAR fecha.
   5. "Zetta, clima em São Paulo": painel de vidro com ícone e botão X; "Zetta, timer de 1 minuto": cartão de timer (pisca dourado ao acabar).
   6. Criar cubo azul, esfera dourada, cone roxo: montagem, pegar (cor igual, brilho sutil), apagar.
   7. Cor da esfera ("mude sua cor para roxo") e persistência ao reabrir.
   8. Medir FPS (OVR Metrics) com a central aberta e fechada; se cair, reduzir/remover a linha de diagnóstico e os LineRenderers dos ícones.
- **Licença da fonte:** Rajdhani © Indian Type Foundry, SIL Open Font License 1.1 (arquivo `Assets/Fonts/OFL-Rajdhani.txt`).

# 🎨 RODADA 5 (pedido de 28 tarefas) — andamento
## A (visual) — feita
- A1: vidro #102C46 ~70%, borda #42C8FF mais grossa (3,2 mm) com aura aditiva (saída pré-multiplicada `Blend One OneMinusSrcAlpha`, quad com margem de 2 cm), textos #EAF6FF. A2: esfera dourada = #E8B95E (borda) e núcleo #FFF5CC, brilho ×2,3 (antes saía laranja `(1,0.45,0.04)`).
- A3: resposta da Zetta em #EAF6FF, maior e **acima da esfera** (alinhada à direita), fora da central; textos da central ≥ 0,18 (TMP `fontSizeMin`), painéis sem sobreposição (conferido por captura `central-3.png`).
- A4: central em arco ao redor da cabeça, 2 camadas (0,9 m apps/comandos; 1,1 m cabeçalho/sistema), cada painel voltado para o usuário, lâmina de espessura 9 mm atrás do vidro, textos/ícones 1,4 cm e botões 2 cm à frente (paralaxe), 26 partículas de luz entre as camadas, entrada de trás para frente. `sortingOrder` fixa a ordem vidro < botões < textos (corrigiu botões escurecidos).
- A5: `BotaoHolo` com BoxCollider (gatilho), acende quando o dedo se aproxima, afunda ao toque, clique sonoro (`SonsZetta`). Mantive a detecção por XR Hands (mesma de antes) em vez do poke do XRI: o XRI conflitaria com o grab dos hologramas. Sites=listar_sites, Modelos 3D=modelos_salvos, Notas=gerenciar_notas mostrar, Timers=resumo dos ativos, Desenhar=liga/desliga, Informações=clima+notícias; PARAR e FECHAR por toque.
- Build de conferência: ver fim da seção.
## B (funções novas) — feitas (verificadas no Editor por captura/Console; nada testado no headset)
- B6 `FormasMatematicas` (`criar_forma_matematica`: mobius, klein, nó toroidal, DNA, toro, fractal; malhas procedurais normalizadas a 1 m). B7 `VistaExplodida` (`vista_explodida`). B8 `GraficoHolo` (`grafico_temperatura_semana` com Open-Meteo real e `criar_grafico`).
- B9 `EscaneamentoHolo` (varredura após a foto da câmera). B10 `BriefingBomDia` (`bom_dia`: hora, notas, timers, clima, notícias). B11 `ModoFoco` (`modo_foco`: só um timer grande, o resto some e volta ao sair). B16 `SonsAmbiente` (sons de criar/apagar ligados ao `HoloRegistro`).
- B12 `GestosDuplos` (duas mãos abrindo afastam os objetos; pinça dupla longa recolhe tudo). B13 `LinhaDoTempo` + `DiarioZetta` (log do dia em PlayerPrefs). B14 `EtiquetasSala` (classificações dos ARPlane: mesa, cadeira, chão, teto, parede). B15 `PortalClima` (portal com o clima real). B17 `ApresentacaoModelo` (`apresentar_modelo`: destaca cada peça enquanto a Zetta explica).
- Todos os módulos novos entram pela lista de `ZettaExtras` (sem editar a cena) e registram as ferramentas no Awake.
## C (arquitetura e qualidade) — feitas
- C18: `AssistenteVoz` virou `partial class` em 5 arquivos (`.cs` núcleo/estado, `.Conexao`, `.Audio`, `.Rotulos`, `.Ferramentas`); movimentação mecânica de métodos, sem mudar comportamento (compila sem erros, 37 ferramentas continuam registradas).
- C19: `EstadoZetta` (Espera/Ouvindo/Pensando/Falando/Erro), calculado uma vez por quadro; `EsferaZetta` e o texto de estado da `CentralZetta` só leem dele; evento `aoMudar`. `AssistenteVoz.Processando` exposto.
- C20: testes automáticos como harness de Editor (menu `Zetta > Rodar testes`, relatório em `Docs/TESTES-ZETTA.md`; 16/16 passam, incluindo o registro das ferramentas em Play). Motivo de não usar asmdef de teste: assemblies de teste da Unity não referenciam o Assembly-CSharp.
- C21: `DesempenhoHolo` (`mostrar_desempenho`): FPS, ms médio e pior (2 s), memória, nº de hologramas; borda verde/dourada/vermelha; canto inferior esquerdo. No Editor: ~211 FPS, 4,7 ms (CPU do PC, não representa o Quest).
- C22: cache de modelos 3D por descrição (`Modelos3D/cache.json`; chave sem acento/pontuação/maiúscula; entrada com arquivo sumido é limpa). Só vale para "descricao" (a foto da câmera é sempre única).

## D (ferramentas de desenvolvedor) — feitas
- D23 `CodigoHolo` + `ExportarCodigoZetta` (cópia somente leitura em Resources/Codigo, sem segredos). D24 `MapaProjeto` (layout de forças em 3D, 1 malha de fios). D25 `AjustesZetta`/`SliderHolo`/`AjustesHolo` (globais `_ZettaOpac`/`_ZettaBorda` no shader do vidro; a esfera lê velocidade/brilho/tamanho). D26 `RegrasHolo` (+ `AssistenteVoz.ChamarFerramenta`, `TemFerramenta`, evento `aoFraseCompleta`). D27 `AlertasErros`. D28 ponte PC (`servidor.py`, `PontePcEditor`, `PonteHolo`, campos `senhaPonte`/`enderecoPonte` no `ZettaSegredos`).
# 🎨 REDESIGN ZETTA (Direção B) — andamento por fase
## Fase 1 — Auditoria
- Git: branch main; só havia `.utmp/...fingerprint.bin` modificado (artefato do Gradle, não é meu) e `Docs/` não rastreado. Compilação inicial: 0 erros (só warnings antigos).
- Visual: `HoloUtil.CriarPainel` (painéis: Info, Timers, Botões), `PaineisHolograficos` (4 painéis em arco sempre visíveis: hora, data, conexão, assistente), `NotasHolo` e hologramas via `CriadorHolograma.AplicarVisual`, esfera em `EsferaZetta` + shader `EsferaAditiva`, rótulos em `AssistenteVoz.CriarTexto`.
- Lógica (não mexer): `AssistenteVoz` (Gemini Live), `InteracaoMaos`/`InterrupcaoPinca` (XR Hands), `CameraOlhos`, `AncorasHolo`, `GeradorModelo3D`, `ApagadorHolo`. `InterrupcaoPinca.PincaNoAnel` já detecta pinça na esfera (reaproveitada para abrir a central).
- Dependências dos painéis: `BotaoHolo` e `TimersHolo` liam o `LineRenderer` "Contorno" (agora a borda é do shader).
## Fase 2 — Sistema visual
- `TemaZetta.cs` (paleta oficial, medidas, tempos, materiais), `Resources/VidroHolo.shader` (vidro: translucidez + borda + gradiente + realce, sem grab pass), `PainelVidro` (cor da borda/realce por painel), `HoloUtil.CriarPainel` agora usa o vidro, fonte **Rajdhani SemiBold** (licença SIL OFL 1.1, Indian Type Foundry; texto em `Assets/Fonts/OFL-Rajdhani.txt`) com TMP Font Asset `Resources/ZettaFonte SDF` gerado por `Zetta > Gerar fonte` (acentos do português; só faltaram º e ª).

## Fase 3 — Painéis e central sob demanda
- `CentralZetta.cs` (novo): cabeçalho (ZETTA, hora, data, Online/Sem rede), painel Aplicativos (Sites, Modelos 3D, Notas, Timers, Desenhar, Informações), barra de comandos (PARAR, microfone com medidor e "Ouvindo.../Falando.../Pronta/Em espera", FECHAR) e barra "Sistema" com o diagnóstico que estava nos painéis em arco (Gemini, mic, passthrough, falha do 3D). Abre por voz (`abrir_central`/`fechar_central`), por pinça na esfera (`InterrupcaoPinca.PincaNoAnel`) e se recolhe sozinha após 15 s sem interação (dedo em botão ou Zetta falando). Os botões de app só pedem à Zetta (por `Falar`) a ação já existente; "Desenhar" liga/desliga o `DesenhoNoAr`. Montada uma vez e só liga/desliga (sem recriar materiais).
- `PaineisHolograficos` (4 painéis em arco sempre visíveis) ficou no projeto **desligado** na cena (em repouso só a esfera e o rótulo de status dela). `IconesZetta.cs` (ícones de traço fino), `BotaoHolo` (ícone opcional, vidro), `PainelInfo` (ícone, título/corpo na fonte nova; fecha a central ao abrir), `TimersHolo` (vidro, pisca dourado ao acabar).
- Capturas: `Docs/capturas-redesign/` (central-1/2, paineis-info-timer). Build de conferência Android: 0 erros (`Builds/Android/Conferencia.apk`, não instalado).
- Limite: "toque" (poke) na esfera não abre a central, só a pinça. Notas (`NotasHolo`) mantêm o cartão colorido atual (cor escolhida pelo usuário), só ganham a fonte nova.

## Fases 4 a 8 (resumo; detalhes no relatório do topo)
- F4 esfera: só fonte dos rótulos + `EsferaZetta.Realcar()`. F5 hologramas: paleta oficial (ciano/dourado/roxo). F6 animações: tempos e ease do `TemaZetta` em central, `HoloUtil.Surgir` e saída do PainelInfo. F7 otimização: revisão feita, sem custos novos em repouso. F8 validação: Console sem erros, build de conferência 0 erros; pendências só validáveis no Quest.

# 🌙 RESUMO DA RODADA 4 – compila; build de conferência 0 erros; NADA instalado no Quest
- **Tarefa 1 (esfera da Zetta) feita.** Tarefas 2–6 do pedido já estavam concluídas na Rodada 3 (cor ao pegar, visão preta, notas, regra de limites, 3D de ponta a ponta) e foram puladas; ver o bloco "RODADA 3" abaixo. A miniatura da foto (tarefa 3a) agora aparece ao lado da nova esfera (usa a posição do `AnelHolografico`).
- **Esfera:** `EsferaZetta.cs` + shader `Resources/EsferaAditiva.shader`, criada pelo `AnelHolografico` (que continua cuidando de posição/seguir o olhar; `NucleoEnergia` ficou no projeto, desligado).
  - 10 anéis/arcos (contínuos, tracejados, arcos em degradê) em eixos e velocidades próprios, 56 fragmentos/traços de dados piscando, 28 faíscas com rastro de 3 pontos nas órbitas, núcleo em espiral (48 pontos) + brilho central. Gradiente núcleo quase branco-dourado → borda laranja, blend aditivo.
  - Estados (do `AssistenteVoz`): espera = giro lento e brilho fraco; ouvindo (mic > 0,04) = anéis encolhem ~28%; processando (até 6 s após a fala do usuário) = giro 3×; falando = pulso pelo `volumeSaida` + rajada de faíscas nos picos; erro (`ultimoErro` novo) = um pisca vermelho de 0,9 s.
  - Cor: `mudar_cor_esfera` ("mude sua cor para dourado/ciano/verde/vermelho/roxo", também azul/laranja/rosa/branco), salva em PlayerPrefs `zetta_cor_esfera`, transição suave.
  - Desempenho: 1 material compartilhado com instancing, 3 malhas de anel compartilhadas, 189 quads no total (<300), sem pós-processamento; CPU medida no editor: **0,075 ms/quadro** (log `EsferaZetta: CPU média…`). Custo de GPU/FPS no Quest: não medido (precisa do óculos; OVR Metrics).
  - Verificado no editor por captura de tela (âmbar e roxo). Passthrough: o shader usa `Blend SrcAlpha One, One One` (alfa acumula para o compositor); a aparência real sobre a sala só se vê no Quest.
  - Texto de status continua embaixo da esfera e a resposta falada à esquerda (como antes).

## Testes no óculos, em ordem (instale: `adb install -r Builds/JarvisMR.apk`)
1. Abrir o app: esfera pequena dourada no canto inferior direito, giro lento (espera). Ela deve ser legível sobre a sala clara e escura; se sumir sobre fundo claro, aumentar `_Brilho`/alfa.
2. "Zetta, olá": brilho sobe; falar perto dela e ver os anéis encolherem ("ouvindo"), depois girar rápido ("processando"), depois pulsar com a voz e soltar faíscas.
3. Provocar um erro (tirar o Wi‑Fi): um pisca vermelho.
4. "Zetta, mude sua cor para roxo/verde/ciano"; fechar e reabrir: a cor continua.
5. Medir FPS (OVR Metrics) com a esfera ativa; se cair, reduzir `NFaiscas`/`NEspiral`.
6. Depois, a lista da Rodada 3 abaixo (cor ao pegar, "o que você vê?", notas, 3D).


# 🌙 RESUMO DA RODADA 3 (pedido a9f4 nº 2) – tudo compila; builds de conferência 0 erros; NADA instalado no Quest
1. **Cor ao pegar:** causa = `BrilhoProximidade` misturava com branco; agora só sobe o brilho (até +20%) na mesma cor.
2. **Visão preta:** `CameraOlhos` com aquecimento, teste de brilho, Blit+ReadPixels, tenta todas as câmeras, salva `ultima_foto.jpg` e mostra miniatura por 5 s.
3. **Notas:** letra no tamanho dos painéis, cartão cresce em altura, `editar_nota`, estado (texto/letra/cor/escala) salvo na âncora.
4. **Limites:** a Zetta explica o que consegue fazer de parecido.
5. **Modelo 3D:** funciona de ponta a ponta no editor (imagem 5–9 s → TRELLIS.2 33–81 s → GLB → glTFast 0,2 s). Causa da falha anterior: GLB com texturas WebP que o glTFast não lê; agora as texturas são trocadas por uma cor única (modelo sem textura). Para textura real: plugin nativo libwebp (ver R3-5).

## Testes no óculos, em ordem (instale antes: `adb install -r Builds/JarvisMR.apk`)
1. Criar um cubo azul e pegar: deve continuar azul (um pouco mais claro), nunca branco; soltar volta ao normal. Repetir com modelo pronto ("sistema solar") e nota.
2. "Zetta, o que você vê?": aparece uma miniatura da foto por 5 s ao lado do anel; se vier preta, `adb logcat -s Unity | findstr CameraOlhos` (câmeras e brilho) e `adb pull /sdcard/Android/data/com.luiz.jarvismr/files/ultima_foto.jpg`.
3. "Zetta, anote: comprar leite": cartão a ~0,7 m, letra legível; "letra maior", "acrescente: e pão", "mude a cor para amarelo", "cartão maior"; escalar com duas mãos; soltar na parede (gruda); fechar e reabrir o app (texto, cor, tamanho e lugar voltam).
4. Pedir algo impossível ("toque uma música"): ela deve dizer o que consegue fazer de parecido.
5. "Zetta, crie uma caneca realista" (1–2 min): o modelo aparece cinza/cor única, sem textura; "mostre meus modelos".
6. Depois: lista da Rodada 2 em `AVALIACAO.md` (apagar objetos, sites, âncoras, FPS).


# 🌙 RESUMO DA RODADA 2 (autônoma)
- **Feitas e commitadas** (cada uma com Console sem erros e build Android de conferência com 0 erros): tarefas 6 (frases cortadas: NO_INTERRUPTION + mic fechado durante o turno), 7 (visão com `CameraOlhos` + permissões), 8 (apagar objetos: ids, `listar_objetos`, sinônimos, desenhos, "isso" pela mão/olhar), 9 (notas holográficas que grudam em paredes/mesas e têm âncora), 10 (`AVALIACAO.md`). Tarefas 0–5 já estavam prontas.
- **Nada foi instalado no Quest** (como pedido): o APK instalado ainda é o do commit `e156519`. Tudo desta rodada só foi testado no editor.
- **Você precisa:** instalar o APK novo (`adb install -r Builds/JarvisMR.apk`), girar o token do GitHub (foi colado em arquivo antes de ir para o asset) e seguir a lista de testes no fim de `AVALIACAO.md`.
- **Parcial/riscos:** eco da voz (sem AEC real), 3D depende de cota gratuita, câmera/planos/âncoras/mãos só validam no óculos. Detalhes por tarefa na seção "RODADA 2" no fim deste arquivo.


# 🌐 ESTÚDIO DE SITES – PUBLICAÇÃO NO GITHUB PAGES (testada de verdade)

## Pronto
- `ZettaSegredos` (fora do git): campo `githubToken` (o valor antigo de `tokenGitHub` foi preservado por `FormerlySerializedAs`) e `githubRepositorio` = `escanor00/zetta-sites`. O token está só no asset `Assets/Segredos/ZettaSegredos.asset`, que o `.gitignore` ignora.
- **Atenção:** ao colar, o token foi parar dentro de `ZettaSegredos.cs` (no atributo `FormerlySerializedAs`). Movi para o asset e limpei o `.cs`; **nunca foi commitado** (`git grep` limpo). Cole sempre pelo Inspector, no campo `Github Token` do asset. Como o valor apareceu no texto desta sessão, se for compartilhar a conversa ou os logs, gere um token novo no GitHub.
- Fluxo `criar_site`/`atualizar_site`: Gemini gera o HTML → confere o repositório e a permissão de escrita do token → `PUT /contents/<site>/index.html` → painel "publicando…" com o contador → consulta a URL (a cada 4 s, até 90 s) até responder 200 **com a marca desta publicação** → painel "no ar!" → **abre sozinha no navegador do Quest** (intent Android `ACTION_VIEW`; no editor abre no navegador do PC) → a Zetta avisa por voz.
- Se passar de 90 s: aviso "a publicação demorou" (voz + rótulo do anel + painel) e uma espera silenciosa de até 5 min que abre o link quando a página subir (o Pages levou ~3 min numa atualização).
- Erros explicados por voz e no status: falta o token (`sem_token`), token inválido/vencido (401), sem permissão (403), repositório não encontrado (404), limite do GitHub (429), sem conexão, demora e erro genérico (`http_N`).
- Corrigido bug antigo do `PainelInfo` (`MissingReferenceException` ao trocar de painel): agora atualiza o texto no lugar quando o título é o mesmo e para as animações antes de recriar.

## Testado no editor com o token real
1. Repositório público, branch `main`, site base `https://escanor00.github.io/zetta-sites/` responde 200; `PUT` sem token dá 401 (mapeado para "token inválido").
2. `criar_site` "Teste Zetta" → publicado → `https://escanor00.github.io/zetta-sites/teste-zetta/` respondeu **HTTP 200** com a marca da publicação.
3. `atualizar_site` → commit "atualiza teste-zetta" no GitHub e a página refletiu a mudança (~3 min no Pages; acima dos 90 s, por isso a espera tardia).
4. `apagar_site` → commit "apaga teste-zetta"; repositório limpo. Sem token: "falta o token do GitHub" (painel + rótulo).
- **Não testado:** abrir o navegador pelo intent dentro do Quest, e o que acontece com o app imersivo quando o navegador abre (o Quest pode pausar o app: mic e anel param até você voltar; o app reconecta ao retomar).

## Testes no óculos, em ordem
1. "Zetta, crie um site sobre o sistema solar": painel "publicando…" com contador e, em ~1–3 min, o navegador abre sozinho com o site.
2. Ao voltar ao app, conferir se a Zetta ouve (reconexão após pausa) e o anel está de volta.
3. "Liste meus sites", "adicione uma seção sobre Marte" (aguarde o Pages), "apague o site do sistema solar".
4. Teste de erro: apagar o token no asset, refazer o build e pedir um site: ela deve dizer que falta o token.

---

# 🧊 DIAGNÓSTICO DO MODELO 3D (último pedido que falhou)

**Etapa que falhou: a PRIMEIRA, a geração da imagem** (não é o Gemini: a imagem vem do Space gratuito **FLUX.1-schnell**), por **cota do ZeroGPU do Hugging Face**: `You have exceeded your ZeroGPU quota (90s requested vs. 0s left)... Authenticate with a Hugging Face token for more quota`. O plano B (Shap-E) recebeu a mesma recusa. Ou seja, o TRELLIS.2 nem chegou a ser chamado; o endpoint não mudou e o download/glTFast não foram exercitados. Reproduzido no editor com a mesma conexão (o logcat do Quest só tinha 5 linhas do Unity, sem registro da sua tentativa; a causa bate com a do teste da Fase 6).
- Qualquer chamada a Spaces com ZeroGPU sem token usa a cota anônima por IP, que está zerada ("0s left").

## O que mudou
- **Etapa e erro à vista:** o holograma "gerando…" mostra a etapa e o andamento; em caso de falha aparecem (1) o painel "Falha no modelo 3D" com "Etapa: …" e o erro por 30 s, (2) o rótulo embaixo do anel "3D falhou: <etapa>" por 8 s, (3) a linha `3D: etapa: erro` no painel CONEXÃO, (4) o log `GeradorModelo3D: falhou em [etapa]`. As etapas distinguem: foto da câmera, geração da imagem (FLUX), TRELLIS.2 com a subetapa (`[sessão]`, `[envio da imagem]`, `[preprocess_image]`, `[image_to_3d]`, `[extract_glb]`, `[download do GLB]`), Shap-E (plano B) e carregamento do GLB (glTFast, que antes só ia para o log).
- **A Zetta explica por voz** conforme a causa: cota do Hugging Face (e como resolver com token), endpoint/interface mudou (404), serviço lotado/fora do ar (503/429/fila), falha de download, câmera sem permissão, falha ao abrir o GLB, erro inesperado.
- **Token do Hugging Face:** `GradioClient` envia `Authorization: Bearer <token>` quando `ZettaSegredos.tokenHuggingFace` está preenchido, só para hf.space/huggingface.co, e o token nunca vai para log, painel ou fala. Vale para FLUX, TRELLIS.2 e Shap-E.

## O que você precisa fazer
1. Criar a conta grátis em huggingface.co → Settings → Access Tokens → token do tipo **Read** (fine-grained sem escopos extras também serve).
2. Colar no asset `Assets/Segredos/ZettaSegredos`, campo `tokenHuggingFace` (nunca em arquivos versionados) e refazer o build.
3. Testar: "Zetta, crie uma poltrona vermelha realista". Se mesmo com token a cota esgotar, ela avisa que é limite diário.
4. Alternativa sem conta: tentar mais tarde; a cota anônima é por IP e se renova.

---

# 🔧 ATUALIZAÇÃO MAIS RECENTE (correções pós-teste no Quest)

## Pronto (código + checagem no editor; Console sem erros; build de conferência 0 erros)
1. **Véu azul com pontinhos** – causa mais provável: os **planos da sala** (`ARPlane.prefab` do MR Template, textura `PlanePatternDot` azul com pontos) que o `SalaAR` liga para física/mesa; o template reativava o renderer. Correção: `SalaAR.EsconderVisual` usa `Renderer.forceRenderingOff` em todos os renderers do plano (vale mesmo que o template reative o renderer) e é aplicado assim que cada plano aparece (`trackablesChanged`) e a cada 1 s. Conferido: sem skybox (Skybox Material vazio, ambiente "Flat"), câmera SolidColor preto alfa 0, sem Volume/pós-processamento, `Environment` do template com renderer desligado, bounding boxes desligadas, nada do tamanho da visão. `Assets/CompositionLayers` só tem configurações (não há camada de cor/tonalidade); o passthrough é o `ARCameraBackground` (underlay, sem tonalidade).
2. **Pegar pelas bordas** – (a) `ColisorPegada.cs`: caixa pelos bounds dos renderers (incluindo filhos), no espaço local da raiz (segue a escala) e remedida a cada 0,5 s, usada nos modelos prontos (antes eram esferas fixas aproximadas) e nos .glb; primitivas (cubo, esfera, cilindro, cápsula, cone, pirâmide, anel) já tinham colisor exato. (b) `InteracaoMaos.Aplicar`: pegada dinâmica do XRI (`useDynamicAttach`, `matchAttachPosition/Rotation`, `snapToColliderVolume`, reinicia a cada pegada): o objeto mantém o ponto e o ângulo tocados. Sistema de mãos ativo: XR Interaction Toolkit + XR Hands (Near-Far); o Meta Interaction SDK não está instalado. (c) `BrilhoProximidade.cs`: clareia e aumenta a opacidade quando a ponta do indicador/polegar chega a 10 cm da superfície (máximo a 2 cm) e enquanto segura. (d) duas mãos para escalar/girar mantidas.
3. **Ativação pelo nome** – já feita no ajuste anterior (filtro `EhChamado`, instrução da Gemini, pinça sobre o anel, "diga 'Zetta' antes" 2 s, conversa por 8 s). (e) Conferido no editor com a chave real: sessão Gemini Live "Conectado", pedido de fala → resposta com transcrição "Teste de áudio ok." (áudio chegou e foi tocado; no óculos o rótulo mostra "falando" enquanto toca, ou o erro).
4. **Menu "Player Settings" do template** (Hand Menu Setup) desativado na cena e também por `PassthroughMR` na inicialização.
5. **Lixeira** – 0,9 m abaixo da cabeça (nunca abaixo de 0,7 m do chão), 0,4 m à direita e 0,35 m à frente; segue o corpo suavemente quando você anda e só reorienta devagar se você girar a cabeça mais de 40°; tamanho/brilho ao aproximar um objeto mantidos. Conferido no editor: 0,90 m abaixo da cabeça.

## O que pode falhar (só o óculos confirma)
- Se o véu continuar, veja `adb logcat -s Unity` e me diga se ele some ao desligar o Space Setup/planos; próxima hipótese: textura de pontos vinda de outro objeto do template.
- Pegada: ajustes finos (distâncias do brilho, `snapToColliderVolume`) dependem do toque real das mãos.
- Âncoras persistentes e planos reais continuam sem validação no óculos.

## Testes no óculos, em ordem
1. Abrir o app: a sala deve aparecer com as cores reais, **sem** véu azul de pontos, e sem o menu "Player Settings" ao virar a mão.
2. Dizer "Z, que horas são?" / "Zé, olá": ela responde e o rótulo mostra "falando" com o áudio tocando.
3. Frase sem chamar: aparece "diga 'Zetta' antes" por 2 s; pinça sobre o anel acorda; depois de responder, 8 s sem precisar do nome.
4. "Crie um cubo": aproximar a mão de qualquer aresta/canto → brilho; pinçar numa quina e mover → o objeto fica preso no ponto tocado, sem pular para o centro. Repetir com cone, anel e "mostre o sistema solar".
5. Duas mãos: escalar e girar; soltar e arremessar devagar.
6. Lixeira: na altura da cintura, à direita e à frente, ao alcance; andar pela sala (ela acompanha); girar a cabeça (ela não gira junto); levar um objeto até ela (brilha e apaga ao soltar).

---

# ☀️ RESUMO DA MANHÃ (noite autônoma, fases 0 a 8)

## O que ficou pronto (tudo compila; builds de conferência das fases 0–8 com 0 erros, git com commits e tags `fase-N`/`antes-fase-N`)
- **Fase 0** segurança: chave do Gemini fora do código e da cena (asset `ZettaSegredos`, fora do git), `.gitignore`, git iniciado.
- **Fase 1** base: passthrough forçado (`PassthroughMR`), template GoalManager desligado, vigia do microfone, reconexão sem fim, status visível, botão PARAR e pinça longa para interromper.
- **Fase 2** utilidades: clima e notícias ao vivo, painéis com X, timers etc.
- **Fase 3** mãos: pegar, escalar com duas mãos, arremessar, botões de toque.
- **Fase 4** sala: planos/mesa, encaixe na mesa holográfica, âncoras.
- **Fase 5** criação: formas novas (cápsula, cone, pirâmide, anel) e `editar_objeto` (mover/cor/tamanho/duplicar).
- **Fase 6** olhos: `olhar` ("o que é isso?"), `gerar_modelo_3d`, `modelos_salvos`.
- **Fase 7** sites: `criar_site`, `atualizar_site`, `listar_sites`, `abrir_site`, `apagar_site`.
- **Fase 8** acabamento: limite de 24 hologramas, sombras desligadas, erros amigáveis.

## O que falhou / ficou parcial
- **Nada foi validado no óculos** (estava dormindo durante o teste): passthrough, microfone, mãos, câmera, âncoras e planos reais são só código + testes no editor.
- Busca do Google indisponível no plano grátis (substituída por clima/notícias).
- Geração 3D (TRELLIS.2/Shap-E) pode ser recusada pela cota anônima do Hugging Face; nunca rodou de ponta a ponta.
- Publicação de sites no GitHub **não testada** (falta o token).
- Pasta de backup `My project - backup fase 0` está incompleta; o git é a proteção real.
- SpaceWarp e materiais compartilhados avaliados e não feitos (motivos na Fase 8).

## O que você precisa fazer
1. Instalar o último APK (`Builds/JarvisMR.apk`) no Quest: `adb install -r Builds/JarvisMR.apk` (o build atual já inclui a Fase 8).
2. **GitHub (Fase 7):** criar conta/token como descrito na seção "Fase 7" e preencher `usuarioGitHub` e `tokenGitHub` no asset `Assets/Segredos/ZettaSegredos`; depois refazer o build.
3. (Opcional) token do Hugging Face em `ZettaSegredos.tokenHuggingFace` se a cota anônima do 3D atrapalhar; hoje o código não o usa.
4. Aceitar, na 1ª vez, as permissões de microfone e de câmeras do headset.

## Testes no óculos, em ordem
1. Abrir o app: deve mostrar a **sala (passthrough)**, nunca fundo azul. Se falhar, ver o painel CONEXÃO e `adb logcat -s Unity` (linhas `PassthroughMR:`).
2. Dois toques no óculos: o app não deve pausar nem mostrar retângulo preto; o anel continua.
3. Dizer "Zetta, olá": ela ouve e responde; conferir o rótulo de status embaixo do anel e o painel ASSISTENTE (nível do microfone).
4. Pinça longa (~1 s) ou botão PARAR interrompem a fala.
5. "Zetta, qual o clima em São Paulo?" e "notícias de tecnologia" (painel com X).
6. "Crie um cubo azul", pegar com a mão, duas mãos para escalar, arremessar devagar.
7. Botões holográficos por toque do dedo.
8. "Ative a mesa" e soltar um objeto perto dela (encaixa).
9. Fase 5: "crie um cone vermelho", "mova para a direita", "pinte de verde", "aumente isso", "duplique".
10. Reabrir o app: objetos voltam no mesmo lugar (âncoras).
11. Fase 6: "o que é isso?" apontando um objeto; "transforme isso em 3D" (espera 1–3 min ou aviso de cota); "mostre meus modelos".
12. Fase 7 (só depois do token): "crie um site sobre…", "abra o site", "adicione…", "transforme em 3D", "liste meus sites", "apague o site".
13. Fase 8: criar ~25 objetos, medir FPS, ler painéis em sala clara e escura.
Detalhes e listas por fase mais abaixo neste arquivo.

---

## Tarefa 0 — Remover Firebase (concluída)
- Apagados: Assets/Firebase, Assets/GeneratedLocalRepo, Assets/Editor Default Resources/Firebase, Assets/Plugins/{iOS,tvOS}/Firebase, Assets/Plugins/Android/FirebaseApp.androidlib, google-services.json (Assets e StreamingAssets).
- mainTemplate.gradle / settingsTemplate.gradle: removidas dependências e repositório local do Firebase; Force Resolve executado (templates regenerados limpos). Removidos também ProjectSettings/AndroidResolverDependencies.xml (cache) e as chaves firebaseAnalytics do GvhProjectSettings.xml.
- Testado: grep sem referências a Firebase em .cs/.gradle/.xml/.json/.unity; Console sem erros após recompilar.
- Pendente: nada.

## Tarefa 1 — Voz em tempo real (concluída; falta testar no óculos)
- Assets/AssistenteVoz.cs: Gemini Live API por WebSocket direto (System.Net.WebSockets). Modelo `gemini-3.8-live` (docs oficiais: grátis no plano Free), endpoint BidiGenerateContent v1beta, setup com AUDIO + transcrição de entrada/saída.
- Microfone 16 kHz PCM enviado em blocos de 100 ms (`realtimeInput.audio`); permissão de microfone pedida em runtime no Android; resposta em PCM 24 kHz tocada num AudioSource via buffer circular; transcrição da resposta no TextoResposta (que segue o olhar e fica abaixo do anel).
- Interrupção: barge-in nativo do servidor (`interrupted` limpa o buffer de áudio na hora). Reconexão automática (3 tentativas, 3 s).
- Chave copiada do TesteGemini via Inspector (sem imprimir; erros do WebSocket são sanitizados). TesteGemini desativado, não apagado.
- Testado no Editor (Play Mode): conectou, recebeu áudio + transcrição ("Olá! Como posso ajudar você hoje?"), Console sem erros.
- Pendente: teste no Quest 3 (microfone, permissão, eco do alto-falante no microfone podendo se auto-interromper).

## Tarefa 2 — Núcleo pulsa com a voz (concluída)
- NucleoEnergia.energia = repouso (0,15) + volume da voz × ganho (2), suavizado; campo `voz` ligado ao AssistenteVoz na cena.
- Testado: compila sem erros; volume da voz medido no Play Mode (>0 quando o áudio toca).

## Tarefa 3 — Painéis em arco (concluída)
- Assets/PaineisHolograficos.cs (GameObject `PaineisHolograficos` na cena): 4 painéis a 1,2 m em arco (-58°, -30°, +30°, +58° em relação à direção do olhar 1,5 s após iniciar): HORA, DATA, CONEXÃO (rede + status do Gemini) e ASSISTENTE (energia da voz). Fundo quase transparente, contorno ciano em LineRenderer, texto TMP; surgem em sequência com animação (ease-out-back), texto atualizado a cada 0,25 s.
- Testado no Play Mode: 4 painéis criados, escala final 1, textos atualizando (hora, data, "REDE OK / Gemini: Conectado"); Console sem erros.
- Pendente: conferir visual no óculos (tamanho/legibilidade; os painéis ficam fixos no mundo, no ponto onde você olhava ao iniciar).

## Tarefa 4 — Hologramas por voz (concluída)
- Assets/CriadorHolograma.cs (GameObject `CriadorHolograma`): registra a ferramenta `criar_objeto` (forma: cubo/esfera/cilindro; cor: nome em português ou hex; tamanho em metros; posicao x/y/z relativa à cabeça, padrão 0 / -0,05 / 0,6) no setup do Gemini Live e trata o toolCall devolvendo toolResponse.
- Cada objeto tem Rigidbody (sem gravidade) + XRGrabInteractable (XRI 3.6, `throwOnDetach` desligado), visual holográfico em Sprites/Default (grade de linhas + preenchimento translúcido). Máximo de 30 objetos (o mais antigo é removido).
- Testado no Play Mode de ponta a ponta: pedi por texto "Crie um cubo azul de 20 centímetros na minha frente" → Gemini chamou `criar_objeto` → cubo 0,2 m azul criado, com XRGrabInteractable e Rigidbody; resposta "Cubo azul criado com sucesso." Também testei cilindro vermelho e esfera `#00FF88` (tamanhos e escala corretos). Console sem erros.

## Tarefa 5 — Efeito de montagem (concluída)
- `HologramaMontagem` (em CriadorHolograma.cs): em ~1 s a grade de linhas sobe de baixo para cima (alfa por vértice) e o preenchimento a segue; leve crescimento 0,92→1.
- Corrigido bug achado no teste (o topo nunca preenchia); verificado: alfa final = 1 em grade e preenchimento nos 3 formatos.

## Extras
- Troquei FindObjectOfType (obsoleto) por FindAnyObjectByType nos scripts do projeto.

---
# RESUMO FINAL
Tarefas 0 a 5 concluídas, sem travas. Console sem erros de compilação; cena salva. Nada de build Android foi feito. AnelHolografico.cs, NucleoEnergia.cs e TesteGemini.cs mantidos (TesteGemini só desativado). A chave de API não foi escrita em logs nem neste arquivo.

## O que você precisa testar no óculos (Quest 3)
1. Primeiro build: confirme que o Gradle resolve sem Firebase (o espaço em "My project" não deve mais quebrar). Não fiz build.
2. Permissão de microfone: aparece o pedido ao abrir? Se negar, o texto mostra "Sem permissão de microfone". Se não pedir, verifique se `android.permission.RECORD_AUDIO` está no manifesto final.
3. Voz: o texto "Pode falar..." aparece (conexão OK, rede liberada no Quest)? Ele responde em voz, com a transcrição abaixo do anel?
4. Interrupção: fale por cima dele — o áudio deve cortar na hora. Atenção a ECO: se o alto-falante do Quest for captado pelo microfone, ele pode se auto-interromper; nesse caso use fone ou me peça um modo "mutar mic enquanto ele fala".
5. Núcleo: pulsa mais rápido/forte quando ele fala e fica calmo em repouso?
6. Painéis: aparecem em arco ~1,5 s após iniciar (hora, data, conexão, assistente). Estão legíveis/bem posicionados? Ficam fixos no mundo onde você olhava ao iniciar.
7. "Crie um cubo azul" (e esfera/cilindro, outras cores, "do tamanho de uma bola de tênis", "à minha esquerda"): o objeto surge com a montagem em ~1 s?
8. Pegar e girar o objeto com as mãos (pinça/ray). Se não pegar, verifique camadas de interação e se o Hand Tracking/OpenXR está ativo (o Console no Editor avisa que o subsistema de mãos não está rodando, o que é normal fora do óculos).
9. Se o modelo `gemini-3.8-live` der erro de cota/acesso, troque o campo `modelo` no AssistenteVoz (outros IDs: gemini-3.8-live-extended-thinking, gemini-3.1-flash-live-preview).

## Correção — permissão de microfone no Quest
- Causa provável: o projeto não tinha AndroidManifest próprio (só templates gradle) e o `PermissionsManager` do template pedia USE_SCENE no Awake ao mesmo tempo que o microfone; pedidos simultâneos são recusados na hora pelo Android.
- Criado Assets/Plugins/Android/Microfone.androidlib (manifest com `android.permission.RECORD_AUDIO` + `uses-feature microphone required=false`), mesclado ao manifesto no build.
- AssistenteVoz: pede `Permission.Microphone` em tempo de execução e ESPERA a resposta (callbacks + checagem direta; se a recusa for imediata, espera 2 s e repete até 4 vezes) antes de `Microphone.Start`. Se negado, mostra "microfone negado" e inicia sozinho se o usuário liberar depois nas configurações.
- PermissionsManager (cena): `Process Permissions On Awake` desligado; o AssistenteVoz chama `ProcessPermissions()` logo depois da resposta do microfone, em sequência (sem competir).
- Estados exibidos (campo `estadoMic`, no painel ASSISTENTE e no texto): "aguardando permissão", "microfone negado", "iniciando microfone", "ouvindo".
- Testado no Editor: compila sem erros; estado foi de "iniciando microfone" para "ouvindo", painel mostra "Mic: ouvindo". Não testado no Quest.
- Verificar no óculos: o diálogo de microfone aparece; depois o de cena (USE_SCENE). Se já negou antes, desinstale o app ou libere em Configurações > Apps > Permissões.

---
# FASE 2 (assistente "Zetta")

## F2-1 — Realidade mista (concluída; falta testar no óculos)
- Já estavam corretos (conferido): Main Camera SolidColor preto alfa 0; ARCameraManager + ARCameraBackground ativos; OpenXR Android com Meta Quest: Camera (Passthrough), Session, Boundary Visibility (e Planes, Anchors, Raycasts, Occlusion, Bounding Boxes) ligados.
- Feito agora: Skybox removido (RenderSettings.skybox = null, ambiente Flat); desativado o MeshRenderer opaco "Environment" (malha 11×3×11 m do template, que cobriria a visão); manifest (Plugins/Android/Microfone.androidlib) agora declara `com.oculus.feature.PASSTHROUGH` e `com.oculus.feature.BOUNDARYLESS_APP`.
- Console sem erros; cena salva. Sem build (conforme regra).
- Testar no óculos: o app abre já mostrando a sala (sem fundo preto) e continua rodando ao ativar a visão do sistema.

## F2-2 — Nome e personalidade (concluída; falta testar por voz no óculos)
- AssistenteVoz: instrução fixa (const) — Zetta (dois T's), educada, espirituosa, respostas curtas, pt-BR; só responde quando a fala COMEÇA com "Zetta" (aceita Zeta/Zéta), senão silêncio total. Campo `instrucaoExtra` (vazio) para acrescentar regras. Voz não alterada.
- Reforço no cliente: a transcrição da fala do usuário é checada por regex; sem "Zetta" no começo, o áudio/texto/ferramentas do modelo naquele turno são descartados. Flag público `acordada` (liga ao dizer Zetta ou enquanto ela fala; volta à espera após 6 s sem atividade).
- Testado no Play Mode (por texto): "Qual é a capital da França?" → silêncio total; "Zetta, diga olá e se apresente" → "Olá! Eu sou a Zetta, sua assistente holográfica, pronta para tornar sua experiência em realidade mista ainda mais mágica." Console sem erros.
- Pendente: testar por voz real (a checagem da palavra depende da transcrição de entrada do Gemini; se transcrever "Zetta" de outro jeito, ajustar a regex `RegexAtivacao`).

## F2-3 — Posição do anel (concluída)
- AnelHolografico: escala 40% (`escala`), posição no canto inferior direito (`deslocamento` 0,36 / −0,24 m a 1,2 m), seguindo o olhar com suavização; núcleo e auras são filhos, então também ficam a 40%.
- NucleoEnergia: em espera, núcleo azul fraco (corEspera) e energia limitada; acordada, núcleo ciano brilhante (corNucleo) e auras mais fortes.
- TextoResposta: ao lado esquerdo do anel, alinhado à direita, caixa 0,42×0,26 m, fonte automática até 0,5.
- Bug achado e corrigido: `AddComponent<TextMeshPro>()` troca Transform por RectTransform e a referência guardada antes virava nula (o texto não seguia o olhar; o mesmo bug existe no TesteGemini, que está desativado e não foi tocado).
- Testado no Play Mode: anel em (0,36; −0,24; 1,20) no espaço da câmera, escala 0,4, núcleo azul fraco; texto em (0,01; −0,24; 1,20). Console sem erros.

## F2-4 — Informação ao vivo (PARCIAL: busca do Google indisponível no plano grátis; painel pronto)
- Implementado: `{"googleSearch":{}}` no setup do Live (campo `usarBuscaGoogle`), ferramenta `mostrar_painel` (PainelInfo.cs: painel holográfico temporário, surge/encolhe, 20 s, 1 por vez, à esquerda do centro da vista) e utilitários compartilhados em HoloUtil.cs. AssistenteVoz ganhou `RegistrarFerramenta(declaração, tratador, instrução)`.
- Teste real: com a busca do Google ativada, o servidor fechou a conexão com "You exceeded your current quota" (2 tentativas); sem ela conecta normalmente. A doc de preços lista Search como "Supported" no Free tier para gemini-3.8-live, mas na prática a chave grátis foi recusada → conforme a regra (plano pago = pular), deixei `usarBuscaGoogle = false` por padrão. Quando houver cota, basta ligar o campo no AssistenteVoz.
- Sem a busca, a instrução manda a Zetta avisar educadamente que não tem dados ao vivo (sem inventar clima/notícias).
- Testado: "Zetta, mostre um painel com três curiosidades sobre Marte" → ferramenta chamada, painel criado com título e 3 linhas. Console sem erros.
- Pendente: clima/notícias reais dependem da busca (ou de uma API externa grátis, ex. Open-Meteo, se quiser que eu integre depois).

## F2-5 — Timers e lembretes (concluída)
- TimersHolo.cs (GameObject `TimersHolo`): ferramentas `criar_timer(nome, segundos)` e `cancelar_timer(nome|todos)`. Cada timer é um painel holográfico com nome e contagem regressiva MM:SS, empilhados no canto superior esquerdo da vista, seguindo o olhar suavemente; vários ao mesmo tempo.
- Ao zerar: pisca ciano/laranja por 6 s e o app manda um aviso ao Live (`AssistenteVoz.Falar`), que passa pela regra do "Zetta"; a Zetta avisa por voz.
- Testado no Play Mode (por texto): "crie dois timers: 12 s para o café e 25 s para a pizza" → 2 painéis simultâneos contando; ao fim do café a Zetta falou "O seu café está pronto, hora de uma pausa merecida!"; o painel sumiu após o pisca. Console sem erros.
- Pendente no óculos: conferir posição/legibilidade dos painéis e a frase falada por voz real ("Zetta, 10 minutos para o café").

## F2-6 — Modelos prontos (concluída)
- ModelosProntos.cs (GameObject `ModelosProntos`): ferramenta `mostrar_modelo(sistema_solar | molecula_agua | globo_terrestre)` a 0,9 m à frente. Visual holográfico (grade + preenchimento) montado peça por peça: cada parte voa de um ponto aleatório até o lugar, com atraso escalonado (total ≈ 2 s).
  - Sistema solar: Sol + 8 planetas em pivôs que orbitam (depois de montado), órbitas em linhas ciano, anel de Saturno.
  - Molécula de água: O + 2 H (ângulo 104,5°) + 2 ligações, girando devagar.
  - Globo: grade + preenchimento com continentes gerados por código (Perlin 3D), eixo inclinado 23,5°, girando.
  - Todos com Rigidbody sem gravidade + XRGrabInteractable (pegar com as mãos). Máx. 3 modelos (o mais antigo sai).
- Refatorei CriadorHolograma.AplicarVisual (reutilizável) e criei HoloRegistro.cs (registro central dos hologramas, para apagar/âncoras/física); HologramaMontagem ganhou `Reiniciar()`.
- Testado no Play Mode: os 3 modelos montaram (todas as partes com grade e preenchimento a 100%), órbita girando, grab presente; modelo inválido devolve erro. Console sem erros.
- Pendente no óculos: aparência (cores/tamanho) e se dá para pegar os modelos (o collider é um só, esfera na raiz).

## F2-7 — Desenhar no ar (concluída no editor; precisa das mãos reais para validar)
- DesenhoNoAr.cs (GameObject `DesenhoNoAr`): ferramenta `controlar_desenho(ligar | apagar | sair)`. No modo ligado, XR Hands (XRHandSubsystem) lê a ponta do indicador e do polegar; pinça (< 2,2 cm, solta > 3,4 cm) inicia/termina um traço de luz ciano (LineRenderer núcleo + halo mais largo) na ponta do indicador; funciona nas duas mãos; um pequeno cursor ciano mostra a ponta do dedo. Máx. 200 traços.
- Testado no Play Mode (sem mãos): ligar/apagar/sair respondem corretamente e traços simulados criam/limpam LineRenderers (40 pontos). Console sem erros.
- Pendente no óculos: conferir se o ponto do traço coincide com a ponta do dedo (poses convertidas com XROrigin.Origin) e se a pinça do desenho não dispara outros interatores do template (raios/poke) ao mesmo tempo.

## F2-8 — Interação com a sala (concluída no editor; planos reais só no óculos)
- SalaAR.cs (GameObject `SalaAR`): liga o ARPlaneManager do XR Origin (Meta OpenXR) com detecção Horizontal+Vertical (os MeshColliders vêm do prefab ARPlane do template); lê as classificações (Table / Floor / Wall) e esconde a malha dos planos (`mostrarPlanos` para exibir). `TentarMesa()` devolve a mesa mais próxima (usada na tarefa 9) e `AlturaChao()` a altura do chão.
- Física: hologramas flutuam sem gravidade; ferramenta `fisica_objetos(soltar | flutuar)`: ao soltar ligam gravidade, colisão contínua e PhysicsMaterial com bounciness 0,55 (também nos planos detectados). Chão virtual de reserva (y = chão, desativa quando há plano de chão real). Objetos que caírem >3 m abaixo do chão voltam para perto do usuário.
- Testado no Play Mode: esfera criada a 2,2 m, `Soltar(true)` → caiu, repousou em y=0,05 (raio) no chão virtual e, ao ser levantada, quicou (velocidade vertical invertida; material HoloQuica, bounciness 0,55). Console sem erros.
- Pendente no óculos: confirmar que a Scene Permission (USE_SCENE) foi concedida e que mesa/chão/paredes aparecem classificados (se `TentarMesa` falhar, a tarefa 9 usa 1 m à frente). Para ver os planos, ligue `mostrarPlanos` em SalaAR.

## F2-9 — Mesa holográfica (concluída no editor; mesa real só no óculos)
- MesaHolografica.cs (GameObject `MesaHolografica`): ferramenta `controlar_mesa(ativar | desativar)`. Ativar usa `SalaAR.TentarMesa()` (mesa detectada mais próxima) ou, sem mesa, um ponto a 1 m à frente em altura de mesa. Plataforma: disco ciano brilhante (grade + preenchimento) com 3 anéis girando em velocidades/sentidos diferentes (contínuo, tracejado, 3 arcos), surgindo com animação; desativar encolhe e remove.
- Objetos criados com a mesa ativa (via evento `HoloRegistro.aoAdicionar`: cubo/esfera/cilindro, modelos prontos e, futuramente, GLB) nascem no centro e sobem 0,22 m em 1,2 s.
- Testado no Play Mode: ativar → "a 1 metro à sua frente (mesa não detectada)"; cubo criado longe nasceu no centro da mesa e parou 0,22 m acima; plataforma com 4 filhos (disco + 3 anéis); desativar OK. Console sem erros.
- Pendente no óculos: com mesa real, confirmar que a plataforma assenta na altura certa.

## F2-10 — Hologramas que ficam no lugar (código pronto; só dá para validar no óculos)
- AncorasHolo.cs (GameObject `AncorasHolo`): usa ARAnchorManager (Meta OpenXR, persistência XR_META_spatial_entity_persistence). Para cada holograma do `HoloRegistro`: depois de ficar parado 2 s (e não estar sendo segurado) cria uma âncora na pose (`TryAddAnchorAsync`), salva (`TrySaveAnchorAsync`), grava o GUID + descrição (tipo/forma/cor/tamanho/modelo) em `persistentDataPath/HologramasAncorados.json` e apaga a âncora antiga. Se o objeto for movido >3 cm/5°, ele é re-salvo na nova pose (a âncora fica só no dado persistido; o objeto continua livre para a física/mão).
- Ao abrir o app (depois que o subsistema de âncoras roda): lê o JSON, `TryLoadAnchorAsync` de cada GUID e recria o objeto na pose da âncora pela fábrica do tipo (`primitiva` e `modelo` já registradas; `glb` será registrada na tarefa 13), sem a subida da mesa. Apagar objeto remove a âncora e a entrada do JSON (`ApagarTodasAncoras()` para "apagar tudo").
- Refatorações: CriarObjeto/Mostrar aceitam pose do mundo; HoloItem guarda tipo/tamanho/extra.
- Testado no editor: compila sem erros; fábricas recriam cilindro e molécula numa pose do mundo (posição/rotação corretas); sem subsistema de âncoras o componente fica inativo sem erros. NÃO testado: salvar/carregar âncoras reais.
- Testar no óculos: criar um cubo, esperar ~3 s, fechar e reabrir o app: ele deve reaparecer no mesmo lugar da sala. Se não, olhar o Console/logcat por "AncorasHolo:" (cota/permissão de âncoras, USE_ANCHOR_API já está no manifest).

## F2-11 — Holograma no corpo (código pronto; mãos só no óculos)
- HoloAlvo.cs: `ObjetoOlhado()` escolhe o holograma mais próximo do centro do olhar (tolerância 20°, ajustada pelo tamanho); se nada for achado, tenta o raio do indicador (hand tracking).
- CorpoHolo.cs (GameObject `CorpoHolo`): ferramenta `prender_no_corpo(prender | soltar, mao esquerda|direita)`. Prender: objeto olhado/apontado fica kinematic, XRGrabInteractable desligado, encolhe para ≤ 12 cm e segue a palma da mão (XR Hands, deslocamento 0,11 m para fora das costas da mão, suavizado) a cada frame. Soltar: volta voando (0,6 s) à pose/escala originais e restaura física e grab. Objetos presos não são re-ancorados.
- Testado no Play Mode: com cubo à frente e esfera a 60°, `ObjetoOlhado` escolheu o cubo; "prender" sem mãos devolve erro claro; Prender/Soltar direto encolheu 0,28→0,12, ficou kinematic e na devolução restaurou escala, física e grab, removendo o SeguirMao. Console sem erros.
- Pendente no óculos: confirmar o sentido do deslocamento (se o objeto aparecer na palma em vez de nas costas da mão, inverta o sinal de `deslocamento.y` em CorpoHolo) e a naturalidade do seguimento.

## F2-12 — Excluir objetos (concluída no editor; gestos só no óculos)
- ApagadorHolo.cs (GameObject `ApagadorHolo`): ferramenta `apagar_objeto(alvo = descricao | isso | tudo, descricao, confirmado)`.
  - Por voz: "apague o cubo azul" casa as palavras (sem acento, plural, ignorando artigos) com forma/cor/descrição do `HoloRegistro` e apaga o mais próximo do olhar; "apague isso" usa `HoloAlvo.ObjetoOlhado()` (olhar, depois dedo apontando); "apague tudo" NÃO apaga na primeira chamada — devolve "NADA FOI APAGADO, pergunte se tem certeza"; só com `confirmado=true` dentro de 60 s do pedido apaga tudo e as âncoras.
  - Por gesto: `DetectorArremesso` (adicionado a cada holograma) mede a velocidade nos últimos 0,2 s antes do `selectExited`; ≥ 2,2 m/s apaga.
  - Lixeira: ícone holográfico (lata vermelha com tampa, alça e listras) a ~0,55 m à direita, 0,5 m à frente e 0,25 m acima do chão, que acompanha o usuário devagar; objeto solto dentro (raio 0,22 m) é apagado; objeto segurado perto dela a faz crescer/pulsar.
  - Animação: `HologramaMontagem.Desfazer` — o preenchimento some e a grade se desfaz de cima para baixo em 0,5 s (linhas esmaecem; objetos sem grade, como GLB, encolhem); a âncora é removida via `HoloRegistro.Remover` (AncorasHolo.AoRemover).
- Testado no Play Mode: apagar "o cubo azul" removeu só ele; "unicórnio" → erro; "tudo" sem confirmar não apagou nada e com confirmado=true apagou os 2; arremesso 1 m/s manteve e 4 m/s apagou; esfera criada dentro da lixeira foi apagada em seguida. Console sem erros.
- Pendente no óculos: altura/posição da lixeira (alcançável?), limiar de arremesso (2,2 m/s) e se "apague isso" acerta o objeto olhado.

## F2-13 — Modelos 3D com TRELLIS.2 (PARCIAL: código completo; geração real bloqueada pela cota do Space sem token)
- glTFast 6.20.0 instalado (com.unity.cloud.gltfast) e seus shaders (glTF/*, Shader Graphs/glTF-*) adicionados a Always Included Shaders (evita GLB rosa no build).
- GradioClient.cs: cliente do protocolo Gradio 6 (queue/join + SSE queue/data, upload, download), resolvendo api_name → fn_index por /config; sem token.
- GeradorModelo3D.cs (GameObject `GeradorModelo3D`): ferramenta `gerar_modelo_3d(origem = olhando | descricao, descricao)`.
  - Fluxo TRELLIS.2 (Space oficial microsoft/TRELLIS.2): start_session → upload → preprocess_image → image_to_3d (resolução **512**, params padrão do Space) → extract_glb (100 mil faces, textura 1024) → download do .glb → salva em persistentDataPath/Modelos3D → glTFast em tempo de execução → escala ~30 cm, BoxCollider, Rigidbody sem gravidade, XRGrabInteractable, registro no HoloRegistro (tipo `glb`, recriado pelas âncoras ao reabrir) e entrada com caixa de grade que se desfaz.
  - (a) "transforme isso em 3D": foto pela Passthrough Camera API do Quest 3 via WebCamTexture (permissões `horizonos.permission.HEADSET_CAMERA` + `android.permission.CAMERA`, declaradas no manifest e pedidas em tempo de execução; recorte quadrado central de 70%, JPEG ≤ 1024 px). Sem câmera no editor usa imagem de teste.
  - (b) "crie um <objeto> realista": os modelos de imagem do Gemini NÃO estão no plano grátis (docs de preços: `gemini-*-image`, "Not available" no Free tier). Usei o Space gratuito **black-forest-labs/FLUX.1-schnell** (texto → imagem, endpoint `/infer`) alimentando o TRELLIS.2; plano B: Space gratuito de texto→3D **hysts/Shap-E** (`/text-to-3d`). Anotação pedida: texto-para-3D = hysts/Shap-E; imagem = FLUX.1-schnell.
  - Enquanto gera: cubo holográfico "gerando… MM:SS" com a etapa/posição na fila e efeito de montagem em repetição; a Zetta avisa por voz ao começar, ao terminar e ao falhar (sugere tentar mais tarde).
- Teste real no Play Mode (pedido "uma maçã vermelha realista" pelo Gemini): FLUX gerou a imagem; TRELLIS.2 respondeu **"You exceeded your ZeroGPU quota (120s requested vs. 0s left)"** e o Shap-E também ("60s requested vs. 0s left"). Sem token do HF a cota anônima desta conexão estava zerada (conforme a regra, não usei token). O caminho de falha funcionou: marcador removido, Zetta avisada, sem erros. A etapa TRELLIS (upload/preprocess/image_to_3d/extract_glb) portanto NÃO foi exercitada de ponta a ponta — só a parte de protocolo até a cota (join/SSE/erro).
- Teste do carregamento de GLB (GLB de teste 1×2×1 feito à mão): glTFast leu, escalou para 0,15×0,30×0,15 (maior eixo 30 cm), centralizou, colocou BoxCollider/XRGrabInteractable e o shader glTF-pbrMetallicRoughness. Console sem erros.
- Para destravar: tentar mais tarde (a cota do ZeroGPU anônimo reinicia) ou, se quiser, criar um token grátis do HF e me pedir para ligá-lo (campo no GeradorModelo3D) — não implementei por instrução.
- Testar no óculos: permissão de câmera, foto do que está olhando (qualidade do recorte), geração de 1 a 3 min, GLB sem cor rosa, pegar o modelo.

---
# RESUMO FINAL — FASE 2 (Zetta)
Tarefas 1–12 implementadas e testadas no editor; 4 e 13 parciais (limites do plano grátis): 
- F2-4: busca do Google no Live recusada por cota no plano grátis → desligada (`usarBuscaGoogle`), painel `mostrar_painel` pronto.
- F2-13: TRELLIS.2 bloqueado pela cota anônima do ZeroGPU durante o teste; todo o resto do pipeline pronto.
Nenhum build Android foi feito nesta fase. Cena salva; Console sem erros de compilação. Chave de API nunca escrita em logs/arquivos .md.

## O que testar no óculos, na ordem
1. Abrir o app: passthrough já visível desde o início (sem fundo preto), sem pausar ao ativar a visão do sistema (F2-1). Verifique também os diálogos de permissão: microfone → cena (USE_SCENE) → (câmera só na hora de "transforme isso em 3D").
2. Dizer "Zetta, olá": ela responde curto, educada e espirituosa; fale algo sem "Zetta" e ela deve ficar em silêncio; núcleo azul fraco em espera, ciano brilhante ao falar (F2-2/3). Se a palavra de ativação falhar, ajustar `RegexAtivacao`.
3. Anel e núcleo pequenos (40%) no canto inferior direito seguindo o olhar; texto de resposta à esquerda do anel, legível (F2-3).
4. "Zetta, mostre um painel com três curiosidades sobre Marte": painel temporário à esquerda (F2-4). Clima/notícias ao vivo: indisponível (sem busca).
5. "Zetta, 10 minutos para o café" (teste com 15 s): contador no canto superior esquerdo, pisca e ela avisa por voz; vários timers (F2-5).
6. "Zetta, mostre o sistema solar" / "a molécula de água" / "o globo terrestre": montam peça por peça em ~2 s; dá para pegar (F2-6).
7. "Zetta, crie um cubo azul": aparece com efeito de montagem; pegar e girar com as mãos.
8. "Zetta, modo desenho": pinça no ar traça luz ciano; "apagar desenho"; "sair do modo desenho" (F2-7). Conferir se o traço coincide com a ponta do dedo.
9. "Zetta, solte": objetos caem e quicam no chão/mesa reais; conferir planos classificados (F2-8). Para ver os planos, ligar `mostrarPlanos` em SalaAR.
10. "Zetta, ativar mesa" (olhando para uma mesa) → plataforma na mesa; crie um objeto → nasce no centro subindo; "desativar mesa" (F2-9).
11. Âncoras: crie um objeto, espere ~3 s, feche e reabra o app → deve reaparecer no mesmo lugar (F2-10). Se não, ver logcat por "AncorasHolo:".
12. "Zetta, coloque no meu braço" olhando para um objeto → segue o pulso esquerdo; "solte do braço" devolve (F2-11). Se ficar na palma em vez de nas costas da mão, inverter o sinal de `deslocamento.y` em CorpoHolo.
13. Apagar: "apague o cubo azul", "apague isso" (olhando), "apague tudo" (deve pedir confirmação); jogar um objeto longe rápido; soltar um objeto na lixeira à direita perto do chão (F2-12).
14. "Zetta, transforme isso em 3D" olhando para um objeto e "Zetta, crie uma cadeira realista" (F2-13): verá o cubo "gerando…"; se a cota do Space estiver esgotada ela avisa e sugere tentar mais tarde.

---
## Correção pós-teste no Quest (2026-11-01)

Relato: app abria em fundo azul (VR) sem a sala; ao ativar a visão do sistema (dois toques) o app pausava, o anel sumia, aparecia um retângulo preto e a Zetta parava de ouvir; a Zetta não ouvia.

### A. Passthrough não aparecia — CAUSA ENCONTRADA
- O tutorial do template (`GoalManager`, objeto "Goal Manager") chama `ARFeatureController.TogglePassthrough(false)` no `Start`, o que **desliga o `ARCameraManager`** (e portanto o passthrough do Meta). Ele só religa quando o usuário avança os cartões do "Coaching UI". Daí o app abrir "em VR".
- Já estavam corretos (verificados): câmera com fundo sólido preto alfa 0, `ARCameraManager`, `ARCameraBackground` e `ARSession` ativos, features OpenXR Meta (Camera/Passthrough, Session, Boundary Visibility, Planes, Anchors, Quest Support) ligadas no alvo Android, manifest final do APK com `com.oculus.feature.PASSTHROUGH` e `BOUNDARYLESS_APP`, sem skybox, malha do Environment desligada, renderer URP do Android com `ARBackgroundRendererFeature`.
- Correção: novo `PassthroughMR.cs` (objeto "PassthroughMR" na cena). Desliga o `GoalManager`, o "Coaching UI" e o "Tap Tooltip" antes do `Start` deles (`DefaultExecutionOrder(-1000)`), reaplica fundo alfa 0 / ARCameraManager / ARCameraBackground / ARSession a cada 0,5 s e ao retomar de pausa, reinicia o `ARCameraManager` se o subsistema ficar parado e escreve no log `PassthroughMR: passthrough ligado|parado`. O estado aparece no painel CONEXÃO.

### B. Retângulo preto
- Nenhum objeto da cena é um retângulo preto visível: o único Quad com material de vídeo (painel de vídeo do template) fica inativo; `ChaoVirtual` só tem collider. O retângulo preto flutuante é a **janela 2D do app pausado** que o Quest mostra quando o app perde o modo imersivo (sessão OpenXR em STOPPING/IDLE). Foi o que o logcat mostrou ao pausar: `FOCUSED -> VISIBLE -> STOPPING -> IDLE`, `APP_CMD_PAUSE`, `APP_CMD_TERM_WINDOW`.
- O painel de vídeo do template só abre pelo tutorial, que agora está desligado.

### C. Microfone / Zetta não ouvia
- Permissão OK: `RECORD_AUDIO` concedida (`dumpsys package`). Mas o appop `RECORD_AUDIO` é "foreground": com o app pausado o Android recusa o microfone (`rejectTime` no `appops`), e o Unity fica com o clip parado depois de voltar.
- Correções em `AssistenteVoz.cs`: watchdog que reinicia o microfone se a posição do clip não avança por 2 s (inclui o retorno de pausa), reconexão da sessão sem limite de tentativas (antes desistia depois de 3), reconexão forçada após pausa longa, `estadoMic` honesto (`aguardando permissão` / `microfone negado` / `conectando` / `ouvindo` / `processando` / `microfone parado` / `erro no microfone`), nível do microfone medido sempre, `ultimoOuvido` e `ultimoErro`. Regex da palavra de ativação mais tolerante (Zetta/Zeta/Jetta/Seta...). Log `AssistenteVoz: ouvi [...]` por turno.
- Painel ASSISTENTE: `Mic: estado`, barrinhas do nível e `Ouvi: <última transcrição>`. Painel CONEXÃO: estado do Gemini, último erro e estado do passthrough.

### D. Logcat
- A 1ª captura foi feita com o headset **em repouso** (`mWakefulness=Asleep`, `sys.hmt.mounted=0`): o app só inicializa e é pausado; não prova nada sobre passthrough/mic. Útil só para confirmar o manifest, as extensões OpenXR e a sequência de pausa.
- Para testar de verdade é preciso estar com o headset NA CABEÇA durante a captura.

---
# NOITE AUTÔNOMA (Fases 0–8 do PLANO-ZETTA.md)

## Fase 0 — Preparação e segurança (concluída)
- Backup em `C:/Users/luizg/My project - backup fase 0`: **INCOMPLETO** (a cópia foi encerrada pelo sistema por falta de memória; só tem arquivos soltos, sem Assets/ProjectSettings). O que protege o projeto é o **git** (commit por fase e tags `antes-fase-N`). Se quiser backup completo, apague essa pasta e copie o projeto sem a pasta Library, com o Unity fechado.
- Git local iniciado (`main`), `.gitignore` de Unity + `Assets/Segredos/`. Commit inicial `Fase 0`; tag `antes-fase-1`.
- A chave do Gemini estava na cena (2 componentes) e escrita no código de `TesteGemini.cs`. Agora vive só em `Assets/Segredos/ZettaSegredos.asset` (ScriptableObject `ZettaSegredos`, ignorado pelo git; também guarda `tokenGitHub`, `usuarioGitHub`, `tokenHuggingFace`). `AssistenteVoz` e `TesteGemini` leem de lá (campo `segredos`, já ligado na cena). Conferido: `git grep` não acha a chave.
- Atenção: o APK leva o asset de segredos dentro (é inevitável para o app funcionar); não distribua o APK.

## Fase 1 — Base sólida (código concluído; FALTA VALIDAR NO ÓCULOS)
Parte já feita antes da noite (seção "Correção pós-teste no Quest" acima): `PassthroughMR` (desliga o tutorial do template que desligava o passthrough), watchdog do microfone, reconexão sem limite, estados do mic.
Feito nesta fase:
1. **Comparação com o OpenQuestCapture**: o repositório não documenta a configuração, então comparei o `AndroidManifest.xml` dele com o APK nosso. Nosso manifest final já tem tudo que o deles tem (`CAMERA`, `HEADSET_CAMERA`, `BOUNDARYLESS_APP`, `com.oculus.vr.focusaware`, `supportedDevices`, `headtracking`) e mais `PASSTHROUGH`, `RECORD_AUDIO`, `USE_SCENE`. Nada faltando.
2. **Materiais**: todos os hologramas usam `Sprites/Default` (transparente, sem escrita de profundidade); os objetos opacos do template (controles, painel de vídeo) ficam inativos. Nenhum retângulo preto na cena; o retângulo do relato era a janela do app pausado.
3. **Microfone/eco**: sem AEC acessível pelo `Microphone` do Unity, então o mic **fica mudo enquanto a Zetta fala** (+0,6 s) para ela não se ouvir. Para interromper: **pinça longa (~1 s)** com qualquer mão (`InterrupcaoPinca.cs`), que limpa o áudio e descarta o resto do turno. Limitação: não dá para interromper falando "Zetta, pare" enquanto ela fala. Dá para desligar com `silenciarMicEnquantoFala=false` em `AssistenteVoz` (aí volta o corte por voz, com risco de eco).
4. **Status sempre visível**: rótulo pequeno embaixo do anel (`TextoStatus`): ouvindo / processando / falando / conectando / microfone negado / sem internet / erro: ...; mais o painel ASSISTENTE (mic, barrinhas de nível, "Ouvi: ...") e CONEXÃO (Gemini, erro, passthrough).
5. **Logcat**: captura de arranque feita (OpenXR sobe, permissões e extensões ok, sem crash). Não foi possível confirmar passthrough/mic em uso real porque o headset estava em repouso.
- Se o passthrough AINDA não aparecer no óculos: procure no logcat `PassthroughMR:` e `xrCreatePassthroughFB`; a opção B seria migrar para o Meta Spatial SDK, mas **não migrei** (decisão sua).

### Testes no óculos — Fase 1
1. Abrir o app: a sala deve aparecer sozinha, sem fundo azul e sem cartões de tutorial.
2. Painel CONEXÃO mostra "passthrough ligado".
3. Dizer "Zetta, que horas são?": rótulo vai de "ouvindo" → "processando" → "falando" → "ouvindo".
4. Enquanto ela fala, segurar a pinça ~1 s: ela cala.
5. Dois toques no óculos e voltar: o anel volta e o mic volta a "ouvindo" em ~3 s.

## Fase 2 — Personalidade e utilidades (concluída; falta validar por voz no óculos)
Itens 1, 2, 3 e 5 já estavam prontos desde a fase anterior (F2-2, F2-3, F2-5 acima: palavra "Zetta", personalidade, anel/núcleo a 40% no canto inferior direito seguindo o olhar, timers).
- **Item 4 (informação ao vivo)**: a busca do Google continua indisponível na cota grátis da Live API (`usarBuscaGoogle=false`). Em vez dela, `InfoAoVivo.cs` (objeto "InfoAoVivo" na cena) traz **clima** (Open-Meteo, sem chave) e **notícias** (Google News RSS, sem chave) como ferramentas `consultar_clima(cidade)` e `buscar_noticias(assunto)`. Elas mostram o painel temporário (`PainelInfo`) e devolvem os dados à Zetta, que resume por voz. Testado no editor com dados reais (São Paulo; "tecnologia"). Cotações e placares continuam sem fonte grátis: ela avisa que não tem.
- **Item 6 (painéis em arco)**: menores (26×15 cm), fundo mais transparente (alfa 0,08) e 22 cm acima da linha do olhar, para não atrapalhar a visão da sala.
### Testes no óculos — Fase 2
1. "Zetta, qual o clima em Curitiba?": ela diz "um instante", aparece o painel à esquerda e ela resume por voz.
2. "Zetta, notícias de tecnologia": painel com 4 manchetes e resumo falado.
3. "Zetta, 2 minutos para o café" e depois outro timer com outro nome: dois contadores; ao fim ela avisa por voz.
4. Falar sem dizer "Zetta": ela não responde (núcleo azul fraco).
5. Os 4 painéis em arco devem ficar acima do olhar e discretos.

## Fase 3 — Mãos (concluída no código; FALTA VALIDAR NO ÓCULOS)
**Decisão: não instalei o Meta Interaction SDK.** A documentação da Meta parte do `OVRCameraRig`/Meta XR Core SDK (plugin Oculus); usar isso aqui exigiria trocar o rig de câmera do AR Foundation que sustenta o passthrough, com risco alto e sem como testar de madrugada. O plano pede "um único sistema de mãos" e sem conflito com o XRI, então usei o que o template já traz e é compatível com Unity 6.6 + OpenXR: **XR Interaction Toolkit 3.6 + XR Hands** (mãos com `NearFarInteractor`, `PokeInteractor` e rastreamento de mãos já montados em "MR Interaction Setup"). Se quiser mesmo o ISDK da Meta, é uma migração à parte (decisão sua).
- `InteracaoMaos.cs` (aplicado em `criar_objeto`, `mostrar_modelo` e `gerar_modelo_3d`): pegar de perto e à distância (Near-Far); **duas mãos** escalam e giram (`selectMode=Multiple` + `XRGeneralGrabTransformer`); `LimiteEscala` mantém o tamanho entre **5 cm e 2 m** enquanto segura (vale também após vários gestos); **arremesso** com a velocidade da mão (`throwOnDetach`, amortecimento 1,2 para ir parando no ar). Arremesso acima de ~2,2 m/s continua APAGANDO o objeto (Fase 2/F2-12), então arremesse devagar para só lançar.
- `BotaoHolo.cs`: botão **poke** holográfico (detecta a ponta do indicador pelo XR Hands; afunda até 12 mm e brilha; dispara ao atravessar a superfície; rearma ao afastar). Usado em: botão **X** no canto do painel de informação e botão **PARAR** embaixo do anel (aparece só enquanto a Zetta fala e interrompe a fala).
- Excluir objetos (item 5) já estava pronto: voz `apagar_objeto` (descrição / "isso" / "tudo" com confirmação), arremesso rápido, lixeira holográfica à direita perto do chão, animação de desmontar em ~0,5 s e remoção da âncora.
### Testes no óculos — Fase 3
1. "Zetta, crie um cubo azul": pegar com uma mão, de perto e depois apontando de longe com pinça.
2. Pegar com as duas mãos e afastar/aproximar: o cubo escala; nunca passa de 2 m nem fica menor que 5 cm. Girar as mãos gira o cubo.
3. Arremessar devagar: voa e vai parando. Arremessar forte: some em grade de linhas.
4. "Zetta, notícias de tecnologia": tocar no X do painel com o indicador fecha o painel.
5. Pedir algo longo; enquanto ela fala aparece PARAR embaixo do anel: tocar nele calar a fala.
6. "Zetta, apague o cubo azul" / "apague isso" (olhando) / "apague tudo" (pede confirmação).

## Fase 4 — A sala como palco (concluída; planos/mesa/âncoras reais só no óculos)
Itens já prontos desde a fase anterior: detecção de planos (AR Foundation, `SalaAR`: mesa, chão, paredes), gravidade por voz (`fisica_objetos`: "solte" faz cair e quicar nas superfícies reais, "flutue" volta a flutuar), mesa holográfica (`controlar_mesa`: plataforma com 3 anéis girando; objetos criados com ela ativa nascem do centro subindo) e âncoras persistentes (`AncorasHolo`: salva em `HologramasAncorados.json` + âncoras Meta e recria ao abrir o app).
Feito agora:
- **Encaixe (item 4)**: ao soltar um objeto perto da mesa holográfica (até 1,15× o raio, de 5 cm abaixo a 60 cm acima) e com o objeto quase parado, ele desliza em ~0,35 s e pousa com a base sobre o disco (testado no editor: base exatamente na superfície). Não acomoda se a gravidade estiver ligada (aí a física faz o trabalho). Vale para `criar_objeto`, `mostrar_modelo` e modelos 3D.
- O disco da mesa ganhou um colisor fino para os objetos apoiarem quando a gravidade está ligada.
- Decisão: continuei com o **AR Foundation (planos)** em vez do MRUK; é o que já funciona neste projeto e não exige instalar mais um SDK.
### Testes no óculos — Fase 4
1. Olhar para a sala por ~10 s (o Quest precisa ter feito o escaneamento da sala em Configurações > Espaço). "Zetta, ative a mesa": a plataforma aparece na mesa real (ou a 1 m à frente se não achar mesa).
2. "Zetta, crie uma esfera verde": nasce do centro e sobe.
3. Pegar a esfera e soltar sobre o disco: ela desliza e pousa em cima.
4. "Zetta, solte": objetos caem e quicam na mesa/chão reais. "Zetta, flutue" volta ao normal.
5. Deixar 2 objetos em lugares diferentes, fechar o app e abrir de novo: reaparecem no mesmo lugar (âncoras).
6. "Zetta, desative a mesa": a plataforma some.

## Fase 5 – Criação de hologramas (concluída em código)
- Novas formas em `criar_objeto`: capsula, cone, piramide, anel (`FormasHolo.cs`, malhas procedurais).
- Nova ferramenta por voz `editar_objeto` (`EditorObjetos.cs`): mover, cor, tamanho (5 cm–2 m), duplicar; alvo por descrição ou "isso" (objeto olhado). Alterações são regravadas nas âncoras (`AncorasHolo.MarcarAlterado`).
- Testado no editor: 4 formas criadas; mover/cor/tamanho/duplicar OK.
- Testes no óculos: (1) "Zetta, crie um cone vermelho"; (2) "crie um anel azul" e "uma pirâmide"; (3) "mova o cone para a direita"; (4) "pinte de verde"; (5) "aumente isso" olhando para um objeto; (6) "duplique o cubo"; (7) reabra o app e confira se cor/tamanho mudados persistiram.

## Fase 6 – Olhos da Zetta e 3D realista (concluída em código)
- Câmera: Passthrough Camera API via `WebCamTexture` (Unity 6 + Quest), permissões `horizonos.permission.HEADSET_CAMERA` e `CAMERA` pedidas em tempo de execução (já estão no manifesto). Código em `GeradorModelo3D.CapturarFoto` (agora público e reaproveitado).
- Visão: `Visao.cs`, ferramenta `olhar` ("Zetta, o que é isso?"): tira o quadro, envia à Gemini Live (`AssistenteVoz.EnviarImagem`, `realtimeInput.video` JPEG) e pede a resposta em português.
- TRELLIS.2: `gerar_modelo_3d` (origem olhando/descricao) já existia: holograma "gerando..." com contador, aviso por voz, glTFast, escala ~30 cm, interações e âncora; cota cheia → avisa e sugere tentar depois. Reserva texto→3D: Shap-E. **Limite conhecido:** a cota anônima do ZeroGPU (sem token do Hugging Face) pode recusar a geração; não testado de ponta a ponta.
- Biblioteca: `BibliotecaModelos.cs`, ferramenta `modelos_salvos` (listar/mostrar). Os .glb agora são salvos como `<nome>_<ticks>.glb` em `Modelos3D`. Modelos gerados antes desta versão (nome `modelo_...`) aparecem como "modelo".
- Testado no editor: `olhar` (imagem de teste) e `listar` vazio sem erros; "mostrar" não foi testado (não há .glb para testar).
- Testes no óculos: (1) aceitar a permissão de câmeras na 1ª vez; (2) olhar um objeto e dizer "Zetta, o que é isso?"; (3) "Zetta, leia isso" apontando para um texto; (4) "Zetta, transforme isso em 3D" e aguardar 1–3 min (ou aviso de cota); (5) "Zetta, crie uma poltrona vermelha realista"; (6) "Zetta, mostre meus modelos" e "mostre o primeiro".
- Para o usuário (opcional): se a cota anônima esgotar, criar um token do Hugging Face e colocar em `ZettaSegredos.tokenHuggingFace` (hoje não é usado pelo código).

## Fase 7 – Estúdio de Sites por voz (pronta; publicação depende do token)
- `EstudioSites.cs`, ferramentas: `criar_site`, `atualizar_site` (inclui "transforme em 3D" com A-Frame 1.6.0), `listar_sites`, `abrir_site`, `apagar_site`.
- O Gemini (`gemini-3.6-flash`, REST) gera um HTML único e autocontido em português; o prompt proíbe dados pessoais, formulários e recursos externos (só A-Frame no modo 3D). Cópia local em `Sites/<slug>/index.html` + `sites.json` no armazenamento do app.
- Publicação: API REST do GitHub → repositório público `zetta-sites` (criado se não existir, com Pages ligado na `main`), uma pasta por site: `https://<usuario>.github.io/zetta-sites/<slug>/`. O painel mostra "gerando…/publicando…/publicado" e ela avisa por voz. `abrir_site` usa `Application.OpenURL` (intent ACTION_VIEW → navegador do Quest).
- Testado no editor com a chave real do Gemini: criou um site de 22 KB, listou, recusou abrir não publicado, apagou. **A publicação no GitHub NÃO foi testada** (precisa do token).
- **O que você precisa fazer (login/conta):** (1) criar conta no GitHub, se não tiver; (2) em Settings → Developer settings → Fine-grained tokens, criar um token; como o repositório `zetta-sites` só existe depois de criado, o jeito mais simples é criar antes um repositório público vazio `zetta-sites` e dar ao token acesso só a ele, com permissões "Contents: Read and write" e "Pages: Read and write" (e "Administration: Read and write" se quiser que o app crie o repositório sozinho); (3) abrir o asset `Assets/Segredos/ZettaSegredos` e preencher `usuarioGitHub` e `tokenGitHub` (nunca em arquivos versionados); (4) refazer o build.
- Testes no óculos: (1) "Zetta, crie um site sobre o sistema solar"; (2) aguardar o aviso e "abra o site"; (3) "adicione uma seção sobre Marte"; (4) "transforme em 3D"; (5) "liste meus sites"; (6) "apague o site do sistema solar".

## Fase 8 – Acabamento e desempenho (concluída no que dá sem o óculos)
- `LimiteHologramas.cs`: no máximo 24 hologramas ao mesmo tempo; ao passar disso o mais antigo (que ninguém segura) é apagado com a animação normal. Testado no editor (limite 3, 5 formas → ficaram 3). Recriação das âncoras salvas não aciona o limite.
- Sombras da luz direcional desligadas (os hologramas são sem iluminação; economiza GPU no Quest).
- Erros de ferramentas: exceções internas agora viram uma mensagem em português para a Zetta ("deu um problema interno…") em vez do texto técnico em inglês; o detalhe vai só para o log. Os demais erros de ferramentas já eram em português.
- Avaliado e NÃO feito: materiais compartilhados (cada holograma tem cor própria e `editar_objeto` muda a cor do material, então compartilhar quebraria isso); Application SpaceWarp (exige vetores de movimento; materiais transparentes/aditivos o tornam arriscado sem medir no óculos); perfil real de quadros/draw calls (precisa do óculos).
- Revisão visual (cor, brilho, linhas, legibilidade em sala clara/escura) e medição de FPS: só no óculos.
- Testes no óculos: (1) criar ~25 objetos e ver se o mais antigo some sem engasgar; (2) conferir FPS (Meta Quest Developer Hub ou OVR Metrics Tool); (3) ler os painéis em sala clara e escura e ajustar brilho/cores se preciso; (4) se houver engasgos, medir antes de ligar SpaceWarp.

## Ajuste – Ativação por nome (pós-Fase 8)
- Filtro (a): `AssistenteVoz.EhChamado` – primeira palavra da fala, sem pontuação/acento/maiúscula, inteira e isolada, em {zetta, zeta, seta, zera, z, ze (cobre zê/zé), zetá→zeta, mais as variantes antigas zeda, zeca, jeta, cheta, geta}. "Z, tudo bem?" ativa; "zero", "zebra", "oi zetta" não. Testado no editor com 14 frases.
- Instrução (b): a Zetta agora sabe que o nome pode vir como "Zeta/Zé/Zê/Z/Seta" e só responde ao ser chamada. Os dois valem juntos: se o filtro OU a própria Gemini detectar o chamado, ela responde (antes o filtro descartava a resposta da Gemini; isso foi removido; se a Gemini fala, `acordada` é ligado). Ferramentas também deixam de ser bloqueadas pelo filtro.
- (c) Pinça sobre o anel (mão a menos de 22 cm do anel, no início da pinça) chama `Acordar()` e mostra "pode falar" por 2 s. Código em `InterrupcaoPinca` (funciona quando ela não está falando; falando, a pinça segurada continua interrompendo).
- (d) Mantido/ajustado: frase sem chamado e sem resposta mostra "diga 'Zetta' antes" por 2 s no rótulo do anel; modo conversa sem nome por 8 s depois de responder (era 6 s).
- Testes no óculos: (1) dizer só "Z, que horas são?", "Zé, olá", "Zeta, olá" (todos devem responder); (2) conversar sem chamar: deve aparecer "diga 'Zetta' antes" por 2 s e ela fica quieta; (3) responder e, em até 8 s, continuar sem o nome; (4) pinçar com o dedo sobre o anel e falar sem o nome; (5) ver no `adb logcat -s Unity` as linhas `ouvi [...]` para ajustar a lista de variações.

## RODADA 2 (noite autônoma) – registro por tarefa
- **Tarefas 0–5:** já estavam concluídas e commitadas antes desta rodada (0: Estúdio de Sites publicado e testado com token real, `fe93c76`; 1: véu de pontos = prefab `ARPlane` do template, planos agora com `forceRenderingOff`; 2: colisor por bounds + pegada dinâmica + brilho; 3: ativação por nome; 4: menu "Player Settings" desligado; 5: lixeira na cintura – `e156519`). Não refeitas.
- **Tarefa 6 – frases cortadas (código pronto; falta ouvir no Quest).** AEC não foi tentado: o `Microphone` do Unity não expõe `AcousticEchoCanceler`, e `AudioRecord` com `VOICE_COMMUNICATION` via JNI não dá para validar sem o óculos. Usei o plano B reforçado: (1) `realtimeInputConfig.activityHandling = NO_INTERRUPTION` (+ `startOfSpeechSensitivity LOW`) no setup: o servidor não corta mais a resposta por barulho; se ele recusar a configuração duas vezes seguidas, o app a desliga sozinho (`semInterrupcaoServidor`). (2) O microfone agora fica fechado durante TODO o turno de resposta (do primeiro pedaço de áudio até `turnComplete`, não só enquanto há áudio no buffer) + 0,9 s de folga. Só a pinça segurada ~1 s ou o botão PARAR interrompem; "Zetta, pare" por voz não funciona (o mic está mudo enquanto ela fala). Testado no editor: sessão conecta com a nova configuração e uma resposta de 4 frases chegou inteira. Teste no óculos: peça algo longo ("conte uma história de 6 frases") e fale perto dela: ela deve terminar sem cortar.
- **Tarefa 7 – visão (pronta; câmera real só no Quest).** Novo `CameraOlhos.cs`: Passthrough Camera API via `WebCamTexture`; pede em tempo de execução `horizonos.permission.HEADSET_CAMERA` + `CAMERA` (detecta "negada" na hora pelos callbacks, em vez de esperar 60 s), espera a lista de câmeras aparecer depois da permissão, captura 1280x960 e devolve JPEG reduzido mantendo a proporção. `olhar` ("o que é isso?", "o que você vê?") usa quadro inteiro com maior lado de 640 px e o envia por `realtimeInput.video` (a doc da Live API confirma o campo `video`); o rótulo do anel mostra "câmera negada" ou "câmera: <erro>" por 8 s e a Zetta explica. O `GeradorModelo3D` agora reaproveita o mesmo componente. Referência OpenQuestCapture: não consultada online; o manifesto já tem as permissões (verificado antes no build). Testado no editor (imagem de teste, porque o simulador XR lista câmeras falsas): a Gemini respondeu "uma esfera vermelha perfeita". Limite: não sei se `WebCamTexture.devices[0]` é a câmera esquerda ou direita do Quest, nem se o quadro vem espelhado; os dois só se veem no óculos. Teste: "Zetta, o que é isso?" apontando para um objeto; negar a permissão uma vez para ver "câmera negada".

### Tarefa 8 – Apagar objetos (feito, testado no editor)
- `HoloRegistro`: cada item ganhou `id` e há `Resumo()` (id, tipo, nome, cor, tamanho_cm). Nova ferramenta `listar_objetos` devolve isso (+ traços de desenho) à Gemini; `apagar_objeto` aceita `id` e, quando não acha pela descrição, responde com a lista.
- `Buscar` com sinônimos (bola→esfera, caixa→cubo, lata→cilindro, argola→anel, lembrete→nota, cores no feminino).
- "Apague o desenho/traço/risco" limpa os desenhos; "apague tudo" (com confirmação) também limpa desenhos, registro e âncoras.
- "Apague isso": mão (dedo a ≤15 cm do objeto) tem prioridade, depois olhar/apontar.
- `AncorasHolo` ignora itens cujo tipo não tem fábrica (não dá para recriar).
- Teste no editor: bola vermelha→esfera ok; cubo amarelo inexistente→erro+lista; por id ok; tudo pede confirmação e apaga. Mão/olhar reais: sem teste no Quest.

### Tarefa 9 – Notas holográficas (feito, testado no editor; build de conferência das tarefas 8+9 OK, 0 erros)
- `NotasHolo.cs` (no objeto do `ApagadorHolo`): ferramentas `anotar(texto)` e `gerenciar_notas(acao=ler|mostrar|apagar, assunto)`.
- Cartão ciano claro 20x14 cm com texto escuro, pegável (XRGrabInteractable + `InteracaoMaos`), registrado como tipo "nota" (texto em `extra`) → âncora espacial, `Fabricas["nota"]` recria na reabertura, apagar por voz/olhar/lixeira.
- Ao soltar (0,45 s depois), raios nas 6 direções (25 cm) procuram um `ARPlane`; se achar, o cartão desliza até a superfície (normal da parede, texto legível) e fica kinematic para a física não empurrá-lo.
- "Ler" mostra também um painel "Minhas notas" e devolve os textos à Gemini; "mostrar" faz as notas pulsarem 5 s (`BrilhoProximidade.Destacar`).
- Teste no editor: criar, ler, mostrar, apagar por assunto (inclusive "anotação da reunião" via apagar_objeto), grudar numa parede de teste (ARPlane falso). Sem teste no Quest: legibilidade do texto, planos reais, âncoras.

## RODADA 3 (pedido a9f4 nº 2) – registro por tarefa
### R3-1 Cor ao pegar (feito; build 0 erros; sem teste no Quest)
- Causa: `BrilhoProximidade` misturava a cor com BRANCO (`Color.Lerp(base, white, brilho*0,55)`); segurado (brilho=1) ficava 55% branco. O XRI/template não interfere (nenhum affordance nos objetos criados).
- Correção: o brilho agora só sobe o valor (HSV) mantendo matiz/saturação: até +20% segurado (menos ao aproximar a mão) + leve aumento de opacidade; ao soltar volta à cor base exata. Vale para formas, modelos prontos, notas (todos usam o componente). GLB com shader próprio (sem `_Color`) não é tocado. Contorno ciano não foi adicionado (aumento de brilho basta pelo pedido "ou").

### R3-2 Visão com imagem preta (código pronto; build 0 erros; só o Quest confirma)
- `CameraOlhos` reescrito: (a) salva cada foto em `persistentDataPath/ultima_foto.jpg` (`adb pull /sdcard/Android/data/com.luiz.jarvismr/files/ultima_foto.jpg`), loga brilho médio e mostra por 5 s uma miniatura holográfica ao lado do anel (`mostrarMiniatura` no componente `CameraOlhos` desliga). (b) aquecimento: espera até 12 quadros/2 s, depois até 3 tentativas com limiar de brilho 0,03 (`limiarBrilho`). (c) cópia via `Graphics.Blit` → RenderTexture → `ReadPixels` (não usa mais `GetPixels32`). (d) loga todas as câmeras (`CameraOlhos: N câmeras: [...]`) e, se uma der preto/sem imagem, tenta a próxima; se todas falharem: erro "imagem da câmera preta". Não consegui consultar o OpenQuestCapture/exemplos Meta online nesta rodada: a escolha de câmera é por tentativa, não por nome. (e) Manifesto OK (`HEADSET_CAMERA` + `CAMERA`, minSdk 34 = Android 14). Versão mínima do Horizon OS (v74+ na documentação da Meta) não é verificável pelo Unity: não há aviso por voz específico; sem câmera aparece "nenhuma câmera encontrada"/"imagem da câmera preta" e a Zetta explica. (f) `EnviarImagem` confere: `realtimeInput.video {mimeType:"image/jpeg", data: base64}`.
- Editor: câmeras do simulador não entregam quadro → cai na imagem de teste (log mostrou as 4 câmeras falsas). Teste no óculos: pedir "o que você vê?", ver a miniatura, depois `adb logcat -s Unity | findstr CameraOlhos` (brilho por tentativa) e puxar `ultima_foto.jpg`.

### R3-3 Notas (NotasHolo) (feito, testado no editor; build 0 erros)
- Letra com o tamanho aparente do `PainelInfo`: `letraBase` 0,115 a 0,7 m (auto-size desligado). Nota criada a 0,7 m. Cartão com 30 cm de largura que CRESCE em altura (até 45 cm) para caber o texto; se ainda não couber, a letra encolhe no máximo 20% e o excedente vira reticências.
- Nova ferramenta `editar_nota` (acao: letra_maior/menor ×1,25 entre 0,5 e 2,5; trocar_texto; acrescentar; cor; cartao_maior/menor). Alvo: assunto, ou a nota olhada/tocada, ou a única nota.
- Escala com duas mãos: o texto é filho da raiz escalada, então acompanha (TMP SDF, nítido); a escala é salva ao soltar.
- Persistência: texto, letra, cor, tamanho do cartão e escala vão em JSON no `extra` do item (âncora regrava via `MarcarAlterado`, que agora também atualiza `extra`); a fábrica "nota" lê o JSON (notas antigas só com texto continuam funcionando).
- Editor: criar curta/longa, editar (letra, acrescentar, cor, cartão, trocar texto), salvar escala 1,6 e recriar pela fábrica. Legibilidade real (distância, luz): só no Quest.

### R3-4 Regra geral de limites (feito; build 0 erros)
- `AssistenteVoz.BaseInstrucao` ganhou "REGRA DE LIMITES": se não há ferramenta para o pedido, em vez de só "não posso" ela diz em uma frase o que consegue fazer de parecido e oferece. Comportamento real só se ouve no Quest (depende da Gemini).

### R3-5 Modelos 3D de ponta a ponta (token HF presente no asset; nunca impresso)
- Teste real no editor: "caneca realista". Tempos: imagem FLUX 5,4 s e 9,1 s (2 execuções); TRELLIS.2 (envio + geração + exportação + download do GLB, 3–4 MB) 32,9 s e 80,7 s; leitura/instanciação glTFast 0,2 s; total até salvar 38 s e 90 s. Os endpoints (`infer`, `start_session`, `preprocess_image`, `image_to_3d`, `extract_glb`) continuam válidos; o token foi aceito.
- Falha achada: o GLB do TRELLIS.2 tem texturas WebP (`EXT_texture_webp`, exigida) e o glTFast não decodifica WebP sem plugin nativo ("não conseguiu ler o arquivo GLB").
- Correção: `GeradorModelo3D.AdaptarGlbSemWebP` reescreve o GLB (remove texturas e a extensão exigida, mantém geometria) e usa como cor base a cor dominante da imagem de entrada (`CorDominante`; cinza claro se não achar). Resultado: o modelo abre, com cor única, sem textura. Logs `[tempo]` por etapa ficam no Console.
- Para ter textura de verdade: compilar libwebp como plugin nativo `webp-unity` (Android arm64 + Windows) e registrar o `WebpTextureAddon` do glTFast (exemplo em `Library/PackageCache/com.unity.cloud.gltfast*/Runtime/DocExamples`), ou um pacote Unity de WebP. Não feito: precisa baixar binários nativos e validar no Quest.

## RODADA 4 – R4-1 Esfera da Zetta (feito; build 0 erros)
- Ver o resumo no topo deste arquivo. Arquivos: `Assets/EsferaZetta.cs`, `Assets/Resources/EsferaAditiva.shader`, `Assets/AnelHolografico.cs` (cria a esfera e desliga o `NucleoEnergia`).
- Tarefas 2–6 do pedido da Rodada 4: já concluídas na Rodada 3 (R3-1 a R3-5), puladas conforme a regra.
