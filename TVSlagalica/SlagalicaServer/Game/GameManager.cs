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

    private readonly Dictionary<string, (double value, double diff)> _mojBrojResults = new();
    private readonly HashSet<string> _mojBrojTried = new();
    private bool _mojBrojWinnerAwarded = false;

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

        if (game is MojBrojGame)
        {
            _mojBrojResults.Clear();
            _mojBrojTried.Clear();
            _mojBrojWinnerAwarded = false;
        }

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

        bool timeExpired = true;

        try
        {
            await Task.Delay(game.DurationSeconds * 1000, _roundCts.Token);
        }
        catch (TaskCanceledException)
        {
            timeExpired = false; 
        }

        _roundActive = false;
        if (timeExpired && game is MojBrojGame moj2 && !_mojBrojWinnerAwarded)
        {
            var regs = _server.Clients.Where(c => c.IsRegistered).ToList();
            if (regs.Count > 0)
            {
                var best = regs
                .Select(c => (c, res: _mojBrojResults.TryGetValue(c.Player.Name, out var r)
                ? r
                : (value: double.NaN, diff: double.PositiveInfinity)))
                .OrderBy(x => x.res.diff)
                .ToList();


                if (best.Count >= 1 && best[0].res.diff < double.PositiveInfinity)
                {
                    bool tie = best.Count >= 2 && Math.Abs(best[0].res.diff - best[1].res.diff) < 1e-9;

                    if (!tie)
                    {
                        best[0].c.Player.Points += moj2.PointsForWin;
                        _mojBrojWinnerAwarded = true;
                        await BroadcastAsync(new Message
                        {
                            Type = "INFO",
                            Data = $"Vreme je isteklo. Najbliži je {best[0].c.Player.Name} (+{moj2.PointsForWin})"
                        });
                    }
                    else
                    {
                        await BroadcastAsync(new Message
                        {
                            Type = "INFO",
                            Data = "Vreme je isteklo. Nerešeno (isti razmak)."
                        });
                    }
                }
            }
        }

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

        if (_currentGame is MojBrojGame moj)
        {
            string name = player.Player.Name;

            if (_mojBrojTried.Contains(name))
            {
                await player.Send(new Message { Type = "RESULT", Data = "Već si iskoristio pokušaj u igri Moj broj." });
                return false;
            }

            _mojBrojTried.Add(name);

            if (!moj.TryEvaluate(answer, out double val, out string err))
            {
                await player.Send(new Message { Type = "RESULT", Data = $"Neispravan izraz: {err}" });
                // tretiramo kao promašaj (nema ponovnog pokušaja)
                _mojBrojResults[name] = (double.NaN, double.PositiveInfinity);
            }
            else
            {
                double diff = Math.Abs(val - moj.Target);
                _mojBrojResults[name] = (val, diff);

                await player.Send(new Message
                {
                    Type = "RESULT",
                    Data = $"Rezultat: {val} | Razlika: {diff}"
                });

                // Ako je tačno, prvi koji pošalje tačno odmah dobija poene i runda se završava
                if (diff < 1e-9 && !_mojBrojWinnerAwarded)
                {
                    _mojBrojWinnerAwarded = true;
                    player.Player.Points += moj.PointsForWin;

                    await BroadcastAsync(new Message
                    {
                        Type = "INFO",
                        Data = $"{name} je prvi tačno pogodio Moj broj! +{moj.PointsForWin} poena"
                    });

                    _roundActive = false;
                    _roundCts?.Cancel();
                    return true;
                }
            }

            // Ako su svi registrovani igrači iskoristili pokušaj, završavamo rundu i dodeljujemo "najbliži"
            var regs = _server.Clients.Where(c => c.IsRegistered).ToList();
            if (regs.Count > 0 && regs.All(c => _mojBrojTried.Contains(c.Player.Name)))
            {
                if (!_mojBrojWinnerAwarded)
                {
                    // niko nije pogodio tačno -> najbliži dobija poene
                    var best = regs
                    .Select(c => (c, res: _mojBrojResults.TryGetValue(c.Player.Name, out var r)
                    ? r
                    : (value: double.NaN, diff: double.PositiveInfinity)))
                    .OrderBy(x => x.res.diff)
                    .ToList();


                    if (best.Count >= 1 && best[0].res.diff < double.PositiveInfinity)
                    {
                        // proveri nerešen rezultat (isti diff)
                        bool tie = best.Count >= 2 && Math.Abs(best[0].res.diff - best[1].res.diff) < 1e-9;

                        if (!tie)
                        {
                            best[0].c.Player.Points += moj.PointsForWin;
                            _mojBrojWinnerAwarded = true;
                            await BroadcastAsync(new Message
                            {
                                Type = "INFO",
                                Data = $"Niko nije pogodio tačno. Najbliži je {best[0].c.Player.Name} (+{moj.PointsForWin})"
                            });
                        }
                        else
                        {
                            await BroadcastAsync(new Message
                            {
                                Type = "INFO",
                                Data = "Niko nije pogodio tačno. Rezultat je nerešen (isti razmak)."
                            });
                        }
                    }
                }

                _roundActive = false;
                _roundCts?.Cancel();
            }

            return true;
        }

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
