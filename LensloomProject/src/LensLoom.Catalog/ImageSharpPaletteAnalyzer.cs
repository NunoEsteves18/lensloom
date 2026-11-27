using LensLoom.Core.Abstractions;
using LensLoom.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LensLoom.Catalog;

public sealed class ImageSharpPaletteAnalyzer : IPaletteAnalyzer
{
    private const int SampleMaxDimension = 160;
    private const int BucketStep = 24;

    public async Task<PaletteInfo> AnalyzeAsync(
        string imagePath,
        int maxColors = 5,
        CancellationToken cancellationToken = default)
    {
        if (maxColors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxColors), "maxColors must be greater than zero.");
        }

        using var image = await Image.LoadAsync<Rgba32>(imagePath, cancellationToken);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(SampleMaxDimension, SampleMaxDimension),
            Mode = ResizeMode.Max
        }));

        var buckets = new Dictionary<(int R, int G, int B), (long Count, long SumR, long SumG, long SumB)>();
        long totalOpaquePixels = 0;
        double brightnessSum = 0;
        double warmthScoreSum = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = accessor.GetRowSpan(y);

                for (int x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.A < 32)
                    {
                        continue;
                    }

                    totalOpaquePixels++;

                    double luminance = (0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B) / 255.0;
                    brightnessSum += luminance;
                    warmthScoreSum += (pixel.R - pixel.B) / 255.0;

                    var key = (
                        R: (pixel.R / BucketStep) * BucketStep,
                        G: (pixel.G / BucketStep) * BucketStep,
                        B: (pixel.B / BucketStep) * BucketStep);

                    if (buckets.TryGetValue(key, out var current))
                    {
                        buckets[key] = (
                            current.Count + 1,
                            current.SumR + pixel.R,
                            current.SumG + pixel.G,
                            current.SumB + pixel.B);
                    }
                    else
                    {
                        buckets[key] = (1, pixel.R, pixel.G, pixel.B);
                    }
                }
            }
        });

        if (totalOpaquePixels == 0)
        {
            return new PaletteInfo(
                DominantColors: Array.Empty<ColorSwatch>(),
                TemperatureProfile: "Neutral",
                AverageBrightness: 0);
        }

        var swatches = buckets.Values
            .OrderByDescending(b => b.Count)
            .Take(maxColors)
            .Select(b =>
            {
                byte avgR = (byte)Math.Clamp(b.SumR / b.Count, 0, 255);
                byte avgG = (byte)Math.Clamp(b.SumG / b.Count, 0, 255);
                byte avgB = (byte)Math.Clamp(b.SumB / b.Count, 0, 255);
                double percentage = Math.Round((double)b.Count / totalOpaquePixels * 100.0, 2, MidpointRounding.AwayFromZero);

                return new ColorSwatch(
                    Hex: $"#{avgR:X2}{avgG:X2}{avgB:X2}",
                    Percentage: percentage);
            })
            .ToList();

        double avgBrightness = Math.Round(brightnessSum / totalOpaquePixels, 3, MidpointRounding.AwayFromZero);
        double avgWarmth = warmthScoreSum / totalOpaquePixels;

        string temperatureProfile = avgWarmth switch
        {
            > 0.05 => "Warm",
            < -0.05 => "Cool",
            _ => "Neutral"
        };

        return new PaletteInfo(
            DominantColors: swatches,
            TemperatureProfile: temperatureProfile,
            AverageBrightness: avgBrightness);
    }
}