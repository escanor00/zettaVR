# PROGRESSO

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
