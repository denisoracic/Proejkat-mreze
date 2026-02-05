using Shared;
using System.Globalization;

namespace SlagalicaServer.Game;

public class MojBrojGame : IGame
{
    public GameType Type => GameType.MojBroj;
    public int DurationSeconds => 120;
    public int PointsForWin => 15;

    public string CorrectAnswer { get; private set; } = ""; // ovde možemo ostaviti cilj kao string
    public int Target { get; private set; }
    public int[] Numbers { get; private set; } = Array.Empty<int>();

    public void StartRound()
    {
        var rnd = new Random();

        Target = rnd.Next(100, 1000);
        Numbers = new[]
        {
            rnd.Next(1, 10),
            rnd.Next(1, 10),
            rnd.Next(1, 10),
            rnd.Next(1, 10),
            rnd.Next(10, 51),
            rnd.Next(10, 51)
        };

        CorrectAnswer = Target.ToString();
    }

    public string GetPrompt()
        => $"Moj broj:\nCilj: {Target}\nBrojevi: {string.Join(", ", Numbers)}\n" +
           $"Unesi izraz koristeći ponuđene brojeve (svaki najviše jednom) i operacije + - * / i zagrade.";

    public bool CheckAnswer(string answer)
    {
        if (!TryEvaluate(answer, out double value, out _)) return false;
        return Math.Abs(value - Target) < 1e-9;
    }

    public string GetFeedback(string answer)
    {
        if (!TryEvaluate(answer, out double value, out string err))
            return $"Greška: {err}";

        double diff = Math.Abs(value - Target);
        return $"Vrednost izraza: {value} | Razlika od cilja: {diff}";
    }

    public bool TryEvaluate(string expr, out double value, out string error)
    {
        value = 0;
        error = "";

        expr = (expr ?? "").Trim();
        if (expr.Length == 0)
        {
            error = "Prazan unos.";
            return false;
        }

        // 1) Validacija da su brojevi iz izraza podskup ponuđenih brojeva (sa ponavljanjima)
        if (!ValidateNumbersUsage(expr, out error))
            return false;

        // 2) Izračunaj izraz (siguran parser, bez eval/skripti)
        if (!TryEvalArithmetic(expr, out value, out error))
            return false;

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            error = "Nevažeći rezultat (NaN/Inf).";
            return false;
        }

        return true;
    }

    private bool ValidateNumbersUsage(string expr, out string error)
    {
        error = "";

        var available = new Dictionary<int, int>();
        foreach (var n in Numbers)
        {
            if (!available.ContainsKey(n)) available[n] = 0;
            available[n]++;
        }

        // izvuci sve integer brojeve iz izraza (npr "-3" broji kao 3)
        var used = new Dictionary<int, int>();
        var num = "";
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (char.IsDigit(c))
            {
                num += c;
            }
            else
            {
                if (num.Length > 0)
                {
                    int v = int.Parse(num, CultureInfo.InvariantCulture);
                    if (!used.ContainsKey(v)) used[v] = 0;
                    used[v]++;
                    num = "";
                }
            }
        }
        if (num.Length > 0)
        {
            int v = int.Parse(num, CultureInfo.InvariantCulture);
            if (!used.ContainsKey(v)) used[v] = 0;
            used[v]++;
        }

        foreach (var kv in used)
        {
            int v = kv.Key;
            int cnt = kv.Value;
            if (!available.ContainsKey(v) || available[v] < cnt)
            {
                error = $"Koristiš broj {v} {cnt}x, a ponuđen je {(available.ContainsKey(v) ? available[v] : 0)}x.";
                return false;
            }
        }

        return true;
    }

    // ====== Parser + evaluator (Shunting-yard) ======

    private static bool TryEvalArithmetic(string expr, out double value, out string error)
    {
        value = 0;
        error = "";

        if (!TryTokenize(expr, out var tokens, out error))
            return false;

        if (!TryToRpn(tokens, out var rpn, out error))
            return false;

        if (!TryEvalRpn(rpn, out value, out error))
            return false;

        return true;
    }

    private static bool TryTokenize(string expr, out List<string> tokens, out string error)
    {
        tokens = new List<string>();
        error = "";

        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c))
            {
                int j = i;
                while (j < expr.Length && char.IsDigit(expr[j])) j++;
                tokens.Add(expr.Substring(i, j - i));
                i = j;
                continue;
            }

            if ("+-*/()".Contains(c))
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            error = $"Nedozvoljen znak: '{c}'.";
            return false;
        }

        // Obradi unarni minus: pretvori "- X" u "0 X -"
        var fixedTokens = new List<string>();
        for (int k = 0; k < tokens.Count; k++)
        {
            string t = tokens[k];
            if (t == "-" && (k == 0 || tokens[k - 1] == "(" || IsOp(tokens[k - 1])))
            {
                fixedTokens.Add("0");
                fixedTokens.Add("-");
            }
            else fixedTokens.Add(t);
        }

        tokens = fixedTokens;
        return true;
    }

    private static bool TryToRpn(List<string> tokens, out List<string> output, out string error)
    {
        output = new List<string>();
        error = "";
        var ops = new Stack<string>();

        foreach (var t in tokens)
        {
            if (IsNumber(t)) output.Add(t);
            else if (IsOp(t))
            {
                while (ops.Count > 0 && IsOp(ops.Peek()) &&
                       (Prec(ops.Peek()) >= Prec(t)))
                {
                    output.Add(ops.Pop());
                }
                ops.Push(t);
            }
            else if (t == "(") ops.Push(t);
            else if (t == ")")
            {
                bool found = false;
                while (ops.Count > 0)
                {
                    var x = ops.Pop();
                    if (x == "(") { found = true; break; }
                    output.Add(x);
                }
                if (!found) { error = "Neuparene zagrade."; return false; }
            }
            else { error = "Nepoznat token."; return false; }
        }

        while (ops.Count > 0)
        {
            var x = ops.Pop();
            if (x == "(" || x == ")") { error = "Neuparene zagrade."; return false; }
            output.Add(x);
        }

        return true;
    }

    private static bool TryEvalRpn(List<string> rpn, out double value, out string error)
    {
        value = 0;
        error = "";
        var st = new Stack<double>();

        foreach (var t in rpn)
        {
            if (IsNumber(t))
            {
                st.Push(double.Parse(t, CultureInfo.InvariantCulture));
                continue;
            }

            if (IsOp(t))
            {
                if (st.Count < 2) { error = "Neispravan izraz."; return false; }
                double b = st.Pop();
                double a = st.Pop();

                double r;
                switch (t)
                {
                    case "+": r = a + b; break;
                    case "-": r = a - b; break;
                    case "*": r = a * b; break;
                    case "/":
                        if (Math.Abs(b) < 1e-12) { error = "Deljenje nulom."; return false; }
                        r = a / b;
                        break;
                    default:
                        error = "Nepoznata operacija.";
                        return false;
                }
                st.Push(r);
                continue;
            }

            error = "Neispravan token u evaluaciji.";
            return false;
        }

        if (st.Count != 1) { error = "Neispravan izraz (višak elemenata)."; return false; }
        value = st.Pop();
        return true;
    }

    private static bool IsNumber(string t) => t.All(char.IsDigit);
    private static bool IsOp(string t) => t is "+" or "-" or "*" or "/";
    private static int Prec(string op) => (op is "*" or "/") ? 2 : 1;
}
