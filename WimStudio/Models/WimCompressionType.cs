namespace WimStudio.Models;

/// <summary>
/// Unterstützte WIM-Kompressionsarten gemäß DISM-Spezifikation.
/// </summary>
public enum WimCompressionType
{
    /// <summary>Keine Kompression - schnellste Erstellung, größte Datei</summary>
    None,

    /// <summary>Schnelle Kompression (XPRESS) - guter Kompromiss</summary>
    Fast,

    /// <summary>Maximale Kompression (LZX) - kleinste Datei, langsamste Erstellung</summary>
    Maximum,

    /// <summary>Wiederherstellungskompression (LZMS) - nur für /capture-image verfügbar</summary>
    Recovery
}

public static class WimCompressionTypeExtensions
{
    /// <summary>Liefert den DISM-Kommandozeilenwert für den Kompressionstyp.</summary>
    public static string ToDismArgument(this WimCompressionType type) => type switch
    {
        WimCompressionType.None => "none",
        WimCompressionType.Fast => "fast",
        WimCompressionType.Maximum => "max",
        WimCompressionType.Recovery => "recovery",
        _ => "fast"
    };
}
