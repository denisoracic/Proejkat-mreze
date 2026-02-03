using SlagalicaServer.Networking;

namespace SlagalicaServer
{
    public class Program
    {
        static async Task Main()
        {
            GameServer server = new GameServer(5000);
            await server.Start();
        }
    }
}
