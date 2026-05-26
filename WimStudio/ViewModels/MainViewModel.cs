using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WimStudio.Helpers;
using WimStudio.Models;
using WimStudio.Services;

namespace WimStudio.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DismService _dism;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        _dism = new DismService();
        _dism.OutputReceived += OnDismOutput;
        _dism.ProgressChanged += OnDismProgress;

        CompressionTypes = new ObservableCollection<WimCompressionType>(
            Enum.GetValues<WimCompressionType>());

        Images = new ObservableCollection<WimImageInfo>();

        IsAdmin = AdminHelper.IsRunningAsAdministrator();
        if (!IsAdmin)
        {
            AppendLog("WARNUNG: Anwendung läuft NICHT mit Administratorrechten.");
            AppendLog("DISM-Operationen werden voraussichtlich fehlschlagen.");
        }
        else
        {
            AppendLog("WIM Studio mit Administratorrechten gestartet. Bereit.");
        }
    }

    #region Eigenschaften

    public ObservableCollection<WimCompressionType> CompressionTypes { get; }
    public ObservableCollection<WimImageInfo> Images { get; }

    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _sourceDirectory = string.Empty;
    [ObservableProperty] private string _wimFilePath = string.Empty;
    [ObservableProperty] private string _imageName = "Custom Windows Image";
    [ObservableProperty] private string _imageDescription = string.Empty;
    [ObservableProperty] private WimCompressionType _selectedCompression = WimCompressionType.Maximum;
    [ObservableProperty] private bool _verify = true;
    [ObservableProperty] private bool _bootable;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _statusText = "Bereit";
    [ObservableProperty] private string _logOutput = string.Empty;
    [ObservableProperty] private WimImageInfo? _selectedImage;
    [ObservableProperty] private string _mountDirectory = string.Empty;

    #endregion

    #region Befehle

    [RelayCommand]
    private void BrowseSourceDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Quellverzeichnis für die WIM-Erfassung wählen"
        };

        if (dialog.ShowDialog() == true)
            SourceDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseWimSave()
    {
        var dialog = new SaveFileDialog
        {
            Title = "WIM-Datei speichern unter",
            Filter = "Windows Imaging Format (*.wim)|*.wim|Alle Dateien (*.*)|*.*",
            DefaultExt = "wim",
            FileName = "custom.wim"
        };

        if (dialog.ShowDialog() == true)
            WimFilePath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseWimOpen()
    {
        var dialog = new OpenFileDialog
        {
            Title = "WIM-Datei öffnen",
            Filter = "Windows Imaging Format (*.wim)|*.wim|Alle Dateien (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            WimFilePath = dialog.FileName;
            _ = LoadImageInfoAsync();
        }
    }

    [RelayCommand]
    private void BrowseMountDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Mount-Verzeichnis wählen (sollte leer sein)"
        };

        if (dialog.ShowDialog() == true)
            MountDirectory = dialog.FolderName;
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task CaptureImageAsync()
    {
        if (!ValidateCapture()) return;

        if (File.Exists(WimFilePath))
        {
            var result = MessageBox.Show(
                $"Die Datei '{WimFilePath}' existiert bereits.\n\n" +
                "Soll das neue Image an die bestehende WIM angehängt werden?\n" +
                "(Nein = abbrechen, damit die Datei nicht überschrieben wird)",
                "Datei existiert bereits",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await AppendImageAsync();
                return;
            }
            return;
        }

        await ExecuteAsync("WIM-Erfassung", async ct =>
        {
            var success = await _dism.CaptureImageAsync(
                SourceDirectory, WimFilePath, ImageName, ImageDescription,
                SelectedCompression, Verify, Bootable, ct);

            if (success)
            {
                AppendLog($"\n✓ WIM-Datei erfolgreich erstellt: {WimFilePath}");
                await LoadImageInfoAsync();
            }
            else
            {
                AppendLog("\n✗ WIM-Erstellung fehlgeschlagen.");
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task AppendImageAsync()
    {
        if (!File.Exists(WimFilePath))
        {
            MessageBox.Show("Die WIM-Datei existiert nicht. Bitte zuerst auswählen oder neu erstellen.",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!ValidateCapture(checkWimExists: false)) return;

        await ExecuteAsync("Image anhängen", async ct =>
        {
            var success = await _dism.AppendImageAsync(
                SourceDirectory, WimFilePath, ImageName, ImageDescription, Verify, ct);

            if (success)
            {
                AppendLog($"\n✓ Image '{ImageName}' erfolgreich angehängt.");
                await LoadImageInfoAsync();
            }
            else
            {
                AppendLog("\n✗ Anhängen fehlgeschlagen.");
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task LoadImageInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(WimFilePath) || !File.Exists(WimFilePath))
            return;

        await ExecuteAsync("Image-Informationen laden", async ct =>
        {
            var images = await _dism.GetImageInfoAsync(WimFilePath, ct);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Images.Clear();
                foreach (var img in images) Images.Add(img);
                AppendLog($"\n{images.Count} Image(s) gefunden.");
            });
        });
    }

    [RelayCommand(CanExecute = nameof(CanDeleteImage))]
    private async Task DeleteSelectedImageAsync()
    {
        if (SelectedImage == null) return;

        var confirm = MessageBox.Show(
            $"Soll Image [{SelectedImage.Index}] '{SelectedImage.Name}' wirklich gelöscht werden?\n\n" +
            "Hinweis: DISM markiert das Image nur. Die Datei wird erst durch /Export-Image verkleinert.",
            "Image löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var index = SelectedImage.Index;
        await ExecuteAsync("Image löschen", async ct =>
        {
            var success = await _dism.DeleteImageAsync(WimFilePath, index, ct);
            AppendLog(success ? "\n✓ Image gelöscht." : "\n✗ Löschen fehlgeschlagen.");
            if (success) await LoadImageInfoAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanMountImage))]
    private async Task MountImageAsync()
    {
        if (SelectedImage == null) return;

        if (string.IsNullOrWhiteSpace(MountDirectory))
        {
            MessageBox.Show("Bitte zuerst ein Mount-Verzeichnis wählen.",
                "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var index = SelectedImage.Index;
        await ExecuteAsync("Image mounten", async ct =>
        {
            var success = await _dism.MountImageAsync(WimFilePath, index, MountDirectory, false, ct);
            AppendLog(success
                ? $"\n✓ Image gemountet unter: {MountDirectory}"
                : "\n✗ Mounten fehlgeschlagen.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanUnmountImage))]
    private async Task UnmountImageAsync(string? commitParameter)
    {
        var commit = commitParameter == "commit";

        if (commit)
        {
            var confirm = MessageBox.Show(
                "Änderungen im gemounteten Image dauerhaft übernehmen?\nDieser Vorgang kann mehrere Minuten dauern.",
                "Änderungen committen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;
        }

        await ExecuteAsync(commit ? "Unmount (commit)" : "Unmount (discard)", async ct =>
        {
            var success = await _dism.UnmountImageAsync(MountDirectory, commit, ct);
            AppendLog(success ? "\n✓ Image ausgehängt." : "\n✗ Unmount fehlgeschlagen.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunOperation))]
    private async Task ExportImageAsync()
    {
        if (SelectedImage == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Image exportieren als",
            Filter = "WIM-Datei (*.wim)|*.wim",
            DefaultExt = "wim",
            FileName = $"export_{SelectedImage.Index}.wim"
        };

        if (dialog.ShowDialog() != true) return;

        var sourceIndex = SelectedImage.Index;
        var destination = dialog.FileName;

        await ExecuteAsync("Image exportieren", async ct =>
        {
            var success = await _dism.ExportImageAsync(
                WimFilePath, sourceIndex, destination, SelectedCompression, ct);

            AppendLog(success
                ? $"\n✓ Image exportiert nach: {destination}"
                : "\n✗ Export fehlgeschlagen.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
        AppendLog("\nAbbruch angefordert...");
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogOutput = string.Empty;
    }

    #endregion

    #region CanExecute-Logik

    private bool CanRunOperation() => !IsBusy;
    private bool CanCancel() => IsBusy;
    private bool CanDeleteImage() => !IsBusy && SelectedImage != null && !string.IsNullOrEmpty(WimFilePath);
    private bool CanMountImage() => !IsBusy && SelectedImage != null && !string.IsNullOrWhiteSpace(MountDirectory);
    private bool CanUnmountImage() => !IsBusy && !string.IsNullOrWhiteSpace(MountDirectory);

    partial void OnIsBusyChanged(bool value)
    {
        // Befehlsverfügbarkeit aktualisieren
        CaptureImageCommand.NotifyCanExecuteChanged();
        AppendImageCommand.NotifyCanExecuteChanged();
        LoadImageInfoCommand.NotifyCanExecuteChanged();
        DeleteSelectedImageCommand.NotifyCanExecuteChanged();
        MountImageCommand.NotifyCanExecuteChanged();
        UnmountImageCommand.NotifyCanExecuteChanged();
        ExportImageCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedImageChanged(WimImageInfo? value)
    {
        DeleteSelectedImageCommand.NotifyCanExecuteChanged();
        MountImageCommand.NotifyCanExecuteChanged();
        ExportImageCommand.NotifyCanExecuteChanged();
    }

    partial void OnMountDirectoryChanged(string value)
    {
        MountImageCommand.NotifyCanExecuteChanged();
        UnmountImageCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region Hilfsmethoden

    private bool ValidateCapture(bool checkWimExists = true)
    {
        if (string.IsNullOrWhiteSpace(SourceDirectory) || !Directory.Exists(SourceDirectory))
        {
            MessageBox.Show("Bitte ein gültiges Quellverzeichnis wählen.",
                "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(WimFilePath))
        {
            MessageBox.Show("Bitte einen Zielpfad für die WIM-Datei wählen.",
                "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(ImageName))
        {
            MessageBox.Show("Bitte einen Image-Namen angeben.",
                "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private async Task ExecuteAsync(string operationName, Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;
        StatusText = $"{operationName} läuft...";
        AppendLog($"\n=== {operationName} gestartet ===");

        try
        {
            await action(_cts.Token);
            StatusText = $"{operationName} abgeschlossen.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"{operationName} abgebrochen.";
            AppendLog("Vorgang abgebrochen.");
        }
        catch (Exception ex)
        {
            StatusText = "Fehler aufgetreten.";
            AppendLog($"\nAUSNAHME: {ex.Message}");
            MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnDismOutput(string line)
    {
        Application.Current.Dispatcher.Invoke(() => AppendLog(line));
    }

    private void OnDismProgress(int percent)
    {
        Application.Current.Dispatcher.Invoke(() => Progress = percent);
    }

    private void AppendLog(string text)
    {
        LogOutput += text + Environment.NewLine;
    }

    #endregion
}
