using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ImageEditor
{
    class TRImage
    {
        public enum MaskDrawType { SelectedOnly, DimOthers }

        public Bitmap OriginalImage;
        public float[,] ExcessiveGreen;
        public int[,] CatecoryMask;


        public void LoadImage(string path)
        {
            OriginalImage = new Bitmap(path);
            CalculateExcessiveGreen();
        }

        /// <summary>
        /// Calculate excessive green minus excessive red 
        /// </summary>
        void CalculateExcessiveGreen()
        {
            ExcessiveGreen = new float[OriginalImage.Width, OriginalImage.Height];

            for (int x = 0; x < OriginalImage.Width; x++)
            {
                for (int y = 0; y < OriginalImage.Height; y++)
                {
                    var px = OriginalImage.GetPixel(x, y);
                    float r = px.R;
                    float g = px.G;
                    float b = px.B;

                    if ((r + b + g) > 0)
                        ExcessiveGreen[x, y] = (3 * g - 2.4f * r - b) / (r + b + g);
                    else
                        ExcessiveGreen[x, y] = 0;
                }
            }
        }

        public void CalculateCatecoryMask(float stresshold)
        {
            CatecoryMask = new int[OriginalImage.Width, OriginalImage.Height];

            for (int x = 0; x < OriginalImage.Width; x++)
            {
                for (int y = 0; y < OriginalImage.Height; y++)
                {
                    CatecoryMask[x, y] = ExcessiveGreen[x, y] > stresshold ? 1 : 0;
                }
            }
        }

        public Bitmap DrawMask(MaskDrawType type, int category)
        {
            Bitmap retval = new Bitmap(OriginalImage);

            for (int x = 0; x < OriginalImage.Width; x++)
            {
                for (int y = 0; y < OriginalImage.Height; y++)
                {
                    switch (type)
                    {
                        case MaskDrawType.SelectedOnly:
                            if (CatecoryMask[x, y] == category)
                                retval.SetPixel(x, y, OriginalImage.GetPixel(x, y));
                            else
                                retval.SetPixel(x, y, Color.Black);
                            break;

                        case MaskDrawType.DimOthers:
                            if (CatecoryMask[x, y] == category)
                                retval.SetPixel(x, y, OriginalImage.GetPixel(x, y));
                            else
                            {
                                Color px = OriginalImage.GetPixel(x, y);
                                Color newpx = Color.FromArgb(255, px.R / 2, px.B / 2, px.G / 2);
                                retval.SetPixel(x, y, newpx);
                            }
                            break;
                    }
                }
            }
            return (retval);
        }
    }
}

