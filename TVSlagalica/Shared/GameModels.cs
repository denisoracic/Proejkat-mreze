namespace Shared;

public enum GameType
{
    MojBroj,
    Skocko,
    KoZnaZna
}

public class RoundStart
{
    public GameType Game { get; set; }
    public int DurationSeconds { get; set; }
    public string Prompt { get; set; } = "";
}

public class RoundEnd
{
    public GameType Game { get; set; }
    public string CorrectAnswer { get; set; } = "";
    public string Info { get; set; } = "";
}
