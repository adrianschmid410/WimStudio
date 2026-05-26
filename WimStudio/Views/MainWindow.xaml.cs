using System.Windows;
using System.Windows.Controls;

namespace WimStudio.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sorgt dafür, dass das Log-Fenster automatisch nach unten scrollt,
    /// wenn neue Ausgaben hinzukommen.
    /// </summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.ScrollToEnd();
    }
}
