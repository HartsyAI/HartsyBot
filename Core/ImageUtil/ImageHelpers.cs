using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Hartsy.Core.ImageUtil
{
    public class ImageHelpers
    {
        /// <summary>Saves an image asynchronously to a specified directory.</summary>
        /// <param name="image">The image to save.</param>
        /// <param name="username">The username associated with the image.</param>
        /// <param name="messageId">The message ID associated with the image.</param>
        /// <param name="imageIndex">The index of the image in the batch.</param>
        /// <param name="format">The image format to use for saving (e.g., "jpeg", "gif").</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task SaveImageAsync(Image<Rgba32> image, string username, ulong messageId, int imageIndex, string format = "jpeg")
        {
            string directoryPath = Path.Combine(Directory.GetCurrentDirectory(), $"../../../images/{username}/{messageId}/");
            Directory.CreateDirectory(directoryPath); // Ensure the directory exists
            string fileExtension = format.ToLower();
            string filePath = Path.Combine(directoryPath, $"{messageId}-image_{imageIndex}.{fileExtension}");
            if (imageIndex == 5)
            {
                filePath = Path.Combine(directoryPath, $"{messageId}-new_image.{fileExtension}");
            }
            switch (format.ToLower())
            {
                case "jpeg":
                    await image.SaveAsJpegAsync(filePath);
                    break;
                case "gif":
                    await image.SaveAsGifAsync(filePath);
                    break;
                case "png":
                    await image.SaveAsPngAsync(filePath);
                    break;
                case "webp":
                    await image.SaveAsWebpAsync(filePath);
                    break;
                default:
                    await image.SaveAsJpegAsync(filePath);
                    break;
            }
        }

        private static readonly string watermarkPath = "../../../images/logo.png";
        private static readonly string watermarkUrl = "https://github.com/kalebbroo/HartsyBot/blob/main/images/logo.png?raw=true";

        /// <summary>Adds a semi-transparent watermark to the bottom right corner of each quadrant in a grid image.</summary>
        /// <param name="gridImage">The grid image to add the watermark to.</param>
        /// <param name="watermarkImagePath">The path to the watermark image.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task AddWatermarkGrid(Image<Rgba32> gridImage)
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
            // TODO: THIS NEEDS TO BE REFACTORED. THIS SUCKS.
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

        /// <summary>Adds a watermark to every frame of an animated GIF and returns the modified image as a memory stream.</summary>
        /// <param name="imagePath">Path to the original GIF image.</param>
        /// <returns>A memory stream containing the watermarked GIF.</returns>
        public static async Task<MemoryStream> AddWatermarkToGifAsync(string imagePath)
        {
            MemoryStream ms = new();
            using Image<Rgba32> originalImage = await Image.LoadAsync<Rgba32>(imagePath);
            Image<Rgba32> watermarkImage = await LoadWatermarkImageAsync();
            // Create a list to store the modified frames
            List<Image<Rgba32>> modifiedFrames = [];
            foreach (ImageFrame<Rgba32> frame in originalImage.Frames)
            {
                // Create a new image from the frame data
                Image<Rgba32> frameImage = new(frame.Width, frame.Height);
                // Copy pixel data from the original frame to the new image
                frameImage.Frames[0].DangerousTryGetSinglePixelMemory(out Memory<Rgba32> memory);
                frame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> frameMemory);
                frameMemory.CopyTo(memory);
                ApplyWatermarkDirectlyToImage(frameImage, watermarkImage);
                // Add the processed frame to the list
                modifiedFrames.Add(frameImage);
            }
            // Create a new image to hold the frames with watermarks
            using var newImage = new Image<Rgba32>(originalImage.Width, originalImage.Height);
            foreach (var modifiedFrame in modifiedFrames)
            {
                newImage.Frames.AddFrame(modifiedFrame.Frames[0]);
            }
            // Save the new animated GIF to the memory stream
            newImage.SaveAsGif(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>Applies a watermark directly to an image.</summary>
        /// <param name="image">The image to apply the watermark to.</param>
        /// <param name="watermarkImage">The watermark image.</param>
        private static void ApplyWatermarkDirectlyToImage(Image<Rgba32> image, Image<Rgba32> watermarkImage)
        {
            int watermarkWidth = image.Width / 5;
            int watermarkHeight = watermarkImage.Height * watermarkWidth / watermarkImage.Width;
            watermarkImage.Mutate(x => x.Resize(watermarkWidth, watermarkHeight));
            int xPosition = image.Width - watermarkWidth - 10;
            int yPosition = image.Height - watermarkHeight - 10;
            image.Mutate(ctx => ctx.DrawImage(watermarkImage, new Point(xPosition, yPosition), 0.5f)); // Apply 50% opacity
        }

        /// <summary>Loads the watermark image from a file or URL.</summary>
        /// <returns>The loaded watermark image.</returns>
        private static async Task<Image<Rgba32>> LoadWatermarkImageAsync()
        {
            if (File.Exists(watermarkPath))
            {
                return await Image.LoadAsync<Rgba32>(watermarkPath);
            }
            else
            {
                using HttpClient httpClient = new();
                Stream watermarkStream = await httpClient.GetStreamAsync(watermarkUrl);
                return await Image.LoadAsync<Rgba32>(watermarkStream);
            }
        }

        public static string ExtractFirstFrame(string gifFilePath)
        {
            if (string.IsNullOrEmpty(gifFilePath))
            {
                Console.WriteLine("Error: The provided GIF file path is null or empty.");
                return string.Empty;
            }
            string firstFrameFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(gifFilePath)}_first_frame.png");
            try
            {
                using Image<Rgba32> gifImage = Image.Load<Rgba32>(gifFilePath);
                if (gifImage.Frames.Count == 0)
                {
                    Console.WriteLine("Error: The GIF does not contain any frames.");
                    return string.Empty;
                }
                Image<Rgba32> firstFrame = gifImage.Frames.CloneFrame(0);
                firstFrame.Save(firstFrameFilePath);
                Console.WriteLine($"First frame extracted and saved to: {firstFrameFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting the first frame: {ex.Message}");
                return string.Empty;
            }
            return firstFrameFilePath;
        }

        public static async Task<string?> DownloadFileFromEmbedAsync(string fileUrl, string fileExtension = "gif")
        {
            if (string.IsNullOrEmpty(fileUrl))
            {
                Console.WriteLine("Error: The provided file URL is null or empty.");
                return null;
            }
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{fileExtension}");
            try
            {
                using HttpClient httpClient = new();
                byte[] fileData = await httpClient.GetByteArrayAsync(fileUrl);
                if (fileData == null || fileData.Length == 0)
                {
                    Console.WriteLine("Error: The downloaded file data is null or empty.");
                    return null;
                }
                await File.WriteAllBytesAsync(tempFilePath, fileData);
                Console.WriteLine($"File downloaded and saved to: {tempFilePath}");
            }
            catch (HttpRequestException httpRequestException)
            {
                Console.WriteLine($"Error downloading file: {httpRequestException.Message}");
                return null;
            }
            catch (IOException ioException)
            {
                Console.WriteLine($"Error saving file: {ioException.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return null;
            }
            return tempFilePath;
        }
    }
}