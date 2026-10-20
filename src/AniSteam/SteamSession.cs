using System.Collections.Concurrent;
using SteamKit2;
using SteamKit2.Authentication;

namespace AniSteam;

public sealed class SteamSession : ISteamSession
{
    private readonly SteamClient client = new();
    private readonly CallbackManager callbacks;
    private readonly SteamUser user;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly PresencePublisher publisher = new();
    private readonly Task loop;
    private LoginToken? token = Storage.LoadToken();
    private CancellationTokenSource? authentication;
    private string desired = "";
    private long reconnectAt;
    private int backoff = 3;
    private bool connected;
    private bool loggedOn;
    private bool blocked;
    private bool disposed;
    public bool HasSavedLogin => token is not null;
    public string PublishedName => publisher.PublishedName;
    public event Action<string>? PublishedNameChanged {
        add => publisher.PublishedNameChanged += value;
        remove => publisher.PublishedNameChanged -= value;
    }
    public event Action<string, bool>? StatusChanged;
    public event Action<string?>? QrChanged;
    public event Action? LoginRequired;

    public SteamSession()
    {
        callbacks = new CallbackManager(client);
        user = client.GetHandler<SteamUser>()!;
        callbacks.Subscribe<SteamClient.ConnectedCallback>(_ => {
            connected = true;
            reconnectAt = 0;
            backoff = 3;
            if (token is not null) LogOn();
            else { Status("请用 Steam 手机 App 扫码登录"); LoginRequired?.Invoke(); }
        });
        callbacks.Subscribe<SteamClient.DisconnectedCallback>(_ => {
            connected = loggedOn = blocked = false;
            publisher.Reset();
            authentication?.Cancel();
            QrChanged?.Invoke(null);
            if (!lifetime.IsCancellationRequested) {
                reconnectAt = Environment.TickCount64 + backoff * 1000;
                backoff = Math.Min(backoff * 2, 60);
                Status("Steam 连接已断开，正在重连…");
            }
        });
        callbacks.Subscribe<SteamUser.LoggedOnCallback>(cb => {
            loggedOn = cb.Result == EResult.OK;
            publisher.Reset();
            if (loggedOn) {
                client.GetHandler<SteamFriends>()!.SetPersonaState(EPersonaState.Online);
                QrChanged?.Invoke(null);
                Status("Steam 已登录", true);
            } else {
                if (cb.Result is EResult.InvalidPassword or EResult.AccessDenied or EResult.Expired or EResult.Revoked) {
                    token = null;
                    Storage.DeleteToken();
                    LoginRequired?.Invoke();
                } else {
                    client.Disconnect();
                }
                Status("Steam 登录失败：" + cb.Result);
            }
        });
        callbacks.Subscribe<SteamUser.LoggedOffCallback>(cb => {
            loggedOn = false;
            publisher.Reset();
            Status("Steam 已登出：" + cb.Result);
            client.Disconnect();
        });
        callbacks.Subscribe<SteamUser.PlayingSessionStateCallback>(cb => {
            blocked = cb.PlayingBlocked;
            Status(blocked ? "Steam 正在运行其他游戏，番剧同步已让位" : "Steam 已登录", loggedOn);
        });
        loop = Task.Run(Run);
    }

    public void Start() => commands.Enqueue(() => { Status("正在连接 Steam…"); client.Connect(); });
    public void SetDesiredName(string name) => Volatile.Write(ref desired, name);

    public void BeginQrLogin() => commands.Enqueue(() => {
        if (!connected) { Status("正在连接 Steam，连接成功后可扫码"); return; }
        if (loggedOn) return;
        authentication?.Cancel();
        authentication = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        authentication.CancelAfter(TimeSpan.FromMinutes(3));
        _ = Authenticate(authentication.Token);
    });

    private async Task Authenticate(CancellationToken cancellation)
    {
        try {
            var session = await client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails {
                DeviceFriendlyName = "AniSteam (Animeko)", IsPersistentSession = true
            }).WaitAsync(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            session.ChallengeURLChanged = () => { if (!cancellation.IsCancellationRequested) QrChanged?.Invoke(session.ChallengeURL); };
            QrChanged?.Invoke(session.ChallengeURL);
            Status("使用 Steam 手机 App 扫码，并确认登录");
            var result = await session.PollingWaitForResultAsync(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            commands.Enqueue(() => {
                if (cancellation.IsCancellationRequested) return;
                token = new LoginToken(result.AccountName, result.RefreshToken);
                Storage.SaveToken(token);
                LogOn();
            });
        }
        catch (OperationCanceledException) {
            if (!lifetime.IsCancellationRequested && connected && authentication?.Token == cancellation) {
                QrChanged?.Invoke(null);
                Status("二维码已过期，请点击刷新登录二维码");
            }
        }
        catch (Exception ex) {
            if (!cancellation.IsCancellationRequested) {
                QrChanged?.Invoke(null);
                Status("扫码登录失败，请刷新二维码（" + ex.GetType().Name + "）");
            }
        }
    }

    public void ForgetLogin() => commands.Enqueue(() => {
        authentication?.Cancel();
        token = null;
        Storage.DeleteToken();
        if (loggedOn) { client.Send(PresencePublisher.CreateMessage("")); user.LogOff(); }
        loggedOn = false;
        publisher.Reset();
        QrChanged?.Invoke(null);
        client.Disconnect();
        Status("已退出登录；连接后可重新扫码");
    });

    private void LogOn()
    {
        if (token is null || !connected) return;
        Status("正在登录 Steam…");
        user.LogOn(new SteamUser.LogOnDetails {
            Username = token.AccountName, AccessToken = token.RefreshToken,
            LoginID = 0x414E4953, ShouldRememberPassword = true, MachineName = "AniSteam"
        });
    }

    private void Run()
    {
        try {
            while (!lifetime.IsCancellationRequested) {
                try {
                    while (commands.TryDequeue(out var action)) action();
                    callbacks.RunWaitCallbacks(TimeSpan.FromMilliseconds(200));
                    long now = Environment.TickCount64;
                    if (!connected && reconnectAt > 0 && now >= reconnectAt) { reconnectAt = 0; client.Connect(); }
                    if (loggedOn && client.IsConnected)
                        publisher.Publish(Volatile.Read(ref desired), now, blocked, message => client.Send(message));
                    else if (!client.IsConnected) publisher.Reset();
                }
                catch (Exception ex) {
                    Storage.Log("Steam loop: " + ex.GetType().Name);
                    publisher.Reset();
                    Status("Steam 通信失败，正在重连…");
                    client.Disconnect();
                    connected = loggedOn = false;
                    reconnectAt = Environment.TickCount64 + 5000;
                }
            }
        }
        finally {
            try {
                if (loggedOn) { client.Send(PresencePublisher.CreateMessage("")); user.LogOff(); }
            } catch (Exception ex) { Storage.Log("Steam shutdown: " + ex.GetType().Name); }
            finally { client.Disconnect(); }
        }
    }

    private void Status(string message, bool ready = false) { Storage.Log(message); StatusChanged?.Invoke(message, ready); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        authentication?.Cancel();
        lifetime.Cancel();
        if (loop.Wait(TimeSpan.FromSeconds(3))) { authentication?.Dispose(); lifetime.Dispose(); }
    }
}
