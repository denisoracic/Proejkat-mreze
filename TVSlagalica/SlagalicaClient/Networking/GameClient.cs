using System.Net.Sockets;
using System.Text;
using Shared;
using System.Text.Json;

namespace SlagalicaClient.Networking;

public class GameClient
{
    private TcpClient _client = new();

    public async Task Start()
    {
        await _client.ConnectAsync("localhost", 5000);
        Console.WriteLine("Povezan na server.");

        Console.Write("Unesi ime: ");
        string name = Console.ReadLine() ?? "Igrac";

        await Send(new Message { Type = "REGISTER", Data = name });

        _ = Receive();

        while (true)
        {
            string input = Console.ReadLine() ?? "";
            await Send(new Message { Type = "ANSWER", Data = input });
        }
    }

    private async Task Send(Message msg)
    {
        var stream = _client.GetStream();
        string payload = Message.Serialize(msg) + "\n"; // delimiter
        byte[] data = Encoding.UTF8.GetBytes(payload);
        await stream.WriteAsync(data);
    }


    private async Task Receive()
    {
        NetworkStream stream = _client.GetStream();
        byte[] buffer = new byte[4096];
        var sb = new StringBuilder();

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0) break;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            // Obradi sve kompletne poruke (do '\n')
            while (true)
            {
                string all = sb.ToString();
                int idx = all.IndexOf('\n');
                if (idx < 0) break;

                string oneJson = all.Substring(0, idx).Trim();
                sb.Remove(0, idx + 1);

                if (string.IsNullOrWhiteSpace(oneJson)) continue;

                Message msg;
                try
                {
                    msg = Message.Deserialize(oneJson);
                }
                catch
                {
                    Console.WriteLine("Ne mogu da parsiram poruku: " + oneJson);
                    continue;
                }

                // OVDE ostaje tvoja if/else logika za ROUND_START/END/SCORES...
                if (msg.Type == "ROUND_START")
                {
                    var rs = System.Text.Json.JsonSerializer.Deserialize<Shared.RoundStart>(msg.Data);
                    Console.WriteLine("\n=== NOVA RUNDA ===");
                    Console.WriteLine($"Igra: {rs.Game}");
                    Console.WriteLine($"Trajanje: {rs.DurationSeconds}s");
                    Console.WriteLine(rs.Prompt);
                    Console.WriteLine("==================\n");
                }
                else if (msg.Type == "ROUND_END")
                {
                    var re = System.Text.Json.JsonSerializer.Deserialize<Shared.RoundEnd>(msg.Data);
                    Console.WriteLine($"\n--- KRAJ RUNDE ({re.Game}) ---");
                    Console.WriteLine($"Tačno: {re.CorrectAnswer}");
                    Console.WriteLine("----------------------------\n");
                }
                else if (msg.Type == "SCORES")
                {
                    Console.WriteLine("\n" + msg.Data + "\n");
                }
                else if (msg.Type == "INFO" || msg.Type == "RESULT")
                {
                    Console.WriteLine(msg.Type + ": " + msg.Data);
                }
                else
                {
                    Console.WriteLine(msg.Data);
                }
            }
        }
    }

}
