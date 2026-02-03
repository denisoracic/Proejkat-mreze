using System.Text.Json;

namespace Shared
{
    public class Message
    {
        public string Type { get; set; }
        public string Data { get; set; }

        public static string Serialize(Message msg)
            => JsonSerializer.Serialize(msg);

        public static Message Deserialize(string json)
            => JsonSerializer.Deserialize<Message>(json);
    }
}
