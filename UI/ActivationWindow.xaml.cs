using System.Diagnostics;
using System.Windows;
using TaskFirst.Licensing;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace TaskFirst.UI;

public partial class ActivationWindow : Window
{
    private readonly LicenseService _license = App.Instance.License;

    public ActivationWindow()
    {
        InitializeComponent();
        DeviceText.Text = $"Device ID: {TaskFirst.Security.MachineId.Current}"
            + (Entitlements.ApiConfigured ? "  ·  online activation enabled" : "  ·  offline mode");
        RenderStatus();
    }

    private void RenderStatus()
    {
        var (text, color) = _license.State switch
        {
            LicenseState.Pro => ($"PRO — {_license.Payload?.Email}", Color.FromRgb(0x3F, 0xD0, 0x7A)),
            LicenseState.Expired => ("License expired", Color.FromRgb(0xFF, 0x6B, 0x6B)),
            LicenseState.Invalid => ("Invalid key", Color.FromRgb(0xFF, 0x6B, 0x6B)),
            _ => ("Free tier", Color.FromRgb(0x9A, 0xA0, 0xB4)),
        };
        StatusText.Text = text;
        StatusText.Foreground = new SolidColorBrush(color);
        DeactivateBtn.Visibility = _license.IsPro ? Visibility.Visible : Visibility.Collapsed;
        if (_license.State != LicenseState.Free)
            Message.Text = _license.LastMessage;
    }

    private async void OnActivate(object sender, RoutedEventArgs e)
    {
        var btn = (System.Windows.Controls.Button)sender;
        btn.IsEnabled = false;
        Message.Text = "Activating…";
        Message.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xB4));
        try
        {
            var (ok, message) = await _license.ActivateAsync(KeyBox.Text);
            Message.Text = message;
            Message.Foreground = new SolidColorBrush(ok
                ? Color.FromRgb(0x3F, 0xD0, 0x7A)
                : Color.FromRgb(0xFF, 0x6B, 0x6B));
            RenderStatus();
            App.Instance.OnLicenseChanged();
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Remove this license from this device?", "TaskFirst",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _license.Deactivate();
        KeyBox.Clear();
        Message.Text = "License removed.";
        RenderStatus();
        App.Instance.OnLicenseChanged();
    }

    private void OnBuy(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Entitlements.BuyUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Message.Text = "Couldn't open browser: " + ex.Message;
        }
    }
}
