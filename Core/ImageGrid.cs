﻿using SixLabors.ImageSharp;
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
                    if (!isPreview)
                    {
                        await AddWatermark(gridImage, "../../../images/logo.png");
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
        private static async Task AddWatermark(Image<Rgba32> gridImage, string watermarkImagePath)
        {
            try
            {
                using var originalWatermarkImage = await Image.LoadAsync<Rgba32>(watermarkImagePath);

                // Apply semi-transparency to the watermark image and create a clone for tiling
                using var transparentWatermark = originalWatermarkImage.Clone(ctx => ctx.ApplyProcessor(new OpacityProcessor(0.03f)));

                // Tile the semi-transparent watermark image across the grid
                for (int y = 0; y < gridImage.Height; y += transparentWatermark.Height)
                {
                    for (int x = 0; x < gridImage.Width; x += transparentWatermark.Width)
                    {
                        gridImage.Mutate(ctx => ctx.DrawImage(transparentWatermark, new Point(x, y), 1f));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding watermark: {ex.Message}");
            }
        }
    }
}
