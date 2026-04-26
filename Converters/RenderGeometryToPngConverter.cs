using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QAMP.Converters
{
    class RenderGeometryToPngConverter
    {
        public static byte[] RenderGeometryToPng(Geometry geometry, Brush brush, int size = 512)
        {
            var geometryDrawing = new GeometryDrawing(brush, null, geometry);

            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(geometryDrawing);

            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                double margin = size * 0.1;
                double contentSize = size - (margin * 2);

                var bounds = geometry.Bounds;
                double scale = Math.Min(contentSize / bounds.Width, contentSize / bounds.Height);

                double canvasCenter = size / 2.0;

                double iconCenterX = (bounds.Left + bounds.Width / 2.0) * scale;
                double iconCenterY = (bounds.Top + bounds.Height / 2.0) * scale;

                context.PushTransform(new TranslateTransform(canvasCenter - iconCenterX, canvasCenter - iconCenterY));
                context.PushTransform(new ScaleTransform(scale, scale));

                context.DrawDrawing(geometryDrawing);
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }
    }
}