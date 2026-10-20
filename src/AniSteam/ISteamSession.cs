namespace AniSteam;

public interface ISteamSession : IDisposable
{
    bool HasSavedLogin { get; }
    string PublishedName { get; }
    event Action<string>? PublishedNameChanged;
    event Action<string, bool>? StatusChanged;
    event Action<string?>? QrChanged;
    event Action? LoginRequired;
    void Start();
    void SetDesiredName(string name);
    void BeginQrLogin();
    void ForgetLogin();
}
