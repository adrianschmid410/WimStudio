using System.Security.Principal;

namespace WimStudio.Helpers;

/// <summary>
/// Hilfsmethoden zum Überprüfen von Privilegien und Umgebung.
/// </summary>
public static class AdminHelper
{
    /// <summary>
    /// Prüft, ob der aktuelle Prozess mit Administratorrechten läuft.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
