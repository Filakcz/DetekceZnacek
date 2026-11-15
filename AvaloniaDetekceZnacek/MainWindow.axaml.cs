using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Documents;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvaloniaDetekceZnacek;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();


        string examplesPath = Path.Combine(AppContext.BaseDirectory, "Vzory");
        _examples = CropClassifier.LoadExamples(examplesPath);
    }

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

            var textBlo = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            textBlo.Inlines!.Add(new Run($"#{i + 1}\n"));
            textBlo.Inlines.Add(new Run($"Template matching: {result.TemplateLabel} ({result.TemplateSimilarity})\n")
            {
                Foreground = result.TemplateOverThreshold ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.Green
            });
            textBlo.Inlines.Add(new Run($"HOG + k-NN: {result.KnnLabel} ({result.KnnDistance})")
            {
                Foreground = result.KnnOverThreshold ? Avalonia.Media.Brushes.Red : Avalonia.Media.Brushes.Green
            });

            row.Children.Add(textBlo);

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
            StageLabel.Text = "Původní obrázek";
        }
        else if (index == 1)
        {
            ResultImage.Source = _stages.RawMask;
            StageLabel.Text = "Maska obrázku";
        }
        else if (index == 2)
        {
            ResultImage.Source = _stages.CleanMask;
            StageLabel.Text = "Maska po morfologických úpravách";
        }
        else
        {
            ResultImage.Source = _stages.Final;
            StageLabel.Text = "Označení potenciálních značek";
        }
    }

    private void StageSlider_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ShowStage((int)Math.Round(e.NewValue));
    }

    private async void TestFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vyber složku s obrázky a Reseni.txt",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        string? folderPath = folders[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            TestFolderLabel.Text = "Složku se nepodařilo otevřít.";
            return;
        }

        TestFolderLabel.Text = folderPath;
        TestResultsPanel.Children.Clear();

        string solutionPath = Path.Combine(folderPath, "Reseni.txt");
        if (!File.Exists(solutionPath))
        {
            TestResultsPanel.Children.Add(new TextBlock { Text = "Ve složce chybí Reseni.txt.", Foreground = Avalonia.Media.Brushes.Red });
            return;
        }

        var solutions = new Dictionary<string, HashSet<string>>();
        foreach (string line in File.ReadAllLines(solutionPath))
        {
            string[] parts = line.Split('=');
            HashSet<string> values = new HashSet<string>();

            string[] signs = parts[1].Split(';');

            foreach (string sign in signs)
            {
                if (!string.IsNullOrWhiteSpace(sign))
                {
                    values.Add(sign);
                }
            }

            solutions[parts[0]] = values;
        }

        List<string> imageFiles = new List<string>();
        HashSet<string> extensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".bmp"};
        
        foreach (string path in Directory.GetFiles(folderPath))
        {
            if (extensions.Contains(Path.GetExtension(path)))
            {
                imageFiles.Add(path);
            }
        }

        foreach (string imagePath in imageFiles)
        {
            string imageNumber = Path.GetFileNameWithoutExtension(imagePath);
            StackPanel section = new StackPanel { Spacing = 6 };
            section.Children.Add(new TextBlock { Text = $"Obrázek {imageNumber}", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.Bold });
            if (!solutions.TryGetValue(imageNumber, out var expected))
            {
                section.Children.Add(new TextBlock { Text = "Pro obrázek není v Reseni.txt řešení.", Foreground = Avalonia.Media.Brushes.Red });
                TestResultsPanel.Children.Add(section);
                continue;
            }

            using var bitmap = new Bitmap(imagePath);
            var stages = ImageProcessing.Process(bitmap);
            var imageRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
            imageRow.Children.Add(new Image { Source = stages.Final, Width = 240, Height = 160, Stretch = Avalonia.Media.Stretch.Uniform });
            var details = new StackPanel { Spacing = 4, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            HashSet<string> remainingExpected = new HashSet<string>(expected);
            for (int i = 0; i < stages.Crops.Count; i++)
            {
                var result = CropClassifier.Classify(stages.Crops[i], _examples);
                if (!result.KnnOverThreshold)
                {
                    details.Children.Add(new TextBlock
                    {
                        Text = $"Detekce #{i + 1}: {result.KnnLabel}",
                        Foreground = remainingExpected.Remove(result.KnnLabel) ? Avalonia.Media.Brushes.Green : Avalonia.Media.Brushes.Red
                    });
                }
            }
            foreach (string missing in remainingExpected)
            {
                details.Children.Add(new TextBlock { Text = $"Chybí: {missing}", Foreground = Avalonia.Media.Brushes.Red });
            }
            if (stages.Crops.Count == 0 && expected.Count == 0)
            {
                details.Children.Add(new TextBlock { Text = "Správně: žádné značky", Foreground = Avalonia.Media.Brushes.Green });
            }
            imageRow.Children.Add(details);
            section.Children.Add(imageRow);
            TestResultsPanel.Children.Add(section);
        }
    }
}
