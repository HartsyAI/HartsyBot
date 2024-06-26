using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace Hartsy.Core
{
    public class ImageGrid
    {
        private static readonly string watermarkPath = "../../../images/logo.png";
        private static readonly string watermarkUrl = "https://github.com/kalebbroo/Hartsy/blob/main/images/logo.png?raw=true";

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
                await AddWatermark(gridImage);
                gridImage.Mutate(i => i.Resize(gridWidth / 3, gridHeight / 3));
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
                string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}/");
                Directory.CreateDirectory(directoryPath);  // Ensure the directory exists
                string filePath = Path.Combine(directoryPath, $"{messageId}:image_{imageIndex}.jpeg");
                await image.SaveAsJpegAsync(filePath);
            }
        }

        /// <summary>Adds a semi-transparent watermark to the bottom right corner of each quadrant in a grid image.</summary>
        /// <param name="gridImage">The grid image to add the watermark to.</param>
        /// <param name="watermarkImagePath">The path to the watermark image.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task AddWatermark(Image<Rgba32> gridImage)
        {
            try
            {
                Image<Rgba32> watermarkImage;
                // Load the watermark image from a file or URL
                if (File.Exists(watermarkPath))
                {
                    watermarkImage = await Image.LoadAsync<Rgba32>(watermarkPath);
                    Console.WriteLine($"Watermark loaded from file: {watermarkPath}"); // debug
                }
                else
                {
                    using HttpClient httpClient = new();
                    Stream watermarkStream = await httpClient.GetStreamAsync(watermarkUrl);
                    watermarkImage = await Image.LoadAsync<Rgba32>(watermarkStream);
                    Console.WriteLine("Watermark loaded from URL."); // debug
                }
                watermarkImage.Mutate(x => x.Opacity(0.3f)); // Apply 30% transparency to the watermark image
                // Calculate the size of each quadrant
                int quadrantWidth = gridImage.Width / 2;
                int quadrantHeight = gridImage.Height / 2;
                // Calculate the locations for each watermark in each quadrant
                Point[] locations =
                [
                    new Point(quadrantWidth - watermarkImage.Width, quadrantHeight - watermarkImage.Height),
                    new Point(gridImage.Width - watermarkImage.Width, quadrantHeight - watermarkImage.Height),
                    new Point(quadrantWidth - watermarkImage.Width, gridImage.Height - watermarkImage.Height),
                    new Point(gridImage.Width - watermarkImage.Width, gridImage.Height - watermarkImage.Height)
                ];
                // Draw the watermark image on each quadrant
                foreach (Point location in locations)
                {
                    gridImage.Mutate(x => x.DrawImage(watermarkImage, location, 1f));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to add Watermark: {ex}");
            }
        }

        /// <summary>Adds a watermark to the bottom right corner of an image. Used for Showcase submissions.</summary>
        /// <param name="mainImage">The main image to add the watermark to.</param>
        /// <returns>The main image with the watermark added to the bottom right corner.</returns>
        public static async Task<Image<Rgba32>> AddWatermarkBottomRight(Image<Rgba32> mainImage)
        {
            Image<Rgba32> watermarkImage;
            // Load the watermark image from a file or URL
            if (File.Exists(watermarkPath))
            {
                watermarkImage = await Image.LoadAsync<Rgba32>(watermarkPath);
            }
            else
            {
                using HttpClient httpClient = new();
                Stream watermarkStream = await httpClient.GetStreamAsync(watermarkUrl);
                watermarkImage = await Image.LoadAsync<Rgba32>(watermarkStream);
            }
            // Resize watermark to fit in the bottom right corner
            int watermarkWidth = mainImage.Width / 3;
            int watermarkHeight = watermarkImage.Height * watermarkWidth / watermarkImage.Width;
            watermarkImage.Mutate(x => x.Resize(watermarkWidth, watermarkHeight));
            // Define the position for the watermark (bottom-right corner)
            int xPosition = mainImage.Width - watermarkWidth - 10; // 10px padding
            int yPosition = mainImage.Height - watermarkHeight - 10; // 10px padding
            // Apply the watermark to the main image
            mainImage.Mutate(ctx => ctx.DrawImage(watermarkImage, new Point(xPosition, yPosition), 0.4f));
            return mainImage;
        }
    }
}
