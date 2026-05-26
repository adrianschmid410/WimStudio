using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WimStudio.Models;

namespace WimStudio.Services;

/// <summary>
/// Kapselt Aufrufe von DISM.exe für WIM-Operationen.
/// DISM ist Teil von Windows und benötigt Administratorrechte.
/// </summary>
public class DismService
{
    private readonly string _dismPath;

    public DismService()
    {
        // DISM.exe ist standardmäßig im System32-Verzeichnis vorhanden
        _dismPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "Dism.exe");
    }

    public event Action<string>? OutputReceived;
    public event Action<int>? ProgressChanged;

    /// <summary>
    /// Erstellt eine neue WIM-Datei aus einem Quellverzeichnis (capture-image).
    /// </summary>
    public async Task<bool> CaptureImageAsync(
        string sourceDirectory,
        string wimFilePath,
        string imageName,
        string imageDescription,
        WimCompressionType compression,
        bool verify,
        bool bootable,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Quellverzeichnis nicht gefunden: {sourceDirectory}");

        var args = new StringBuilder();
        args.Append("/Capture-Image ");
        args.Append($"/ImageFile:\"{wimFilePath}\" ");
        args.Append($"/CaptureDir:\"{sourceDirectory}\" ");
        args.Append($"/Name:\"{imageName}\" ");

        if (!string.IsNullOrWhiteSpace(imageDescription))
            args.Append($"/Description:\"{imageDescription}\" ");

        args.Append($"/Compress:{compression.ToDismArgument()} ");

        if (verify) args.Append("/Verify ");
        if (bootable) args.Append("/Bootable ");

        return await RunDismAsync(args.ToString(), cancellationToken);
    }

    /// <summary>
    /// Fügt einer bestehenden WIM-Datei ein zusätzliches Image hinzu (append-image).
    /// </summary>
    public async Task<bool> AppendImageAsync(
        string sourceDirectory,
        string wimFilePath,
        string imageName,
        string imageDescription,
        bool verify,
        CancellationToken cancellationToken = default)
    {
        var args = new StringBuilder();
        args.Append("/Append-Image ");
        args.Append($"/ImageFile:\"{wimFilePath}\" ");
        args.Append($"/CaptureDir:\"{sourceDirectory}\" ");
        args.Append($"/Name:\"{imageName}\" ");

        if (!string.IsNullOrWhiteSpace(imageDescription))
            args.Append($"/Description:\"{imageDescription}\" ");

        if (verify) args.Append("/Verify ");

        return await RunDismAsync(args.ToString(), cancellationToken);
    }

    /// <summary>
    /// Liest die Image-Informationen aus einer WIM-Datei aus.
    /// </summary>
    public async Task<List<WimImageInfo>> GetImageInfoAsync(
        string wimFilePath,
        CancellationToken cancellationToken = default)
    {
        var args = $"/Get-ImageInfo /ImageFile:\"{wimFilePath}\"";
        var output = new StringBuilder();

        var handler = new Action<string>(line => output.AppendLine(line));
        OutputReceived += handler;
        try
        {
            await RunDismAsync(args, cancellationToken);
        }
        finally
        {
            OutputReceived -= handler;
        }

        return ParseImageInfo(output.ToString());
    }

    /// <summary>
    /// Exportiert ein einzelnes Image aus einer Quell-WIM in eine neue WIM-Datei.
    /// </summary>
    public async Task<bool> ExportImageAsync(
        string sourceWim,
        int sourceIndex,
        string destinationWim,
        WimCompressionType compression,
        CancellationToken cancellationToken = default)
    {
        var args = new StringBuilder();
        args.Append("/Export-Image ");
        args.Append($"/SourceImageFile:\"{sourceWim}\" ");
        args.Append($"/SourceIndex:{sourceIndex} ");
        args.Append($"/DestinationImageFile:\"{destinationWim}\" ");
        args.Append($"/Compress:{compression.ToDismArgument()} ");

        return await RunDismAsync(args.ToString(), cancellationToken);
    }

    /// <summary>
    /// Löscht ein Image aus einer WIM-Datei anhand des Index.
    /// </summary>
    public async Task<bool> DeleteImageAsync(
        string wimFilePath,
        int index,
        CancellationToken cancellationToken = default)
    {
        var args = $"/Delete-Image /ImageFile:\"{wimFilePath}\" /Index:{index}";
        return await RunDismAsync(args, cancellationToken);
    }

    /// <summary>
    /// Mountet ein WIM-Image zur Bearbeitung.
    /// </summary>
    public async Task<bool> MountImageAsync(
        string wimFilePath,
        int index,
        string mountDir,
        bool readOnly,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(mountDir);

        var args = new StringBuilder();
        args.Append("/Mount-Image ");
        args.Append($"/ImageFile:\"{wimFilePath}\" ");
        args.Append($"/Index:{index} ");
        args.Append($"/MountDir:\"{mountDir}\" ");
        if (readOnly) args.Append("/ReadOnly ");

        return await RunDismAsync(args.ToString(), cancellationToken);
    }

    /// <summary>
    /// Hängt ein gemountetes Image aus, optional mit Übernahme der Änderungen.
    /// </summary>
    public async Task<bool> UnmountImageAsync(
        string mountDir,
        bool commit,
        CancellationToken cancellationToken = default)
    {
        var args = $"/Unmount-Image /MountDir:\"{mountDir}\" {(commit ? "/Commit" : "/Discard")}";
        return await RunDismAsync(args, cancellationToken);
    }

    /// <summary>
    /// Führt eine DISM-Aktion mit den angegebenen Argumenten asynchron aus.
    /// </summary>
    private async Task<bool> RunDismAsync(string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _dismPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Regex zum Erfassen der Fortschrittsanzeige von DISM (z.B. "[==========         50.0%        ]")
        var progressRegex = new Regex(@"(\d+(?:[\.,]\d+)?)\s*%");

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            OutputReceived?.Invoke(e.Data);

            var match = progressRegex.Match(e.Data);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var pct))
            {
                ProgressChanged?.Invoke((int)Math.Round(pct));
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                OutputReceived?.Invoke($"FEHLER: {e.Data}");
        };

        OutputReceived?.Invoke($"> dism.exe {arguments}");

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* ignored */ }
            OutputReceived?.Invoke("Vorgang abgebrochen.");
            return false;
        }

        return process.ExitCode == 0;
    }

    /// <summary>
    /// Parst die DISM /Get-ImageInfo-Ausgabe in strukturierte Objekte.
    /// </summary>
    private static List<WimImageInfo> ParseImageInfo(string output)
    {
        var result = new List<WimImageInfo>();
        WimImageInfo? current = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Sowohl deutsche als auch englische Schlüsselwörter unterstützen
            if (line.StartsWith("Index :", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Index:", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null) result.Add(current);
                current = new WimImageInfo();
                var value = ExtractValue(line);
                if (int.TryParse(value, out var idx)) current.Index = idx;
            }
            else if (current != null)
            {
                if (line.StartsWith("Name :", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                    current.Name = ExtractValue(line);
                else if (line.StartsWith("Description :", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Beschreibung :", StringComparison.OrdinalIgnoreCase))
                    current.Description = ExtractValue(line);
                else if (line.StartsWith("Size :", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Größe :", StringComparison.OrdinalIgnoreCase))
                {
                    var sizeStr = new string(ExtractValue(line).Where(c => char.IsDigit(c)).ToArray());
                    if (long.TryParse(sizeStr, out var size)) current.Size = size;
                }
                else if (line.StartsWith("Architecture", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Architektur", StringComparison.OrdinalIgnoreCase))
                    current.Architecture = ExtractValue(line);
            }
        }

        if (current != null) result.Add(current);
        return result;
    }

    private static string ExtractValue(string line)
    {
        var idx = line.IndexOf(':');
        return idx < 0 ? string.Empty : line[(idx + 1)..].Trim();
    }
}
