# AVALIAÇÃO – Zetta MR (Quest 3) – fim da Rodada 2

Base: código, `PROGRESSO.md`, `PLANO-ZETTA.md`. Último APK instalado no Quest: build do commit `e156519` (tarefas até a 5). Tudo depois foi só compilado e testado no editor.

## (a) Estado
| Item | Estado |
|---|---|
| F0 segurança/git | pronto e testado |
| F1 passthrough, voz, mic | pronto, corrigido após teste no Quest; **sem reteste** das correções finais |
| F2 clima, notícias, timers | pronto sem teste no Quest (busca do Google: parcial, indisponível no plano grátis) |
| F3 mãos (pegar, escalar, arremessar, botões) | pronto sem teste no Quest após as correções de pegada |
| F4 planos, mesa, âncoras | pronto sem teste no Quest (planos/âncoras reais só no óculos) |
| F5 formas e `editar_objeto` | pronto sem teste no Quest |
| F6 visão + 3D | visão: pronta sem teste no Quest; 3D: **parcial** (cota anônima do ZeroGPU recusa) |
| F7 Estúdio de Sites | pronto e testado de verdade (GitHub Pages, no editor); falta testar no Quest |
| F8 limite 24, sombras off | pronto; FPS nunca medido |
| T6 frases cortadas | pronto sem teste no Quest |
| T7 visão (`CameraOlhos`) | pronto sem teste no Quest (índice/espelhamento da câmera desconhecidos) |
| T8 apagar objetos | pronto, testado no editor |
| T9 notas holográficas | pronto, testado no editor (parede de teste); texto/planos reais só no Quest |

## (b) Problemas e riscos conhecidos
- **Eco**: a Zetta pode ouvir a si mesma. Mitigação atual: mic mudo durante a resposta; não dá para interromper por voz.
- Chaves (Gemini, GitHub) ficam no asset `ZettaSegredos`, que **entra no APK**. Não compartilhe o APK.
- 3D depende de cota gratuita de terceiros; pode falhar a qualquer hora.
- Várias suposições só o óculos confirma: `WebCamTexture.devices[0]`, espelhamento, planos AR, âncoras, rastreio de mãos.
- Dependência de nomes de modelos Gemini e do protocolo Live (podem mudar).
- Busca de apagar exige que **todas** as palavras combinem (seguro, mas pode pedir ajuda à lista).

## (c) Qualidade do código
- Bom: segredos isolados; `HoloRegistro` como fonte única (âncoras, apagar, editar, notas); fábricas de âncora por tipo; erros amigáveis em português; commits/tags por tarefa.
- Refatorar: `AssistenteVoz.cs` é grande (WebSocket, áudio, estado, ativação por nome). Há dois jeitos de registrar ferramentas (`RegistrarFerramenta` e a cadeia `aoChamarFuncao` do `CriadorHolograma`). Valores serializados na cena sobrescrevem padrões do código (já causou bug). Muitos `FindAnyObjectByType`. Sem testes automáticos (nem para `EhChamado`/`Buscar`, que são lógica pura).

## (d) Desempenho no Quest
- Cada holograma = 2 malhas + materiais próprios `Sprites/Default` translúcidos → overdraw e draw calls (24 objetos ≈ 50+ chamadas). Sem materiais compartilhados e sem SpaceWarp.
- Por quadro: `BrilhoProximidade` (ClosestPoint por dedo × colisor × objeto), `OverlapSphere` da lixeira, `HoloAlvo`; TMP com auto-size nas notas/painéis.
- Codificação JPEG e captura da câmera na thread principal causam um engasgo curto. APK de ~1,1 GB. FPS nunca medido.

## (e) Top 5 melhorias
1. **Validar tudo no Quest e corrigir** – semanas de código sem teste acumulam risco; é o maior ganho.
2. **Eco/interrupção por voz** (AudioRecord nativo `VOICE_COMMUNICATION` ou fone) – hoje é o ponto mais frágil da experiência.
3. **Segredos fora do APK** (proxy simples ou token de curta duração) e girar o token do GitHub colado antes.
4. **Medir e reduzir custo gráfico** (OVR Metrics): materiais compartilhados com cor por propriedade, grade+preenchimento numa malha, depois SpaceWarp se compensar.
5. **Dividir `AssistenteVoz` e unificar o registro de ferramentas**, com testes de editor para `EhChamado`, `Buscar`, `Normalizar`.

## (f) O que você precisa fazer
- Girar o token do GitHub (foi colado no chat/arquivo antes de ir para o asset) e atualizar `githubToken` no asset.
- Opcional: token do Hugging Face em `tokenHuggingFace` (3D); mantenha a chave Gemini no plano grátis.
- No Quest: modo desenvolvedor, aceitar permissões de microfone e câmeras; instalar o APK (`adb install -r Builds/JarvisMR.apk`, Quest `2G0YC1ZF9V01Y2`).

## (g) Testes no óculos, em ordem
1. Abrir: sala visível, sem véu azul/pontos. 2. "Zetta, olá"; "Z, que horas são?"; conversa sem o nome por 8 s.
3. Pedir uma resposta longa e falar por cima: não deve cortar. 4. "Zetta, o que é isso?" (aceitar câmera; negar uma vez).
5. "Crie um cubo azul": pegar, escalar com duas mãos, arremessar, soltar na lixeira, menu da mão.
6. "Apague a bola vermelha", "apague isso" (olhando/tocando), "apague tudo" (confirma).
7. "Anote: comprar leite"; pegar e soltar na parede/mesa; "leia minhas notas"; "mostre minhas notas"; "apague a nota do leite".
8. Fechar e reabrir: objetos e notas voltam no lugar. 9. "Crie um site sobre…", "abra o site", "apague o site".
10. "Transforme isso em 3D" (pode dar aviso de cota). 11. ~25 objetos + medir FPS.
