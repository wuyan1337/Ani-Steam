using System.Diagnostics;
using System.Text;

namespace AniSteam;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length != 2 || args[0] != "--parent-pid" || !int.TryParse(args[1], out int pid) || pid <= 0)
        {
            MessageBox.Show("请将补丁解压到 Ani.exe 所在目录，然后正常启动 Ani.exe。\n同步助手会随 Animeko 自动启动。", "AniSteam");
            return;
        }
        using var singleton = new Mutex(true, @"Local\AniSteam.Helper", out bool ownsMutex);
        if (!ownsMutex) return;
        try
        {
            using var parent = Process.GetProcessById(pid);
            if (parent.HasExited) return;
            using var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true));
            using var context = new AniSteamContext(parent, reader);
            context.Start();
            Application.Run(context);
        }
        catch (ArgumentException) { }
        catch (Exception ex)
        {
            Storage.Log("Helper failure: " + ex.GetType().Name);
        }
        finally { singleton.ReleaseMutex(); }
    }
}
