using Android.Graphics;
using Android.Graphics.Drawables;

namespace Alak.App.Platforms.Android;

/// <summary>Downscales a drawable to a small PNG for the apps cache (CAP-013).</summary>
internal static class AndroidIconEncoder
{
    private const int MaxSize = 96;

    public static byte[]? Encode(Drawable? drawable)
    {
        if (drawable == null) return null;

        try
        {
            if (drawable is BitmapDrawable { Bitmap: { } bitmap } && bitmap.Width > 0 && bitmap.Height > 0)
            {
                return EncodeBitmap(bitmap);
            }

            using var target = Bitmap.CreateBitmap(MaxSize, MaxSize, Bitmap.Config.Argb8888!);
            using var canvas = new Canvas(target);
            drawable.SetBounds(0, 0, MaxSize, MaxSize);
            drawable.Draw(canvas);
            return EncodeBitmap(target);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? EncodeBitmap(Bitmap source)
    {
        var scale = MaxSize / (double)Math.Max(source.Width, source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var scaled = Bitmap.CreateScaledBitmap(source, width, height, true);
        try
        {
            using var stream = new MemoryStream();
            scaled.Compress(Bitmap.CompressFormat.Png!, 100, stream);
            return stream.ToArray();
        }
        finally
        {
            if (!ReferenceEquals(scaled, source)) scaled.Recycle();
        }
    }
}
