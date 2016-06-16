using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Xna.Framework.Content.Pipeline.Utilities
{
    public static class DumpTextureExtensions
    {
        /// <summary>
        /// Dumps the passed Texture2DContent to disk in png format.
        /// Saves the first mip only if allMips=false.
        /// </summary>        
        public static void Dump(this TextureContent input, string filePath, bool allFaces, bool allMips)
        {
            var lastFace = input.Faces.Count;
            if (lastFace > 1 && !allFaces)
                lastFace = 1;

            for (var i = 0; i < lastFace; i++)
            {
                var face = input.Faces[i];

                var lastMip = face.Count;
                if (lastMip > 1 && !allMips)
                    lastMip = 1;

                for (var j = 0; j < lastMip; j++)
                {
                    var bm = face[j];
                    var data = bm.GetPixelData();
                    var width = bm.Width;
                    var height = bm.Height;

                    var bitmap = new System.Drawing.Bitmap(width, height);
                    int k = 0;
                    for (int y = 0; y < height; ++y)
                    {
                        for (int x = 0; x < width; ++x)
                        {
                            var c = System.Drawing.Color.FromArgb(data[k + 3], data[k], data[k + 1], data[k + 2]);
                            bitmap.SetPixel(x, y, c);
                            k += 4;
                        }
                    }

                    var fileType = System.Drawing.Imaging.ImageFormat.Png;
                    var filename = string.Format("{0}_Face{1}_Mip{2}.{3}", filePath, i, j, fileType);

                    bitmap.Save(filename, fileType);
                }
            }
        }

        /// <summary>
        /// Dumps the passed BitmapContent to disk in png format.
        /// </summary>     
        public static void Dump(this BitmapContent bitmapContent, string filePath)
        {
            var data = bitmapContent.GetPixelData();
            var width = bitmapContent.Width;
            var height = bitmapContent.Height;

            var bitmap = new System.Drawing.Bitmap(width, height);
            int j = 0;
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    var c = System.Drawing.Color.FromArgb(data[j + 3], data[j], data[j + 1], data[j + 2]);
                    bitmap.SetPixel(x, y, c);
                    j += 4;
                }
            }

            var fileType = System.Drawing.Imaging.ImageFormat.Png;
            var filename = string.Format("{0}.{1}", filePath, fileType);

            bitmap.Save(filename, fileType);
        }
    }
}
