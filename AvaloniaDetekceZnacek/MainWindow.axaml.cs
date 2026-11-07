using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;

namespace AvaloniaDetekceZnacek;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private ImageProcessing.DetectionStages? _stages;
    private List<CropClassifier.Example> _examples = new();

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
        {
            return;
        }

        await using var stream = await files[0].OpenReadAsync();
        using var bitmap = new Bitmap(stream);


        _stages = ImageProcessing.Process(bitmap);
        CropPanel.Children.Clear();
        for (int index = 0; index < _stages.Crops.Count; index++)
        {
            var region = _stages.Regions[index];
            var card = new StackPanel { Spacing = 4, Width = 120 };
            card.Children.Add(new Image
            {
                Source = _stages.Crops[index], Width = 120, Height = 120,
                Stretch = Avalonia.Media.Stretch.Uniform
            });
            card.Children.Add(new TextBlock
            {
                Text = $"#{index + 1}: {region.Width}x{region.Height}",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            });
            CropPanel.Children.Add(card);
        }
        RefreshClassificationResults();
        StageSlider.Value = 0;
        StageSlider.IsEnabled = true;
        ShowStage(0);
    }

    private async void LoadExamplesButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vyber složku se vzory",
            AllowMultiple = false
        });
        if (folders.Count == 0)
        {
            return;
        }

        _examples = CropClassifier.LoadExamples(folders[0].Path.LocalPath);
        RefreshClassificationResults();
    }

    private void RefreshClassificationResults()
    {
        ClassificationPanel.Children.Clear();
        if (_stages is null)
        {
            return;
        }
        if (_examples.Count == 0)
            return;

        for (int i = 0; i < _stages.Crops.Count; i++)
        {
            var result = CropClassifier.Classify(_stages.Crops[i], _examples);
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 14 };
            row.Children.Add(new Image
            {
                Source = _stages.Crops[i], Width = 120, Height = 120,
                Stretch = Avalonia.Media.Stretch.Uniform
            });
            row.Children.Add(new TextBlock
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Text = $"#{i + 1}\n Template matching: {result.TemplateLabel} ({result.TemplateSimilarity})\n HOG + k-NN: {result.KnnLabel} ({result.KnnDistance})"
            });
            ClassificationPanel.Children.Add(row);
        }
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
