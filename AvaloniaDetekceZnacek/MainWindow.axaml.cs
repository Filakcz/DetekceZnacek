using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AvaloniaDetekceZnacek;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void OpenButton_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Vyber obrázek",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Obrázky") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
            }
        });

        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        using var bitmap = new Bitmap(stream);
        ResultImage.Source = ImageProcessing.DetectSigns(bitmap);
    }
}
