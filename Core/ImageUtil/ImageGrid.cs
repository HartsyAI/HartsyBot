using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Hartsy.Core.ImageUtil
{
    public class ImageGrid
    {
        /// <summary>Creates a grid image from a dictionary of image data, where each image is positioned 
        /// in a 2x2 grid based on its batch index. The size of the grid is dynamically determined 
        /// by the size of the first image.</summary>
        /// <param name="imagesData">A dictionary where each key is a batch index and each value is another dictionary containing the 'base64' key with the Base64 encoded image data as its value.</param>
        /// <returns>A new Image object representing the combined grid of images.</returns>
        public static async Task<Image<Rgba32>> CreateGridAsync(Dictionary<int, Dictionary<string, string>> imagesData, string username, ulong messageId)
        {
            // Load the first image to determine the dimensions
            KeyValuePair<string, string> firstImageEntry = imagesData.Values.First().First();
            byte[] firstImageBytes = Convert.FromBase64String(firstImageEntry.Value);
            using Image<Rgba32> firstImage = Image.Load<Rgba32>(firstImageBytes);
            int imageWidth = firstImage.Width;
            int imageHeight = firstImage.Height;
            bool isPreview = imageWidth < 1024;
            int multiplier = isPreview ? 2 : 1;
            int gridWidth = imageWidth * 2 * multiplier;
            int gridHeight = imageHeight * 2 * multiplier;
            using Image<Rgba32> gridImage = new(gridWidth, gridHeight);
            foreach (var imageEntry in imagesData)
            {
                int index = imageEntry.Key;
                Dictionary<string, string> imageData = imageEntry.Value;
                try
                {
                    if (imageData.TryGetValue("base64", out string? base64))
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        using Image<Rgba32> image = Image.Load<Rgba32>(imageBytes);
                        // Calculate x and y based on index to arrange in 2x2 grid
                        int x = index % 2 * imageWidth * multiplier;
                        int y = index / 2 * imageHeight * multiplier;
                        if (isPreview)
                        {
                            image.Mutate(i => i.Resize(imageWidth * multiplier, imageHeight * multiplier));
                        }
                        gridImage.Mutate(ctx => ctx.DrawImage(image, new Point(x, y), 1f));
                        await ImageHelpers.SaveImageAsync(image, username, messageId, index);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing base64 image data: {ex.Message}");
                }
            }
            if (!isPreview)
            {
                await ImageHelpers.AddWatermark(gridImage);
                gridImage.Mutate(i => i.Resize(gridWidth / 3, gridHeight / 3));
            }
            // Clone the gridImage to avoid disposal issues
            return gridImage.Clone();
        }
    }
}
