using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AniSteam;

public sealed record LoginToken(string AccountName, string RefreshToken);

public static class Storage
{
    public static string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniSteam");
    private static string TokenPath => Path.Combine(DirectoryPath, "steam-session.dat");
    private static string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    private static readonly object LogLock = new();

    public static void SaveToken(LoginToken token)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(token);
        try { AtomicWrite(TokenPath, ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static LoginToken? LoadToken()
    {
        if (!File.Exists(TokenPath)) return null;
        try
        {
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(TokenPath), null, DataProtectionScope.CurrentUser);
            try { return JsonSerializer.Deserialize<LoginToken>(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or JsonException)
        { Log("无法读取 Steam 登录凭据：" + ex.GetType().Name); return null; }
    }

    public static void DeleteToken() { if (File.Exists(TokenPath)) File.Delete(TokenPath); }
    public static Settings LoadSettings()
    {
        try { return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new()).Normalize(); }
        catch (Exception ex) when (ex is IOException or JsonException) { return new(); }
    }
    public static void SaveSettings(Settings settings) => AtomicWrite(SettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, Snapshot.JsonOptions));

    private static void AtomicWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, true);
    }

    public static void Log(string message)
    {
        lock (LogLock)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, "helper.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".1", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
