using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SlagalicaServer.Game;

namespace SlagalicaServer.Networking
{
    public class GameServer
    {
        private TcpListener _listener;
        private List<ClientHandler> _clients = new();
        public GameManager GameManager { get; }
        public GameServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            GameManager = new GameManager(this);
        }

        public async Task Start()
        {
            _listener.Start();
            _ = GameManager.RunAsync();
            Console.WriteLine("Server pokrenut i čeka igrače...");

            while (true)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Novi klijent povezan!");

                ClientHandler handler = new ClientHandler(tcpClient, this);
                _clients.Add(handler);

                // Pokrećemo obradu klijenta u posebnom thread-u (neblokirajuće)
                _ = handler.Handle();
            }
        }

        public List<ClientHandler> Clients => _clients;
    }
}
