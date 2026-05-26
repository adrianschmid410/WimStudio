# WIM Studio

Ein WPF-Programm (.NET 8) zur Erstellung und Verwaltung benutzerdefinierter Windows Imaging Format (WIM) Dateien.

> **Hinweis zur Schreibweise:** Der Anzeigename ist *WIM Studio* (mit Leerzeichen). In Bezeichnern (Namespace, Assembly, Projektname, Dateipfade) wird `WimStudio` ohne Leerzeichen verwendet, weil das in .NET die übliche Konvention ist.

## Funktionen

- **Capture Image**: Neue WIM-Datei aus einem Verzeichnis erstellen
- **Append Image**: Weiteres Image an eine bestehende WIM anhängen
- **Get Image Info**: Alle Images in einer WIM-Datei auflisten
- **Delete Image**: Image-Index aus WIM entfernen
- **Export Image**: Einzelnes Image in neue WIM exportieren (effektive Größenreduzierung)
- **Mount/Unmount**: WIM-Image zur Offline-Bearbeitung einhängen
- Auswahl der Kompressionsstufe (None / Fast / Maximum / Recovery)
- Verifizierung und Bootable-Flag konfigurierbar
- Live-Ausgabe und Fortschrittsanzeige

## Voraussetzungen

- **Windows 10 oder Windows 11** (DISM ist Bestandteil von Windows)
- **.NET 8 SDK** zum Bauen
- **Administratorrechte** zur Laufzeit (im Manifest erzwungen)

## Projektmappenstruktur

```
WimStudio/
├── WimStudio.sln              # Solution-Datei
├── Directory.Build.props      # Solutionweite Eigenschaften
├── NuGet.config               # NuGet-Quellen
├── .editorconfig              # Code-Formatierung
├── .gitignore
├── build.cmd                  # Build-Skript
├── README.md
└── WimStudio/                 # Hauptprojekt
    ├── WimStudio.csproj
    ├── app.manifest           # UAC-Manifest
    ├── App.xaml / .xaml.cs
    ├── AssemblyInfo.cs
    ├── Models/
    │   ├── WimImageInfo.cs
    │   └── WimCompressionType.cs
    ├── Services/
    │   └── DismService.cs     # DISM-Kapselung
    ├── ViewModels/
    │   └── MainViewModel.cs
    ├── Views/
    │   ├── MainWindow.xaml
    │   └── MainWindow.xaml.cs
    └── Helpers/
        ├── AdminHelper.cs
        └── InverseBooleanConverter.cs
```

## Bauen und Starten

### Mit Visual Studio 2022

`WimStudio.sln` öffnen, F5 drücken. Visual Studio startet die UAC-Eingabeaufforderung dank Manifest automatisch.

### Mit dotnet CLI

```powershell
cd WimStudio
dotnet restore
dotnet build -c Release
dotnet run --project WimStudio\WimStudio.csproj -c Release
```

### Single-File-Veröffentlichung

```powershell
.\build.cmd
```

Das Skript erzeugt `publish\win-x64\WimStudio.exe` (≈ 150 KB, benötigt installiertes .NET 8 Runtime).

## Technische Details

Die Anwendung ruft die in Windows vorhandene `Dism.exe` aus `%SystemRoot%\System32` auf
und parst deren Ausgabe. Folgende DISM-Befehle werden verwendet:

| Aktion        | DISM-Befehl       |
|---------------|-------------------|
| Capture       | `/Capture-Image`  |
| Append        | `/Append-Image`   |
| Get Info      | `/Get-ImageInfo`  |
| Delete        | `/Delete-Image`   |
| Export        | `/Export-Image`   |
| Mount         | `/Mount-Image`    |
| Unmount       | `/Unmount-Image`  |

## Architektur

- **MVVM-Pattern** mit `CommunityToolkit.Mvvm` (Source Generators für `[ObservableProperty]` und `[RelayCommand]`)
- **DismService**: Kapselt alle `Dism.exe`-Aufrufe asynchron mit `Process` und liefert Fortschritt + Ausgabe per Event
- **MainViewModel**: UI-State, Befehle, Validierung
- **Views/MainWindow.xaml**: Reine deklarative Oberfläche mit DataBinding

## Hinweise zur WIM-Erstellung

1. Das **Quellverzeichnis** ist der Wurzelpfad der Inhalte (z.B. ein eingebundenes Windows-Image-Laufwerk wie `D:\`).
2. Mit der Option **Bootable** wird die WIM für WinPE/Recovery markiert — nur sinnvoll bei bootfähigen Quellen.
3. **Maximum**-Kompression ist deutlich langsamer als **Fast**, ergibt aber rund 30 % kleinere Dateien.
4. Nach `Delete-Image` bleibt die Dateigröße unverändert. Erst `Export-Image` in eine neue WIM gibt den Speicher frei.

## Sicherheit

WIM-Operationen schreiben in Systemverzeichnisse und benötigen Administratorrechte.
Die App fordert diese über das Manifest automatisch an. Wird sie ohne UAC-Erhöhung gestartet, erscheint eine Warnung im Fenster.

## Lizenz

Frei verwendbar — passe das nach deinen Bedürfnissen an.
