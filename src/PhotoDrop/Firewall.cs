using System.ComponentModel;
using System.Diagnostics;

namespace PhotoDrop;

static class Firewall
{
    /// <summary>
    /// Clears any rule Windows already holds for this exe and adds an allow rule for private
    /// networks. Needed only when someone dismissed the firewall prompt, which leaves a block
    /// rule behind and stops Windows ever asking again. Needs elevation, so it prompts for it.
    /// </summary>
    /// <returns>true if the rule was written, false if the user declined or it failed.</returns>
    public static bool Allow()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        var command =
            $"/c netsh advfirewall firewall delete rule name=all program=\"{exe}\" >nul 2>&1 "
            + $"& netsh advfirewall firewall add rule name=\"{Program.AppName}\" dir=in "
            + $"action=allow program=\"{exe}\" enable=yes profile=private,domain";

        try
        {
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", command)
            {
                UseShellExecute = true,          // required for the elevation verb
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is null) return false;

            process.WaitForExit(15000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;    // user said no to the UAC prompt
        }
        catch
        {
            return false;
        }
    }
}
