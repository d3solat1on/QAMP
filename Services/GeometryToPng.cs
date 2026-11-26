using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QAMP.Services;

class GeometryToPng
{
    public static byte[]? GeometryToPngBytes(Geometry? geometry, int width = 128, int height = 128, Brush? fill = null)
    {
        if (geometry == null || width <= 0 || height <= 0)
            return null;

        var brush = fill ?? Brushes.White;
        var drawing = new GeometryDrawing(brush, null, geometry);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            Rect bounds = geometry.Bounds;
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
            {
                double padding = 4.0;
                double availW = Math.Max(1.0, width - padding * 2);
                double availH = Math.Max(1.0, height - padding * 2);
                double scale = Math.Min(availW / bounds.Width, availH / bounds.Height);

                double offsetX = padding + (availW - bounds.Width * scale) * 0.5 - bounds.Left * scale;
                double offsetY = padding + (availH - bounds.Height * scale) * 0.5 - bounds.Top * scale;

                var transform = new MatrixTransform(scale, 0, 0, scale, offsetX, offsetY);
                dc.PushTransform(transform);
            }

            dc.DrawGeometry(brush, null, geometry);
            if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                dc.Pop();
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
