using SlagalicaClient.Networking;

public class Program
{
    static async Task Main()
    {
        GameClient client = new GameClient();
        await client.Start();
    }
}
