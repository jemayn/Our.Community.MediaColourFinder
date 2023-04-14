using OurCommunityMediaColourFinder.Interfaces;
using OurCommunityMediaColourFinder.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace OurCommunityMediaColourFinder.Services;

public class ColourService : IColourService
{
    public ImageWithColour? GetImageWithColour(FocalPointRectangle focalPointRectangle)
    {
        return GetImagesWithColour(new[] { focalPointRectangle }).FirstOrDefault();
    }

    public IEnumerable<ImageWithColour> GetImagesWithColour(IEnumerable<FocalPointRectangle> imageFiles)
    {
        return imageFiles.Select(image => GetColours(image)).ToList();
    }

    private static ImageWithColour GetColours(FocalPointRectangle focusArea, int resizeWidth = 16,
        int resizeHeight = 16)
    {
        // Load the image with ImageSharp
        using Image<Rgba32>? image = Image.Load<Rgba32>(focusArea.Stream);

        // Crop the image to the focus area
        Rectangle rectangle = focusArea.GetRectangle();
        image.Mutate(x => x.Crop(rectangle));

        Rgba32 averageColour = GetAverageColour(resizeWidth, resizeHeight, image);
        Rgba32 brightestColor = GetBrightestColour(resizeWidth, resizeHeight, image);

        ImageWithColour imageWithColour = new()
        {
            Average = $"#{averageColour.ToHex()[..6]}", // Range indexing removes the alpha channel
            Brightest = $"#{brightestColor.ToHex()[..6]}",
            Opposite = InvertColorAndConvertToHex(averageColour.ToHex(), false),
            TextColour = InvertColorAndConvertToHex(averageColour.ToHex(), true),
        };
        return imageWithColour;
    }

    /// <summary>
    /// Calculates the average color of an image given a width and height to resize the image to
    /// and an <see cref="Image&lt;Rgba32&gt;"/> object that represents the image.
    /// </summary>
    /// <param name="resizeWidth">The width to resize the image to.</param>
    /// <param name="resizeHeight">The height to resize the image to.</param>
    /// <param name="image">The Image&lt;Rgba32&gt; object that represents the image.</param>
    /// <returns>An Rgba32 object that represents the average color of the image.</returns>
    private static Rgba32 GetAverageColour(int resizeWidth, int resizeHeight, Image<Rgba32> image)
    {
        // Calculate the average color
        long rSum = 0, gSum = 0, bSum = 0;
        var totalPixels = resizeWidth * resizeHeight;

        for (var y = 0; y < resizeHeight; y++)
        {
            for (var x = 0; x < resizeWidth; x++)
            {
                Rgba32 pixel = image[x, y];
                rSum += pixel.R;
                gSum += pixel.G;
                bSum += pixel.B;
            }
        }

        Rgba32 average = new(
            (byte)(rSum / totalPixels),
            (byte)(gSum / totalPixels),
            (byte)(bSum / totalPixels)
        );
        return average;
    }

    /// <summary>
    /// Finds the brightest color in an image given a width and height to resize the image to
    /// and an Image&lt;Rgba32&gt; object that represents the image.
    /// </summary>
    /// <param name="resizeWidth">The width to resize the image to.</param>
    /// <param name="resizeHeight">The height to resize the image to.</param>
    /// <param name="image">The Image&lt;Rgba32&gt; object that represents the image.</param>
    /// <returns>An Rgba32 object that represents the brightest color in the image.</returns>
    private static Rgba32 GetBrightestColour(int resizeWidth, int resizeHeight, Image<Rgba32> image)
    {
        var maxBrightness = 0;
        Rgba32 brightestColor = default;

        for (var y = 0; y < resizeHeight; y++)
        {
            for (var x = 0; x < resizeWidth; x++)
            {
                Rgba32 pixel = image[x, y];
                var brightness = (int)((0.299 * pixel.R) + (0.587 * pixel.G) + (0.114 * pixel.B));

                if (brightness <= maxBrightness)
                {
                    continue;
                }

                maxBrightness = brightness;
                brightestColor = pixel;
            }
        }

        return brightestColor;
    }

    /// <summary>
    /// Inverts the color represented by a given hex value and converts it to a hex string.
    /// If the `isBlackAndWhite` parameter is `true`, the method returns black or white based on the perceived brightness of the original color.
    /// </summary>
    /// <param name="hexValue">The hex value representing the original color.</param>
    /// <param name="isBlackAndWhite">A boolean value indicating whether the method should return black or white for high-brightness colors.</param>
    /// <returns>A hex string representing the inverted color.</returns>
    private static string InvertColorAndConvertToHex(string hexValue, bool isBlackAndWhite)
    {
        if (hexValue.StartsWith('#'))
        {
            hexValue = hexValue[1..];
        }

        // convert 3-digit hex to 6-digits.
        if (hexValue.Length == 3)
        {
            hexValue = hexValue[0] + hexValue[0].ToString() + hexValue[1] + hexValue[1] + hexValue[2] + hexValue[2];
        }

        if (hexValue.Length == 8)
        {
            // trim the last two characters as this is the "alpha" value
            hexValue = hexValue[..6];
        }

        if (hexValue.Length != 6)
        {
            throw new Exception("Invalid HEX color.");
        }

        var r = Convert.ToInt32(hexValue.Substring(0, 2), 16);
        var g = Convert.ToInt32(hexValue.Substring(2, 2), 16);
        var b = Convert.ToInt32(hexValue.Substring(4, 2), 16);
        if (isBlackAndWhite)
        {
            return GetBlackOrWhiteBasedOnPerceivedBrightness(r, g, b);
        }

        // invert color components
        r = 255 - r;
        g = 255 - g;
        b = 255 - b;

        // convert to hex
        var hexR = r.ToString("X2");
        var hexG = g.ToString("X2");
        var hexB = b.ToString("X2");

        // pad each with zeros and return
        return "#" + PadZero(hexR) + PadZero(hexG) + PadZero(hexB);
    }

    // https://stackoverflow.com/a/3943023/112731
    private static string GetBlackOrWhiteBasedOnPerceivedBrightness(int r, int g, int b)
    {
        var brightness = (r * 0.299) + (g * 0.587) + (b * 0.114);

        return brightness > 186 ? "#000000" : "#FFFFFF";
    }

    private static string PadZero(string str, int len = 2)
    {
        var zeros = new string('0', len);
        return (zeros + str)[str.Length..];
    }
}
