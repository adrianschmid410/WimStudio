namespace WimStudio.Models;

/// <summary>
/// Repräsentiert Metadaten eines WIM-Images.
/// </summary>
public class WimImageInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public long Size { get; set; }
    public string DisplayName => $"[{Index}] {Name}";
}
