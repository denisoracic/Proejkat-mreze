using Shared;
using SlagalicaServer.Networking;

namespace SlagalicaServer.Game;

public static class GameLogic
{
    private static bool answered = false;

    public static async void ProcessAnswer(ClientHandler player, string answer)
    {
        if (answered) return;

        answered = true;

        if (CheckAnswer(answer))
        {
            player.Player.Points += 10;

            await player.Send(new Message
            {
                Type = "RESULT",
                Data = "Tačan odgovor! +10 poena"
            });
        }
        else
        {
            await player.Send(new Message
            {
                Type = "RESULT",
                Data = "Netačan odgovor."
            });
        }
    }

    private static bool CheckAnswer(string ans)
    {
        return ans.ToLower() == "test";
    }

    public static void Reset()
    {
        answered = false;
    }
}
