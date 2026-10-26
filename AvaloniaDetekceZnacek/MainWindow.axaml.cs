using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;

namespace AvaloniaDetekceZnacek;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private ImageProcessing.DetectionStages? _stages;

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


        _stages = ImageProcessing.Process(bitmap);
        StageSlider.Value = 0;
        StageSlider.IsEnabled = true;
        ShowStage(0);
    }

    private void ShowStage(int index)
    {
        if (_stages is null)
        {
            return;
        }

        if (index == 0)
        {
            ResultImage.Source = _stages.Original;
            StageLabel.Text = "Puvodni";
        }
        else if (index == 1)
        {
            ResultImage.Source = _stages.RawMask;
            StageLabel.Text = "Maska";
        }
        else if (index == 2)
        {
            ResultImage.Source = _stages.CleanMask;
            StageLabel.Text = "Maska po morfo";
        }
        else
        {
            ResultImage.Source = _stages.Final;
            StageLabel.Text = "Finalni obrys";
        }
    }

    private void StageSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ShowStage((int)Math.Round(e.NewValue));
    }
}
