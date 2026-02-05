using Shared;

namespace SlagalicaServer.Game;

public class SkockoGame : IGame
{
    public GameType Type => GameType.Skocko;
    public int DurationSeconds => 120;
    public int PointsForWin => 15; // prvi pogodak

    public string CorrectAnswer { get; private set; } = "";

    private int[] combo = new int[4];

    private Dictionary<string, int> attemptsLeft = new();
    private HashSet<string> winners = new();

    public void StartRound()
    {
        var rnd = new Random();

        for (int i = 0; i < 4; i++)
            combo[i] = rnd.Next(1, 7);

        CorrectAnswer = string.Join("", combo);

        attemptsLeft.Clear();
        winners.Clear();
    }

    public string GetPrompt()
        => "Skočko: unesi 4 cifre (1-6). Imaš 8 pokušaja.";

    public bool CheckAnswer(string answer)
        => Normalize(answer) == CorrectAnswer;

    public string GetFeedback(string answer)
    {
        string guess = Normalize(answer);

        if (guess.Length != 4 || guess.Any(c => c < '1' || c > '6'))
            return "Neispravan unos (4 cifre 1–6)";

        int[] g = guess.Select(c => c - '0').ToArray();
        int[] s = combo.ToArray();

        char[] res = new char[4];
        int[] countS = new int[7];
        int[] countG = new int[7];

        for (int i = 0; i < 4; i++)
        {
            if (g[i] == s[i])
                res[i] = 'T';
            else
            {
                countS[s[i]]++;
                countG[g[i]]++;
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (res[i] == 'T') continue;

            int sym = g[i];
            if (countS[sym] > 0 && countG[sym] > 0)
            {
                res[i] = 'M';
                countS[sym]--;
                countG[sym]--;
            }
            else res[i] = 'N';
        }

        return new string(res);
    }


    public int GetAttemptsLeft(string player)
    {
        if (!attemptsLeft.ContainsKey(player))
            attemptsLeft[player] = 8;

        return attemptsLeft[player];
    }

    public void UseAttempt(string player)
    {
        if (!attemptsLeft.ContainsKey(player))
            attemptsLeft[player] = 8;

        if (attemptsLeft[player] > 0)
            attemptsLeft[player]--;
    }

    public bool HasAttempts(string player)
        => GetAttemptsLeft(player) > 0;

    public bool HasWon(string player)
        => winners.Contains(player);

    public bool RegisterWin(string player)
    {
        return winners.Add(player); 
    }

    public bool BothFinished()
        => winners.Count >= 2 || attemptsLeft.Values.All(v => v == 0);
    private static string Normalize(string s)
    {
        return (s ?? "").Trim().Replace(" ", "");
    }

}
