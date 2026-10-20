using SteamKit2;
using SteamKit2.Internal;

namespace AniSteam;

public sealed class PresencePublisher
{
    private string? sent;
    private long lastSent;
    public string PublishedName { get; private set; } = "";
    public event Action<string>? PublishedNameChanged;

    public void Reset()
    {
        sent = null;
        lastSent = long.MinValue;
        SetPublishedName("");
    }

    public void Publish(string desired, long now, bool blocked, Action<ClientMsgProtobuf<CMsgClientGamesPlayed>> send)
    {
        if (Next(desired, now, blocked) is not { } name) return;
        send(CreateMessage(name));
        SetPublishedName(name);
    }

    private void SetPublishedName(string name)
    {
        if (PublishedName == name) return;
        PublishedName = name;
        PublishedNameChanged?.Invoke(name);
    }
    public string? Next(string desired, long now, bool blocked)
    {
        string value = blocked ? "" : desired;
        if (sent == value) return null;
        if (value.Length > 0 && sent is { Length: > 0 } && now - lastSent < 500) return null;
        sent = value;
        lastSent = now;
        return value;
    }

    public static ClientMsgProtobuf<CMsgClientGamesPlayed> CreateMessage(string name)
    {
        var message = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayedWithDataBlob);
        message.Body.client_os_type = unchecked((uint)EOSType.Windows10);
        if (name.Length > 0)
            message.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed {
                game_extra_info = name,
                game_id = new GameID { AppType = GameID.GameType.Shortcut, ModID = uint.MaxValue }
            });
        return message;
    }
}
