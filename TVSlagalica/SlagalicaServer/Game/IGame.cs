using Shared;

namespace SlagalicaServer.Game;

public interface IGame
{
    GameType Type { get; }
    int DurationSeconds { get; }
    int PointsForWin { get; }
    string CorrectAnswer { get; }

    void StartRound();
    string GetPrompt();
    bool CheckAnswer(string answer);
    string GetFeedback(string answer);

}
