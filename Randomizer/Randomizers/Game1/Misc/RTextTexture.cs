using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Randomizer.Randomizers.Game1.Misc
{
    /// <summary>
    /// Creates a basic "text" texture
    /// </summary>
    internal class RTextTexture
    {
        const string text = "©adamrussell.com";
        const float WatermarkPadding = 18f;
        const string WatermarkFont = "Roboto";
        const float WatermarkFontSize = 64f;

        public static byte[] CreateTextTexture(string text, int width, int height)
        {


            var image = Image.Load("source-filename.jpg");

            FontFamily fontFamily;

            if (!SystemFonts.TryGet(WatermarkFont, out fontFamily))
                throw new Exception($"Couldn't find font {WatermarkFont}");

            var font = fontFamily.CreateFont(WatermarkFontSize, FontStyle.Regular);

            var options = new TextOptions(font)
            {
                Dpi = 72,
                KerningMode = KerningMode.Standard
            };

            var rect = TextMeasurer.MeasureSize(text, options);

            image.Mutate(x => x.DrawText(
                text,
                font,
                new Color(Rgba32.ParseHex("#FFFFFFEE")),
                new PointF(image.Width - rect.Width - WatermarkPadding,
                    image.Height - rect.Height - WatermarkPadding)));

            //await image.SaveAsJpegAsync("output-filename.jpg");
            return null;
        }
    }
}
