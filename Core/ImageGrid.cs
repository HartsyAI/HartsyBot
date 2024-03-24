using System.Drawing;

namespace Hartsy.Core
{
    public class ImageGrid
    {
        public static string CreateGrid(List<string> imageBase64s, string outputPath)
        {
            int imageSize = 1024;  // Assuming square images for simplicity
            int gridSize = imageSize * 2;
            using var gridImage = new Bitmap(gridSize, gridSize);
            using var g = Graphics.FromImage(gridImage);

            for (int i = 0; i < imageBase64s.Count; i++)
            {
                using var imageStream = new MemoryStream(Convert.FromBase64String(imageBase64s[i]));
                using var image = Image.FromStream(imageStream);
                int x = (i % 2) * imageSize;
                int y = (i / 2) * imageSize;
                g.DrawImage(image, x, y, imageSize, imageSize);
            }

            string gridFilePath = Path.Combine(outputPath, "grid.png");
            gridImage.Save(gridFilePath, System.Drawing.Imaging.ImageFormat.Png);

            return gridFilePath;
        }
    }
}
