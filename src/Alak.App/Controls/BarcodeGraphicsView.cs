using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Alak.App.Controls;

public enum BarcodeType
{
    Code128,
    QrCode
}

/// <summary>
/// Pure C# vector barcode renderer using Microsoft.Maui.Graphics.ICanvas (0 MB dependencies, zero SkiaSharp overhead).
/// Supports Code 128 (using standard Code 128 pattern table) and QR Matrix.
/// </summary>
public class BarcodeGraphicsView : GraphicsView, IDrawable
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(BarcodeGraphicsView), string.Empty,
            propertyChanged: (b, _, _) => ((BarcodeGraphicsView)b).Invalidate());

    public static readonly BindableProperty BarcodeTypeProperty =
        BindableProperty.Create(nameof(BarcodeType), typeof(BarcodeType), typeof(BarcodeGraphicsView), BarcodeType.Code128,
            propertyChanged: (b, _, _) => ((BarcodeGraphicsView)b).Invalidate());

    public static readonly BindableProperty BarColorProperty =
        BindableProperty.Create(nameof(BarColor), typeof(Color), typeof(BarcodeGraphicsView), Colors.Black,
            propertyChanged: (b, _, _) => ((BarcodeGraphicsView)b).Invalidate());

    public static readonly BindableProperty QuietZoneColorProperty =
        BindableProperty.Create(nameof(QuietZoneColor), typeof(Color), typeof(BarcodeGraphicsView), Colors.Transparent,
            propertyChanged: (b, _, _) => ((BarcodeGraphicsView)b).Invalidate());

    public static readonly BindableProperty ShowTextProperty =
        BindableProperty.Create(nameof(ShowText), typeof(bool), typeof(BarcodeGraphicsView), true,
            propertyChanged: (b, _, _) => ((BarcodeGraphicsView)b).Invalidate());

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public BarcodeType BarcodeType
    {
        get => (BarcodeType)GetValue(BarcodeTypeProperty);
        set => SetValue(BarcodeTypeProperty, value);
    }

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public Color QuietZoneColor
    {
        get => (Color)GetValue(QuietZoneColorProperty);
        set => SetValue(QuietZoneColorProperty, value);
    }

    public bool ShowText
    {
        get => (bool)GetValue(ShowTextProperty);
        set => SetValue(ShowTextProperty, value);
    }

    public BarcodeGraphicsView()
    {
        Drawable = this;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (string.IsNullOrWhiteSpace(Value)) return;

        canvas.SaveState();
        try
        {
            if (QuietZoneColor != null && QuietZoneColor != Colors.Transparent)
            {
                canvas.FillColor = QuietZoneColor;
                canvas.FillRectangle(dirtyRect);
            }

            if (BarcodeType == BarcodeType.QrCode)
            {
                DrawQrCode(canvas, dirtyRect);
            }
            else
            {
                DrawCode128(canvas, dirtyRect);
            }
        }
        finally
        {
            canvas.RestoreState();
        }
    }

    private void DrawQrCode(ICanvas canvas, RectF dirtyRect)
    {
        var matrix = QrCodeGenerator.GenerateMatrix(Value);
        if (matrix == null)
        {
            // Fall back to standard Code 128 if payload exceeds QR Version 4 capacity
            DrawCode128(canvas, dirtyRect);
            return;
        }

        int size = matrix.GetLength(0);
        float padding = 8f;
        float availableWidth = Math.Max(10f, dirtyRect.Width - (padding * 2));
        float availableHeight = Math.Max(10f, dirtyRect.Height - (padding * 2));
        float minDim = Math.Min(availableWidth, availableHeight);
        float moduleSize = minDim / size;

        float startX = dirtyRect.X + (dirtyRect.Width - (moduleSize * size)) / 2f;
        float startY = dirtyRect.Y + (dirtyRect.Height - (moduleSize * size)) / 2f;

        canvas.FillColor = BarColor;

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (matrix[r, c])
                {
                    canvas.FillRectangle(startX + (c * moduleSize), startY + (r * moduleSize), moduleSize + 0.4f, moduleSize + 0.4f);
                }
            }
        }
    }

    private void DrawCode128(ICanvas canvas, RectF dirtyRect)
    {
        var codes = Code128Encoder.Encode(Value);
        if (codes == null || codes.Count == 0) return;

        // Calculate total modules: quiet zones (10 on each side) + sum of pattern widths
        int totalModules = 20; // 10 quiet modules on left + 10 on right
        foreach (var c in codes)
        {
            var pattern = Code128Encoder.Patterns[c];
            foreach (var w in pattern) totalModules += w;
        }

        float moduleWidth = dirtyRect.Width / totalModules;
        float textReserve = ShowText ? 18f : 0f;
        float barHeight = Math.Max(16f, dirtyRect.Height - textReserve - 6f);
        float startY = dirtyRect.Y + 2f;
        float curX = dirtyRect.X + (10f * moduleWidth);

        canvas.FillColor = BarColor;

        foreach (var c in codes)
        {
            var pattern = Code128Encoder.Patterns[c];
            for (int i = 0; i < pattern.Length; i++)
            {
                float w = pattern[i] * moduleWidth;
                if (i % 2 == 0) // Bar
                {
                    canvas.FillRectangle(curX, startY, w, barHeight);
                }
                curX += w;
            }
        }

        if (ShowText)
        {
            canvas.FontColor = BarColor;
            canvas.FontSize = 11f;
            canvas.DrawString(Value, dirtyRect.X, startY + barHeight + 2f, dirtyRect.Width, textReserve,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
