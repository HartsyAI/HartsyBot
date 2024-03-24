using System.Drawing;

namespace Hartsy.Core
{
    public class ImageGrid
    {
        public static string CreateGrid(List<string> imagePaths, string outputPath)
        {
            const int imagesPerRow = 2;
            const int imageSizeWidth = 1024;
            const int imageSizeHeight = 768;

            int gridWidth = imagesPerRow * imageSizeWidth;
            int gridHeight = (imagePaths.Count / imagesPerRow) * imageSizeHeight;

            using var gridImage = new Bitmap(gridWidth, gridHeight);
            using (var g = Graphics.FromImage(gridImage))
            {
                g.Clear(Color.Black); // Fill background if there are transparent areas

                for (int i = 0; i < imagePaths.Count; i++)
                {
                    using var image = Image.FromFile(imagePaths[i]);
                    int x = (i % imagesPerRow) * imageSizeWidth;
                    int y = (i / imagesPerRow) * imageSizeHeight;
                    g.DrawImage(image, new Rectangle(x, y, imageSizeWidth, imageSizeHeight));
                }
            }

            string gridImagePath = Path.Combine(outputPath, "grid.png");
            gridImage.Save(gridImagePath, System.Drawing.Imaging.ImageFormat.Png);

            return gridImagePath;
        }
    }
}
