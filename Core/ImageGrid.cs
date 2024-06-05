using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace Hartsy.Core
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
            var firstImageEntry = imagesData.Values.First().First();
            byte[] firstImageBytes = Convert.FromBase64String(firstImageEntry.Value);
            using var firstImage = Image.Load<Rgba32>(firstImageBytes);
            int imageWidth = firstImage.Width;
            int imageHeight = firstImage.Height;
            bool isPreview = imageWidth < 1024;
            int multiplier = isPreview ? 2 : 1;
            int gridWidth = imageWidth * 2 * multiplier;
            int gridHeight = imageHeight * 2 * multiplier;
            using var gridImage = new Image<Rgba32>(gridWidth, gridHeight);
            foreach (var imageEntry in imagesData)
            {
                int index = imageEntry.Key;
                var imageData = imageEntry.Value;
                try
                {
                    if (imageData.TryGetValue("base64", out var base64))
                    {
                        byte[] imageBytes = Convert.FromBase64String(base64);
                        using var image = Image.Load<Rgba32>(imageBytes);
                        // Calculate x and y based on index to arrange in 2x2 grid
                        int x = (index % 2) * imageWidth * multiplier;
                        int y = (index / 2) * imageHeight * multiplier;
                        if (isPreview)
                        {
                            image.Mutate(i => i.Resize(imageWidth * multiplier, imageHeight * multiplier));
                        }
                        gridImage.Mutate(ctx => ctx.DrawImage(image, new Point(x, y), 1f));
                        await SaveImageAsync(image, username, messageId, index);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing base64 image data: {ex.Message}");
                }
            }
            if (!isPreview)
            {
                await AddWatermark(gridImage, "../../../images/logo.png");
            }
            // Clone the gridImage to avoid disposal issues
            return gridImage.Clone();
        }

        /// <summary>Saves an image asynchronously to a specified directory.</summary>
        /// <param name="image">The image to save.</param>
        /// <param name="username">The username associated with the image.</param>
        /// <param name="messageId">The message ID associated with the image.</param>
        /// <param name="imageIndex">The index of the image in the batch.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task SaveImageAsync(Image<Rgba32> image, string username, ulong messageId, int imageIndex)
        {
            // Check if the image is a final image based on its width
            if (image.Width == 1024)
            {
                var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}/");
                Directory.CreateDirectory(directoryPath);  // Ensure the directory exists
                var filePath = Path.Combine(directoryPath, $"{messageId}:image_{imageIndex}.jpeg");
                await image.SaveAsJpegAsync(filePath);
            }
        }

        /// <summary>Adds a semi-transparent watermark to the bottom right corner of each quadrant in a grid image.</summary>
        /// <param name="gridImage">The grid image to add the watermark to.</param>
        /// <param name="watermarkImagePath">The path to the watermark image.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task AddWatermark(Image<Rgba32> gridImage, string watermarkImagePath)
        {
            try
            {
                Console.WriteLine(watermarkImagePath);
                using var watermarkImage = await Image.LoadAsync<Rgba32>(watermarkImagePath);
                watermarkImage.Mutate(x => x.Opacity(0.2f)); // Apply 30% transparency to the watermark image
                // Calculate the size of each quadrant
                int quadrantWidth = gridImage.Width / 2;
                int quadrantHeight = gridImage.Height / 2;
                // Calculate the locations for each watermark in each quadrant
                var locations = new[]
                {
                    new Point(quadrantWidth - watermarkImage.Width, quadrantHeight - watermarkImage.Height),
                    new Point(gridImage.Width - watermarkImage.Width, quadrantHeight - watermarkImage.Height),
                    new Point(quadrantWidth - watermarkImage.Width, gridImage.Height - watermarkImage.Height),
                    new Point(gridImage.Width - watermarkImage.Width, gridImage.Height - watermarkImage.Height)
                };
                // Draw the watermark image on each quadrant
                foreach (var location in locations)
                {
                    gridImage.Mutate(x => x.DrawImage(watermarkImage, location, 1f));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to add Watermark: {ex}");
            }
        }

        public static async Task<Image<Rgba32>> AddWatermarkBottomRight(Image<Rgba32> mainImage)
        {
            Image<Rgba32> watermarkImage;
            string watermarkPathOrUrl = "../../../images/logo.png";
            // Load the watermark image from a file or URL
            if (File.Exists(watermarkPathOrUrl))
            {
                watermarkImage = await Image.LoadAsync<Rgba32>(watermarkPathOrUrl);
            }
            else
            {
                using HttpClient httpClient = new();
                Stream watermarkStream = await httpClient.GetStreamAsync(watermarkPathOrUrl);
                watermarkImage = await Image.LoadAsync<Rgba32>(watermarkStream);
            }
            // Resize watermark to fit in the bottom right corner
            int watermarkWidth = mainImage.Width / 5;
            int watermarkHeight = watermarkImage.Height * watermarkWidth / watermarkImage.Width;
            watermarkImage.Mutate(x => x.Resize(watermarkWidth, watermarkHeight));
            // Define the position for the watermark (bottom-right corner)
            int xPosition = mainImage.Width - watermarkWidth - 10; // 10px padding
            int yPosition = mainImage.Height - watermarkHeight - 10; // 10px padding
            // Apply the watermark to the main image
            mainImage.Mutate(ctx => ctx.DrawImage(watermarkImage, new Point(xPosition, yPosition), 1f));
            return mainImage;
        }
    }
}
