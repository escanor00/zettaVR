# PLANO-ZETTA — assistente de realidade mista para Meta Quest 3

Este arquivo é o plano mestre do projeto. O Claude Code deve ler este arquivo e o PROGRESSO.md antes de qualquer fase e executar **somente a fase pedida**.

## Visão

A Zetta (sempre com dois T's) é uma assistente de voz em **realidade mista**: a sala real fica sempre visível e os hologramas aparecem por cima, no estilo dos laboratórios de filmes de super-herói (linhas finas de luz ciano, camadas, anéis girando, montagem peça por peça), com visual próprio, sem copiar cenas.
Tudo roda no Quest + nuvem (nada no PC), no plano gratuito sempre que possível.

## Stack atual

- Unity 6.6, template Mixed Reality, OpenXR + Meta Quest feature group, URP.
- Voz: Gemini Live API por WebSocket direto (AssistenteVoz.cs), chave do Google AI Studio. **Manter a voz atual.**
- Scripts existentes: AnelHolografico, NucleoEnergia, TesteGemini (desativado), AssistenteVoz, PaineisHolograficos, CriadorHolograma (+ o que constar no PROGRESSO.md).
- Firebase foi removido. Não reinstalar.

## Referências (usar como consulta, não copiar o projeto inteiro)

| Referência | Para que serve aqui |
|---|---|
| github.com/samuelm2/OpenQuestCapture | Projeto Unity 6 + Meta OpenXR + MRUK com passthrough e acesso às câmeras funcionando no Quest 3. Comparar configuração de OpenXR, AndroidManifest, permissões e cena. Base para acesso às câmeras e, no futuro, escaneamento. |
| github.com/oculus-samples/Unity-NorthStar | É VR (não usar como base). Inspiração para interações com as mãos (Meta Interaction SDK) e para o guia de desempenho no Quest. |
| huggingface.co/spaces/microsoft/TRELLIS.2 | Gera modelo 3D (.glb) a partir de imagem. Chamar pela API do Gradio ("Usar via API" no Space). Teste manual: palmeira em 512 ficou pronta em menos de 30 s, sem login. |
| aframe.io | Para gerar sites 3D/AR no Estúdio de Sites (Fase 7). |
| github.com/meta-quest/Meta-Spatial-SDK-Samples | Plano B (Android nativo, realidade mista e painéis com navegador nativo). Só considerar se a Fase 1 falhar de vez; nunca migrar sem o Luiz decidir. |

## Regras para TODAS as fases

1. Leia este arquivo e o PROGRESSO.md primeiro. Execute só a fase pedida e pule itens já concluídos.
2. No início da fase, faça um commit git ("antes da fase N") para poder voltar atrás. Se o projeto ainda não tiver git, a Fase 0 cuida disso.
3. Uma tarefa por vez. Depois de cada uma: Console sem erros de compilação, cena salva, anotação no PROGRESSO.md (o que fez, como testou, o que ficou pendente).
4. Se uma tarefa travar depois de 3 tentativas, anote a causa provável e siga para a próxima.
5. Ao terminar a fase: gere um build Android só para garantir que compila (não precisa instalar), commit git ("fase N concluída") e escreva no PROGRESSO.md a **lista de testes no óculos** daquela fase, em passos curtos.
6. Nunca escreva chaves ou tokens em logs, .md, commits ou terminal. Segredos ficam num ScriptableObject `ZettaSegredos` fora do git (.gitignore).
7. Só plano gratuito. Se algo exigir plano pago, pule e anote.
8. Realidade mista sempre: nada pode cobrir a visão da sala (sem skybox, sem fundos opacos). Materiais holográficos são transparentes/aditivos e compatíveis com passthrough.
9. Português do Brasil em tudo que a Zetta fala e mostra.
10. Economia de tokens: leia só os arquivos necessários, não repita código nas respostas, respostas finais curtas.
11. Ao consultar APIs (Gemini, Meta, Gradio), confira a documentação oficial atual em vez de supor nomes de modelos ou endpoints.

---

## Fase 0 — Preparação e segurança

- Copie a pasta inteira do projeto para "My project - backup fase 0".
- Inicie um repositório git local com .gitignore padrão de Unity + `ZettaSegredos`. Commit inicial.
- Mova a chave do Gemini para o ScriptableObject `ZettaSegredos` e faça os scripts lerem dali.
- Crie o PROGRESSO.md se não existir, com uma seção por fase.

**Pronto quando:** git funcionando, chave fora dos scripts e da cena, Console limpo.

## Fase 1 — Base sólida: sala sempre visível e microfone funcionando (PRIORIDADE)

Problema atual: o app abre num fundo azul com pontinhos (VR), sem mostrar a sala; se o Luiz ativa a visão da sala pelo sistema (dois toques), o Quest pausa o app, aparece um retângulo preto e a Zetta não ouve.

1. Compare com o OpenQuestCapture e corrija o passthrough de dentro do app, ligado desde a abertura: câmera com fundo sólido preto alfa 0, ARSession + ARCameraManager ativos, feature Meta Quest: Camera (Passthrough), Session e Boundary Visibility no OpenXR (aba Android), AndroidManifest com com.oculus.feature.PASSTHROUGH e com.oculus.feature.BOUNDARYLESS_APP, Environment/fundo azul do template desativado.
2. Ache o objeto que vira retângulo preto e troque o material por um transparente compatível com passthrough. Revise todos os materiais holográficos com o mesmo critério.
3. Microfone: permissão RECORD_AUDIO em tempo de execução, captura confirmada no Quest, áudio chegando à Gemini Live (ver Logcat).
4. Eco: use fonte de áudio com cancelamento de eco do Android (VOICE_COMMUNICATION / AcousticEchoCanceler) se possível. Se não der, silencie o microfone enquanto a Zetta fala e permita interromper com "Zetta, pare" ou gesto de pinça longa.
5. Painel de status sempre visível e pequeno: "ouvindo", "processando", "falando", "microfone negado", "sem internet" ou o erro.
6. Use o Android Logcat (Quest no cabo) para confirmar ausência de erros de OpenXR, passthrough, microfone e WebSocket.

**Pronto quando:** o app abre mostrando a sala sem nenhum toque no óculos, os hologramas aparecem por cima, a Zetta ouve e responde.
**Se falhar de vez:** pare, explique as causas no PROGRESSO.md e descreva a opção de migrar para o Meta Spatial SDK, sem migrar.

## Fase 2 — Personalidade e utilidades da Zetta

1. Palavra de ativação: só responde quando a frase começa com "Zetta". Em espera, núcleo azul fraco; acordada, mais brilhante.
2. Personalidade: educada, espirituosa, respostas curtas, português do Brasil (instrução de sistema do Gemini Live).
3. Anel e núcleo pequenos (40%) no canto inferior direito da visão, seguindo o olhar suavemente, nunca vindo para a frente. Texto da resposta ao lado do anel, tamanho confortável.
4. Informação ao vivo: busca do Google no Gemini Live (se for grátis). Clima, notícias e assuntos atuais também num painel temporário.
5. Timers e lembretes: "Zetta, 10 minutos para o café" cria contador holográfico com nome; ao acabar pisca e ela avisa por voz. Vários ao mesmo tempo.
6. Painéis em arco (relógio, data, conexão): revisar para ficarem discretos e não atrapalharem a visão.

**Não incluir:** menu na palma da mão, teclado holográfico.

## Fase 3 — Mãos de primeira (Meta Interaction SDK)

Faça backup/commit antes. Use um único sistema de mãos para os hologramas (evitar conflito com o XR Interaction Toolkit). Confira na documentação da Meta a forma compatível com Unity 6.6 + OpenXR.

1. Pegar com uma mão e à distância (pinça apontando).
2. Duas mãos para escalar e girar (limite 5 cm a 2 m).
3. Arremesso com física.
4. Botões holográficos tipo "poke" (afundam ao toque do dedo, com brilho).
5. Excluir objetos:
   - voz: ferramenta `apagar_objeto` — "apague o cubo azul" (pela descrição), "apague isso" (o que estou olhando/apontando), "apague tudo" (pede confirmação por voz);
   - gesto: arremessar rápido para longe apaga;
   - lixeira holográfica pequena perto do chão, à direita;
   - animação de saída: desfaz em grade de linhas em ~0,5 s; remove a âncora do objeto.

## Fase 4 — A sala como palco

1. Entendimento da sala (MRUK ou detecção de planos do AR Foundation, o que funcionar melhor; ver OpenQuestCapture): mesa, chão, paredes.
2. Hologramas flutuam sem gravidade; "Zetta, solte" faz cair e quicar nas superfícies reais.
3. Mesa holográfica: "Zetta, ativar mesa" põe na mesa real (ou a 1 m à frente) uma plataforma circular brilhante com anéis; objetos criados com ela ativa nascem do centro, subindo. "Zetta, desativar mesa" remove.
4. Encaixe: objeto solto perto da mesa se acomoda em cima.
5. Âncoras espaciais persistentes: objetos reaparecem no mesmo lugar ao reabrir o app.

## Fase 5 — Criação de hologramas

1. `criar_objeto` (cubo, esfera, cilindro, cone, anel...; cor, tamanho, posição) com o material holográfico e efeito de montagem (grade de linhas que se preenche em ~1 s).
2. `mostrar_modelo`: sistema solar (planetas orbitando), molécula de água, globo terrestre girando — montados peça por peça, cada parte voando até o lugar (~2 s).
3. Desenhar no ar: "modo desenho" (traço de luz no indicador durante a pinça), "apagar desenho", "sair do modo desenho".
4. Holograma no corpo: "coloque no meu braço" prende o objeto olhado ao pulso/mão; "solte do braço" devolve.
5. Edição por voz: mover, mudar cor, aumentar/diminuir, duplicar.

## Fase 6 — Olhos da Zetta e 3D realista

1. Acesso às câmeras do Quest 3 (Passthrough Camera API; ver OpenQuestCapture e documentação atual da Meta). Pedir permissão de câmera.
2. Visão: "Zetta, o que é isso?" envia um quadro (JPEG, baixa resolução) para a Gemini Live e ela responde sobre o que estou olhando.
3. TRELLIS.2 (`gerar_modelo_3d`), API do Gradio do Space microsoft/TRELLIS.2, resolução 512, sem token por enquanto:
   - "Zetta, transforme isso em 3D": foto da câmera → TRELLIS → .glb;
   - "Zetta, crie um <objeto> realista": imagem gerada por modelo grátis do Gemini; se não houver, Space gratuito de texto-para-3D (anotar qual).
   - Enquanto gera: holograma "gerando..." com montagem e contador; ela avisa por voz.
   - Carregar .glb em tempo de execução com glTFast, escala ~30 cm, já com as interações da Fase 3 e âncora da Fase 4.
   - Fila/cota cheia: ela avisa e sugere tentar mais tarde.
4. Biblioteca: "Zetta, mostre meus modelos" lista os .glb salvos no óculos para recolocar na sala.

## Fase 7 — Estúdio de Sites por voz

1. "Zetta, crie um site de...": o Gemini gera um único arquivo HTML autocontido (A-Frame quando for site 3D/AR).
2. Publicação grátis no GitHub Pages pela API do GitHub, repositório `zetta-sites`, uma pasta por site. Token com permissão só para esse repositório, guardado em `ZettaSegredos`.
3. Painel holográfico "publicando..." com o nome do site; ao terminar, ela avisa e oferece abrir.
4. Abrir no navegador do Quest ao lado da Zetta (intent Android com a URL).
5. "Zetta, mude/adicione..." atualiza o site existente; "liste meus sites"; "apague o site X"; "transforme em 3D" refaz com A-Frame.
6. Pode usar nos sites os modelos .glb da Fase 6.
7. Sites são públicos: a Zetta nunca coloca dados pessoais neles.

## Fase 8 — Acabamento e desempenho (inspirado no NorthStar)

1. Perfil de desempenho no Quest: taxa de quadros estável, reduzir draw calls, materiais compartilhados, limitar hologramas simultâneos, avaliar Application SpaceWarp.
2. Revisão visual geral: consistência de cor, espessura de linhas, brilho, legibilidade dos textos na sala clara e escura.
3. Revisão de falas da Zetta: mensagens de erro amigáveis em português.

## Redesign visual (Direção B) — concluído no editor, falta validar no Quest

Ver o relatório no topo do PROGRESSO.md e `Docs/REDESIGN-ZETTA.md`. Resumo: `TemaZetta` + shader `VidroHolo` + fonte Rajdhani, central sob demanda (`CentralZetta`: "abra/feche a central", pinça na esfera, recolhe em 15 s), painéis de vidro, esfera preservada. Próximas atividades: testar no óculos, ajustar legibilidade/posições, e a fase própria do avatar (ideias futuras).

## Rodada 5 — módulos novos (feita no editor; falta validar no Quest)

Resumo e lista de testes do headset no topo do PROGRESSO.md. Módulos entram pela lista de `ZettaExtras` (sem editar a cena) e registram as ferramentas no Awake: formas matemáticas, vista explodida, gráficos, escaneamento, bom dia, modo foco, gestos duplos, linha do tempo, etiquetas da sala, portal do clima, apresentação de modelos, desempenho, código, mapa do projeto, ajustes, regras por blocos, alertas de erro e ponte PC (`Docs/ponte-pc/`). `AssistenteVoz` agora é `partial` em 5 arquivos; `EstadoZetta` é o estado único da voz; testes: `Zetta > Rodar testes`.

## Ideias futuras (não executar sem pedido)

- "Zetta, escaneie este objeto/ambiente": captura como no OpenQuestCapture e reconstrução 3D na nuvem (Gaussian splatting).
- Fios de luz com física conectando hologramas (ideia das cordas do NorthStar).
- Navegador dentro do app (painel holográfico com o site), caso migre para o Meta Spatial SDK.
