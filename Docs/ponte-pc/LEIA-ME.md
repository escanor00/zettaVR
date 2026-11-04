# Ponte PC da Zetta

Deixa o headset mandar **pedidos de mudança** para o PC. Os pedidos entram numa fila em `Docs/pedidos-zetta.md`.
**Nada é executado automaticamente**: o Claude Code (ou você) lê a fila e decide o que fazer.

## Como ligar (uma vez)

1. No Unity: menu **Zetta > Ponte PC > Gerar senha**. Isso cria uma senha aleatória e grava:
   - no asset `Assets/Segredos/ZettaSegredos.asset` (campos `senhaPonte` e `enderecoPonte`; o asset fica fora do git);
   - em `Docs/ponte-pc/.senha` (também fora do git, está no `.gitignore`).
   A senha **não** é mostrada no Console. Para ver, abra o asset no Inspector. O endereço do PC na rede (`http://192.168.x.y:8765`) é preenchido sozinho.
2. Gere um novo APK e instale no Quest (a senha e o endereço vão dentro do build).

## Como iniciar o servidor (toda vez que quiser usar)

```
python Docs/ponte-pc/servidor.py
```

- Porta padrão 8765 (`--porta` muda). Ctrl+C encerra.
- O PC e o Quest precisam estar na **mesma rede Wi-Fi**. Se o Windows perguntar, permita o Python na rede **privada**.
- Sem a senha gerada o servidor não inicia.

## Como usar no headset

- "Zetta, **peça ao PC** para trocar a cor dos painéis" → o pedido entra na fila.
- "Zetta, **mostre a fila do PC**" → painel com a conexão e os pedidos pendentes.

## Segurança

- Senha obrigatória em todo pedido (cabeçalho `X-Zetta-Senha`); 5 senhas erradas bloqueiam o IP por 5 minutos; máximo de 30 pedidos por minuto por IP.
- Só aceita endereços da rede local (privados) ou do próprio PC.
- É HTTP simples (sem criptografia): use só em rede doméstica de confiança. Não exponha a porta na internet.
- Para trocar a senha, rode **Gerar senha** de novo (substitui a anterior) e gere um novo APK.

## Fila

Formato de `Docs/pedidos-zetta.md`:

```
- [ ] #1 · 2026-11-03 21:50 · texto do pedido
```

Troque `[ ]` por `[x]` quando o pedido for atendido (some da lista de pendentes do headset).
