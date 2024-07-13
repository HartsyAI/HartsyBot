using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
                default:
                    await image.SaveAsJpegAsync(filePath);
                    break;
            }
        }
    }
}
