using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Hartsy.Core
{
    public class ImageGrid
    {
        /// <summary>Creates a grid image from a dictionary of image data, where each image is positioned 
        /// in a 2x2 grid based on its batch index. The size of the grid is dynamically determined 
        /// by the size of the first image.</summary>
        /// <param name="imagesData">A dictionary where each key is a batch index and each value is another dictionary containing the 'base64' key with the Base64 encoded image data as its value.</param>
        /// <returns>A new Image object representing the combined grid of images.</returns>
        public static async Task<Image<Rgba32>> CreateGridAsync(Dictionary<int, Dictionary<string, string>> imagesData)
        {
            // Load the first image to determine the dimensions
            var firstImageEntry = imagesData.Values.First().First();
            byte[] firstImageBytes = Convert.FromBase64String(firstImageEntry.Value);
            using var firstImage = Image.Load<Rgba32>(firstImageBytes);

            int imageWidth = firstImage.Width;
            int imageHeight = firstImage.Height;
            int gridWidth = imageWidth * 2;
            int gridHeight = imageHeight * 2;

            using var gridImage = new Image<Rgba32>(gridWidth, gridHeight);

            foreach (var imageEntry in imagesData)
            {
                int index = imageEntry.Key;
                var imageData = imageEntry.Value;

                Console.WriteLine($"Processing image for batch index {index}");

                try
                {
                    if (imageData.TryGetValue("base64", out var base64))
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        using var image = Image.Load<Rgba32>(imageBytes);

                        // Calculate x and y based on index to arrange in 2x2 grid
                        int x = (index % 2) * imageWidth;
                        int y = (index / 2) * imageHeight;

                        gridImage.Mutate(ctx => ctx.DrawImage(image, new Point(x, y), 1f));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing base64 image data: {ex.Message}");
                }
            }
            // Clone the gridImage to avoid disposal issues
            return gridImage.Clone();
        }
    }
}
