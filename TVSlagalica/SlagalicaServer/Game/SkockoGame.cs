using Shared;

namespace SlagalicaServer.Game;

public class SkockoGame : IGame
{
    public GameType Type => GameType.Skocko;
    public int DurationSeconds => 35;
    public int PointsForWin => 20;

    public string CorrectAnswer { get; private set; } = "";

    // 6 simbola: 1-6, kombinacija 4 mesta
    private int[] combo = new int[4];

    public void StartRound()
    {
        var rnd = new Random();
        for (int i = 0; i < 4; i++)
            combo[i] = rnd.Next(1, 7);

        CorrectAnswer = string.Join("", combo); // npr "3612"
    }

    public string GetPrompt()
        => "Skočko: unesi 4 cifre (1-6), bez razmaka. Primer: 3612";

    public bool CheckAnswer(string answer)
        => answer.Trim() == CorrectAnswer;
}
