using Shared;

namespace SlagalicaServer.Game;

public class MojBrojGame : IGame
{
    public GameType Type => GameType.MojBroj;
    public int DurationSeconds => 30;
    public int PointsForWin => 15;

    public string CorrectAnswer { get; private set; } = "";

    private int target;
    private int[] nums = Array.Empty<int>();

    public void StartRound()
    {
        var rnd = new Random();
        target = rnd.Next(100, 1000);
        nums = new[] { rnd.Next(1, 10), rnd.Next(1, 10), rnd.Next(1, 10), rnd.Next(1, 10), rnd.Next(10, 51), rnd.Next(10, 51) };

        // Za sada: "tačan odgovor" je samo target kao string (demo)
        // Kasnije ubacujemo evaluaciju izraza!
        CorrectAnswer = target.ToString();
    }

    public string GetPrompt()
        => $"Cilj: {target}\nBrojevi: {string.Join(", ", nums)}\n(Demo verzija: upiši tačno cilj kao odgovor)";

    public bool CheckAnswer(string answer)
        => answer.Trim() == CorrectAnswer;
}
