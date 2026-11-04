using System;
using System.Collections.Generic;
using System.Globalization;

// Parser seguro de expressões matemáticas (sem eval, sem reflexão, sem código externo): recursão descendente que
// monta uma árvore de nós. Variáveis: x, y, t. Constantes: pi, e. Operadores: + - * / ^ (potência, associativa à direita),
// menos unário, multiplicação implícita (2x, 2(x+1), x sen(y)). Funções: sen/sin, cos, tan, asen/asin, acos, atan,
// senh, cosh, tanh, exp, ln (natural), log (base 10), log2, raiz/sqrt, abs, piso, teto, sinal, min, max, pot.
public sealed class ExpressaoMatematica
{
    public const int MaxCaracteres = 240;
    const int MaxProfundidade = 48;

    abstract class No { public abstract double Av(double x, double y, double t); }
    sealed class Num : No { public double v; public override double Av(double x, double y, double t) => v; }
    sealed class Var : No { public int i; public override double Av(double x, double y, double t) => i == 0 ? x : (i == 1 ? y : t); }
    sealed class Neg : No { public No a; public override double Av(double x, double y, double t) => -a.Av(x, y, t); }
    sealed class Bin : No
    {
        public char op; public No a, b;
        public override double Av(double x, double y, double t)
        {
            double p = a.Av(x, y, t), q = b.Av(x, y, t);
            switch (op)
            {
                case '+': return p + q;
                case '-': return p - q;
                case '*': return p * q;
                case '/': return p / q;
                default: return Math.Pow(p, q);
            }
        }
    }
    sealed class Fn : No
    {
        public Func<double[], double> f; public No[] args;
        public override double Av(double x, double y, double t)
        {
            var v = new double[args.Length];
            for (int i = 0; i < v.Length; i++) v[i] = args[i].Av(x, y, t);
            return f(v);
        }
    }

    static readonly Dictionary<string, (int n, Func<double[], double> f)> Funcoes = new Dictionary<string, (int, Func<double[], double>)>
    {
        { "sen", (1, a => Math.Sin(a[0])) }, { "sin", (1, a => Math.Sin(a[0])) }, { "cos", (1, a => Math.Cos(a[0])) },
        { "tan", (1, a => Math.Tan(a[0])) }, { "tg", (1, a => Math.Tan(a[0])) },
        { "asen", (1, a => Math.Asin(a[0])) }, { "asin", (1, a => Math.Asin(a[0])) }, { "arcsen", (1, a => Math.Asin(a[0])) },
        { "acos", (1, a => Math.Acos(a[0])) }, { "arccos", (1, a => Math.Acos(a[0])) },
        { "atan", (1, a => Math.Atan(a[0])) }, { "arctan", (1, a => Math.Atan(a[0])) },
        { "senh", (1, a => Math.Sinh(a[0])) }, { "sinh", (1, a => Math.Sinh(a[0])) }, { "cosh", (1, a => Math.Cosh(a[0])) }, { "tanh", (1, a => Math.Tanh(a[0])) },
        { "exp", (1, a => Math.Exp(a[0])) }, { "ln", (1, a => Math.Log(a[0])) }, { "log", (1, a => Math.Log10(a[0])) }, { "log2", (1, a => Math.Log(a[0], 2)) },
        { "raiz", (1, a => Math.Sqrt(a[0])) }, { "sqrt", (1, a => Math.Sqrt(a[0])) }, { "abs", (1, a => Math.Abs(a[0])) },
        { "piso", (1, a => Math.Floor(a[0])) }, { "teto", (1, a => Math.Ceiling(a[0])) }, { "sinal", (1, a => Math.Sign(a[0])) },
        { "min", (2, a => Math.Min(a[0], a[1])) }, { "max", (2, a => Math.Max(a[0], a[1])) }, { "pot", (2, a => Math.Pow(a[0], a[1])) },
    };

    readonly No raiz;
    public string Texto { get; }

    ExpressaoMatematica(No r, string texto) { raiz = r; Texto = texto; }

    public double Avaliar(double x = 0, double y = 0, double t = 0) => raiz.Av(x, y, t);

    // true se a expressão é válida; 'erro' diz o motivo em português
    public static bool Tentar(string texto, out ExpressaoMatematica expr, out string erro)
    {
        expr = null; erro = null;
        if (string.IsNullOrWhiteSpace(texto)) { erro = "expressão vazia"; return false; }
        if (texto.Length > MaxCaracteres) { erro = "expressão longa demais"; return false; }
        try
        {
            var p = new Analisador(texto);
            var no = p.Analisar();
            expr = new ExpressaoMatematica(no, texto.Trim());
            return true;
        }
        catch (FormatException e) { erro = e.Message; return false; }
    }

