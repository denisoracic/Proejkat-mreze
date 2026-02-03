using Shared;
using SlagalicaServer.Networking;

namespace SlagalicaServer.Game;

public class GameManager
{
    private readonly GameServer _server;

    private IGame? _currentGame;
    private CancellationTokenSource? _roundCts;

    private bool _roundActive = false;
    private ClientHandler? _winner = null;

    public GameManager(GameServer server)
    {
        _server = server;
    }

    public async Task RunAsync()
    {
        // čeka da se bar 1 klijent poveže (trening mod)
        while (_server.Clients.Count < 1)
            await Task.Delay(200);

        // kratak wait da klijenti stignu da se registruju
        await Task.Delay(800);

        var games = new List<IGame>
        {
            new MojBrojGame(),
            new SkockoGame(),
            new KoZnaZnaGame()
        };

        foreach (var g in games)
        {
            _currentGame = g;
            await StartRoundAsync(g);
            await Task.Delay(800);
        }

        await BroadcastScoresAsync("KRAJ IGRE! Konačni rezultati:");
    }

    private async Task StartRoundAsync(IGame game)
    {
        _roundCts?.Cancel();
        _roundCts = new CancellationTokenSource();

        _winner = null;
        _roundActive = true;

        game.StartRound();

        var start = new RoundStart
        {
            Game = game.Type,
            DurationSeconds = game.DurationSeconds,
            Prompt = game.GetPrompt()
        };

        await BroadcastAsync(new Message
        {
            Type = "ROUND_START",
            Data = System.Text.Json.JsonSerializer.Serialize(start)
        });

        // Pošalji start jednostavnije: direktno JSON payload (bez ugnježdenog Message)
        await BroadcastAsync(new Message
        {
            Type = "ROUND_START",
            Data = System.Text.Json.JsonSerializer.Serialize(start)
        });

        try
        {
            await Task.Delay(game.DurationSeconds * 1000, _roundCts.Token);
        }
        catch (TaskCanceledException) { }

        _roundActive = false;

        var end = new RoundEnd
        {
            Game = game.Type,
            CorrectAnswer = game.CorrectAnswer,
            Info = "Runda završena."
        };

        await BroadcastAsync(new Message
        {
            Type = "ROUND_END",
            Data = System.Text.Json.JsonSerializer.Serialize(end)
        });

        await BroadcastScoresAsync($"Rezultati posle {game.Type}:");
    }

    public async Task<bool> TryAnswerAsync(ClientHandler player, string answer)
    {
        if (!_roundActive || _currentGame == null) return false;

        // Ako već imamo pobednika (prvi tačan), ignoriši
        if (_winner != null) return false;

        if (_currentGame.CheckAnswer(answer))
        {
            _winner = player;
            player.Player.Points += _currentGame.PointsForWin;

            _roundActive = false;
            _roundCts?.Cancel();

            await BroadcastAsync(new Message
            {
                Type = "INFO",
                Data = $"Pobednik runde ({_currentGame.Type}): {player.Player.Name} (+{_currentGame.PointsForWin})"
            });

            return true;
        }

        // netačan odgovor – možeš da vratiš privatno
        await player.Send(new Message { Type = "RESULT", Data = "Netačno." });
        return false;
    }

    private async Task BroadcastAsync(Message msg)
    {
        foreach (var c in _server.Clients.ToList())
        {
            try { await c.Send(msg); } catch { /* ignore */ }
        }
    }

    private async Task BroadcastScoresAsync(string header)
    {
        var lines = new List<string> { header };
        foreach (var c in _server.Clients)
            lines.Add($"{c.Player.Name}: {c.Player.Points}");

        await BroadcastAsync(new Message { Type = "SCORES", Data = string.Join(Environment.NewLine, lines) });
    }
}
