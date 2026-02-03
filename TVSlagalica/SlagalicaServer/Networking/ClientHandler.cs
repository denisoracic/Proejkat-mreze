using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using Shared;
using SlagalicaServer.Game;

namespace SlagalicaServer.Networking
{
    public class ClientHandler
    {
        private TcpClient _client;
        private GameServer _server;

        public Player Player { get; set; } = new Player();

        public ClientHandler(TcpClient client, GameServer server)
        {
            _client = client;
            _server = server;
        }

        public async Task Handle()
        {
            NetworkStream stream = _client.GetStream();
            byte[] buffer = new byte[4096];

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Message msg = Message.Deserialize(json);

                await ProcessMessage(msg);
            }
        }

        private async Task ProcessMessage(Message msg)
        {
            switch (msg.Type)
            {
                case "REGISTER":
                    Player.Name = msg.Data;
                    Console.WriteLine($"Igrač povezan: {Player.Name}");
                    break;

                case "ANSWER":
                    await _server.GameManager.TryAnswerAsync(this, msg.Data);
                    break;
            }
        }

        public async Task Send(Message msg)
        {
            NetworkStream stream = _client.GetStream();
            string payload = Message.Serialize(msg) + "\n"; // delimiter
            byte[] data = Encoding.UTF8.GetBytes(payload);
            await stream.WriteAsync(data);
        }

    }
}