    // formata o número para mostrar (sem notação científica para valores comuns)
    public static string Formatar(double v)
    {
        if (double.IsNaN(v)) return "indefinido";
        if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
        if (Math.Abs(v) >= 1e9 || (Math.Abs(v) < 1e-6 && v != 0)) return v.ToString("0.#####e+0", CultureInfo.InvariantCulture);
        return Math.Round(v, 8).ToString("0.########", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    sealed class Analisador
    {
        readonly string s;
        int i;
        int prof;

        public Analisador(string texto)
        {
            // aceita vírgula decimal, × ÷ − · e o ² ³ comuns
            s = texto.Trim().ToLowerInvariant().Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace('·', '*')
                .Replace("²", "^2").Replace("³", "^3").Replace("π", "pi").Replace("√", "raiz");
        }

        public No Analisar()
        {
            var r = Soma();
            Espaco();
            if (i < s.Length) throw new FormatException("não entendi '" + s.Substring(i, Math.Min(6, s.Length - i)) + "'");
            return r;
        }

        void Espaco() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        bool Ler(char c) { Espaco(); if (i < s.Length && s[i] == c) { i++; return true; } return false; }

        No Soma()
        {
            var a = Produto();
            while (true)
            {
                if (Ler('+')) a = new Bin { op = '+', a = a, b = Produto() };
                else if (Ler('-')) a = new Bin { op = '-', a = a, b = Produto() };
                else return a;
            }
        }

        No Produto()
        {
            var a = Unario();
            while (true)
            {
                if (Ler('*')) a = new Bin { op = '*', a = a, b = Unario() };
                else if (Ler('/')) a = new Bin { op = '/', a = a, b = Unario() };
                else if (ComecaPrimario()) a = new Bin { op = '*', a = a, b = Unario() };   // multiplicação implícita
                else return a;
            }
        }

        bool ComecaPrimario()
        {
            Espaco();
            if (i >= s.Length) return false;
            char c = s[i];
            return char.IsDigit(c) || c == '.' || c == ',' || c == '(' || char.IsLetter(c);
        }

        No Unario()
        {
            if (Ler('-')) return new Neg { a = Unario() };
            if (Ler('+')) return Unario();
            return Potencia();
        }

        No Potencia()
        {
            var b = Primario();
            if (Ler('^')) return new Bin { op = '^', a = b, b = Unario() };   // direita: 2^3^2 = 2^(3^2); -x^2 = -(x^2)
            return b;
        }

        No Primario()
        {
            if (++prof > MaxProfundidade) throw new FormatException("expressão aninhada demais");
            try
            {
                Espaco();
                if (i >= s.Length) throw new FormatException("a expressão terminou antes da hora");
                char c = s[i];
                if (c == '(')
                {
                    i++;
                    var e = Soma();
                    if (!Ler(')')) throw new FormatException("falta fechar o parêntese");
                    return e;
                }
                if (char.IsDigit(c) || c == '.' || c == ',') return Numero();
                if (char.IsLetter(c))
                {
                    int ini = i;
                    while (i < s.Length && (char.IsLetter(s[i]) || (char.IsDigit(s[i]) && i > ini && FuncaoComNumero(s.Substring(ini, i - ini + 1))))) i++;
                    string nome = s.Substring(ini, i - ini);
                    Espaco();
                    if (Funcoes.TryGetValue(nome, out var def))
                    {
                        if (!Ler('(')) throw new FormatException("'" + nome + "' precisa de parênteses");
                        var args = new List<No>();
                        if (!Ler(')'))
                        {
                            do { args.Add(Soma()); } while (Ler(';') || Ler(','));
                            if (!Ler(')')) throw new FormatException("falta fechar o parêntese de " + nome);
                        }
                        if (args.Count != def.n) throw new FormatException(nome + " leva " + def.n + " valor(es)");
                        return new Fn { f = def.f, args = args.ToArray() };
                    }
                    if (nome == "x") return new Var { i = 0 };
                    if (nome == "y") return new Var { i = 1 };
                    if (nome == "t") return new Var { i = 2 };
                    if (nome == "pi") return new Num { v = Math.PI };
                    if (nome == "e") return new Num { v = Math.E };
                    // letras coladas (xy, xt): divide em variáveis/constantes por multiplicação implícita
                    if (nome.Length > 1 && TudoVariavel(nome))
                    {
                        No acc = null;
                        foreach (char ch in nome) { var n = ch == 'x' ? (No)new Var { i = 0 } : ch == 'y' ? new Var { i = 1 } : ch == 't' ? new Var { i = 2 } : new Num { v = Math.E }; acc = acc == null ? n : new Bin { op = '*', a = acc, b = n }; }
                        return acc;
                    }
                    throw new FormatException("não conheço '" + nome + "'");
                }
                throw new FormatException("símbolo inesperado '" + c + "'");
            }
            finally { prof--; }
        }

        static bool FuncaoComNumero(string nome) => nome == "log2";
        static bool TudoVariavel(string n) { foreach (char c in n) if (c != 'x' && c != 'y' && c != 't' && c != 'e') return false; return true; }

        No Numero()
        {
            int ini = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == ',')) i++;
            // notação científica 1e-3 (só se o 'e' for seguido de dígito ou sinal+dígito)
            if (i < s.Length && s[i] == 'e' && i + 1 < s.Length && (char.IsDigit(s[i + 1]) || ((s[i + 1] == '-' || s[i + 1] == '+') && i + 2 < s.Length && char.IsDigit(s[i + 2]))))
            {
                i += 2;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string txt = s.Substring(ini, i - ini).Replace(',', '.');
            if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) throw new FormatException("número inválido '" + txt + "'");
            return new Num { v = v };
        }
    }
}
