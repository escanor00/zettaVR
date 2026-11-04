#!/usr/bin/env python3
"""Ponte PC da Zetta: servidor simples na rede local.

O headset envia pedidos de mudança (texto) e eles ficam numa fila em Docs/pedidos-zetta.md.
Este servidor NÃO EXECUTA NADA: só guarda os pedidos e informa o estado da fila.

Segurança:
  - exige a senha no cabeçalho X-Zetta-Senha (gerada em Zetta > Ponte PC > Gerar senha; fica em Docs/ponte-pc/.senha,
    que está no .gitignore, e no asset ZettaSegredos, fora do git);
  - só aceita conexões de endereços da rede local (privados) ou do próprio PC;
  - limita pedidos por minuto e bloqueia por 5 minutos quem errar a senha 5 vezes;
  - tamanho máximo do pedido: 500 caracteres.
É HTTP simples (sem criptografia): use só em rede doméstica de confiança.

Uso:  python Docs/ponte-pc/servidor.py [--porta 8765] [--arquivo Docs/pedidos-zetta.md]
"""
import argparse
import hmac
import ipaddress
import json
import os
import re
import sys
import threading
import time
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
CABECALHO = (
    "# Pedidos da Zetta (fila)\n\n"
    "Pedidos enviados pelo headset pela ponte do PC. **Nada é executado automaticamente**: "
    "o Claude Code (ou você) lê esta lista e atende o que quiser. Marque `[x]` quando um pedido for atendido.\n\n"
)
LINHA = re.compile(r"^- \[( |x)\] #(\d+) · (\d{4}-\d\d-\d\d \d\d:\d\d) · (.*)$")
MAX_TEXTO = 500
MAX_CORPO = 4096

trava = threading.Lock()
acessos = {}   # ip -> [timestamps do último minuto]
erros = {}     # ip -> [timestamps de senha errada]


def ler_senha():
    s = os.environ.get("ZETTA_PONTE_SENHA", "").strip()
    if s:
        return s
    caminho = os.path.join(AQUI, ".senha")
    if os.path.exists(caminho):
        with open(caminho, encoding="utf-8") as f:
            return f.read().strip()
    return ""


def ler_fila(arquivo):
    itens = []
    if os.path.exists(arquivo):
        with open(arquivo, encoding="utf-8") as f:
            for linha in f:
                m = LINHA.match(linha.rstrip("\n"))
                if m:
                    itens.append({"id": int(m.group(2)), "feito": m.group(1) == "x", "quando": m.group(3), "texto": m.group(4)})
    return itens


def adicionar(arquivo, texto):
    with trava:
        itens = ler_fila(arquivo)
        novo = max([i["id"] for i in itens], default=0) + 1
        if not os.path.exists(arquivo) or os.path.getsize(arquivo) == 0:
            os.makedirs(os.path.dirname(arquivo), exist_ok=True)
            with open(arquivo, "w", encoding="utf-8") as f:
                f.write(CABECALHO)
        with open(arquivo, "a", encoding="utf-8") as f:
            f.write("- [ ] #%d · %s · %s\n" % (novo, datetime.now().strftime("%Y-%m-%d %H:%M"), texto))
        return novo, sum(1 for i in itens if not i["feito"]) + 1


def limpar_texto(t):
    t = re.sub(r"\s+", " ", str(t)).strip()
    return t[:MAX_TEXTO]


def fazer_handler(senha, arquivo):
    class H(BaseHTTPRequestHandler):
        server_version = "ZettaPonte"

        def log_message(self, fmt, *args):   # sem a senha nem o corpo nos logs
            sys.stderr.write("[%s] %s %s\n" % (datetime.now().strftime("%H:%M:%S"), self.client_address[0], fmt % args))

        def responder(self, codigo, obj):
            corpo = json.dumps(obj, ensure_ascii=False).encode("utf-8")
            self.send_response(codigo)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(corpo)))
            self.end_headers()
            self.wfile.write(corpo)

        def autorizar(self):
            ip = self.client_address[0]
            try:
                end = ipaddress.ip_address(ip)
                if not (end.is_private or end.is_loopback):
                    self.responder(403, {"erro": "só rede local"})
                    return False
            except ValueError:
                self.responder(403, {"erro": "endereço inválido"})
                return False
            agora = time.time()
            with trava:
                erros[ip] = [t for t in erros.get(ip, []) if agora - t < 300]
                if len(erros[ip]) >= 5:
                    self.responder(429, {"erro": "bloqueado por 5 minutos (senha errada demais)"})
                    return False
                acessos[ip] = [t for t in acessos.get(ip, []) if agora - t < 60]
                if len(acessos[ip]) >= 30:
                    self.responder(429, {"erro": "muitos pedidos por minuto"})
                    return False
                acessos[ip].append(agora)
            enviada = self.headers.get("X-Zetta-Senha", "")
            if not hmac.compare_digest(enviada.encode("utf-8"), senha.encode("utf-8")):
                with trava:
                    erros[ip].append(agora)
                self.responder(401, {"erro": "senha incorreta"})
                return False
            return True

        def do_GET(self):
            if not self.autorizar():
                return
            if self.path == "/status":
                itens = ler_fila(arquivo)
                self.responder(200, {"ok": True, "pendentes": sum(1 for i in itens if not i["feito"])})
            elif self.path == "/fila":
                itens = ler_fila(arquivo)
                pend = [i for i in itens if not i["feito"]]
                self.responder(200, {"pendentes": pend[-10:], "total_pendentes": len(pend), "feitos": len(itens) - len(pend)})
            else:
                self.responder(404, {"erro": "rota desconhecida"})

        def do_POST(self):
            if not self.autorizar():
                return
            if self.path != "/pedido":
                self.responder(404, {"erro": "rota desconhecida"})
                return
            try:
                tam = int(self.headers.get("Content-Length", "0"))
                if tam <= 0 or tam > MAX_CORPO:
                    self.responder(413, {"erro": "corpo inválido ou grande demais"})
                    return
                dados = json.loads(self.rfile.read(tam).decode("utf-8"))
                texto = limpar_texto(dados.get("texto", ""))
            except (ValueError, UnicodeDecodeError):
                self.responder(400, {"erro": "JSON inválido"})
                return
            if len(texto) < 3:
                self.responder(400, {"erro": "pedido vazio"})
                return
            novo, pend = adicionar(arquivo, texto)
            self.responder(200, {"ok": True, "id": novo, "pendentes": pend})

    return H


def main():
    ap = argparse.ArgumentParser(description="Ponte PC da Zetta (fila de pedidos na rede local)")
    ap.add_argument("--porta", type=int, default=8765)
    ap.add_argument("--arquivo", default=os.path.join(RAIZ, "Docs", "pedidos-zetta.md"))
    ap.add_argument("--host", default="0.0.0.0")
    a = ap.parse_args()
    senha = ler_senha()
    if len(senha) < 16:
        print("Senha ausente ou curta demais. No Unity: menu Zetta > Ponte PC > Gerar senha (ou defina ZETTA_PONTE_SENHA).")
        sys.exit(1)
    srv = ThreadingHTTPServer((a.host, a.porta), fazer_handler(senha, a.arquivo))
    print("Ponte da Zetta ouvindo na porta %d (rede local). Fila: %s\nCtrl+C para parar." % (a.porta, a.arquivo))
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\nPonte encerrada.")


if __name__ == "__main__":
    main()
