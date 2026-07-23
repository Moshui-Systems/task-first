using System.Windows;
using TaskFirst.Security;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TaskFirst.UI;

public partial class PasswordDialog : Window
{
    public string Password => Pwd.Password;

    private PasswordDialog(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        Loaded += (_, _) => Pwd.Focus();
    }

    /// <summary>Prompt for a new password (with confirmation handled by the caller). Returns null if cancelled.</summary>
    public static string? AskNew(string prompt = "Set a tamper-lock password:")
    {
        var dlg = new PasswordDialog(prompt);
        return dlg.ShowDialog() == true && dlg.Password.Length > 0 ? dlg.Password : null;
    }

    /// <summary>
    /// Prompt until the entered password matches <paramref name="storedHash"/> or the user cancels.
    /// Returns true if verified.
    /// </summary>
    public static bool Challenge(string storedHash, string prompt = "Enter your tamper-lock password to continue:")
    {
        string current = prompt;
        while (true)
        {
            var dlg = new PasswordDialog(current);
            if (dlg.ShowDialog() != true) return false;
            if (PasswordHasher.Verify(dlg.Password, storedHash)) return true;
            current = "Incorrect password. Try again:";
        }
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
        Pwd.Clear();
        Pwd.Focus();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOk(sender, e);
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
