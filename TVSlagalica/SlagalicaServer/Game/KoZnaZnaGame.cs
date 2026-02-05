using Shared;

namespace SlagalicaServer.Game;

public class KoZnaZnaGame : IGame
{
    public GameType Type => GameType.KoZnaZna;
    public int DurationSeconds => 25;
    public int PointsForWin => 10;

    public string CorrectAnswer { get; private set; } = "";
    private string question = "";

    public void StartRound()
    {
        // Kasnije: baza pitanja. Sad: demo pitanje.
        question = "Koji je glavni grad Srbije?";
        CorrectAnswer = "beograd";
    }

    public string GetPrompt()
        => $"Ko zna zna:\nPitanje: {question}";

    public bool CheckAnswer(string answer)
        => answer.Trim().ToLower() == CorrectAnswer;
    public string GetFeedback(string answer)
    {
        return CheckAnswer(answer) ? "TAČNO" : "NETAČNO";
    }

}
