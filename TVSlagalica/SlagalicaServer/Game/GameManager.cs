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
    public bool RegistrationOpen { get; private set; } = true;

    public GameManager(GameServer server)
    {
        _server = server;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("Čekam prvog igrača da se registruje...");

        while (_server.Clients.Count(c => c.IsRegistered) == 0)
            await Task.Delay(200);

        Console.WriteLine("Prvi igrač se registrovao — pokrećem tajmer od 30s.");

        int waited = 0;

        while (waited < 30)
        {
            int registered = _server.Clients.Count(c => c.IsRegistered);

            if (registered >= 2)
            {
                Console.WriteLine("Dva igrača spremna – start igre!");
                break;
            }

            await Task.Delay(1000);
            waited++;
        }

        int finalCount = _server.Clients.Count(c => c.IsRegistered);

        if (finalCount == 1)
            Console.WriteLine("Startujem sa jednim igračem (trening mod).");
        else
            Console.WriteLine($"Startujem sa {finalCount} igrača.");

        RegistrationOpen = false;


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
        if (!_roundActive || _currentGame == null)
            return false;

        if (_currentGame is SkockoGame skocko)
        {
            string name = player.Player.Name;

            if (skocko.HasWon(name))
            {
                await player.Send(new Message
                {
                    Type = "RESULT",
                    Data = "Već si pogodio kombinaciju!"
                });
                return false;
            }

            if (!skocko.HasAttempts(name))
            {
                await player.Send(new Message
                {
                    Type = "RESULT",
                    Data = "Nemaš više pokušaja."
                });
                return false;
            }

            skocko.UseAttempt(name);

            if (skocko.CheckAnswer(answer))
            {
                bool first = skocko.RegisterWin(name);

                int points = first ? 15 : 10;
                player.Player.Points += points;

                await BroadcastAsync(new Message
                {
                    Type = "INFO",
                    Data = $"{name} je pogodio Skočko! +{points} poena"
                });
            }
            else
            {
                string fb = skocko.GetFeedback(answer);

                await player.Send(new Message
                {
                    Type = "RESULT",
                    Data = $"Feedback: {fb} | Pokušaja preostalo: {skocko.GetAttemptsLeft(name)}"
                });
            }

            if (skocko.BothFinished())
            {
                _roundActive = false;
                _roundCts?.Cancel();
            }

            return true;
        }

        if (_winner != null)
            return false;

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

        string feedback = _currentGame.GetFeedback(answer);

        await player.Send(new Message
        {
            Type = "RESULT",
            Data = feedback
        });

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
