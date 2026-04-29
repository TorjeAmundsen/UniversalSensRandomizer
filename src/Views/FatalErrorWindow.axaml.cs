using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace UniversalSensRandomizer.Views;

public partial class FatalErrorWindow : Window
{
    public FatalErrorWindow()
    {
        InitializeComponent();
    }

    public FatalErrorWindow(string message) : this()
    {
        TextBox? messageBox = this.FindControl<TextBox>("MessageBox");
        if (messageBox is { })
        {
            messageBox.Text = message;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
