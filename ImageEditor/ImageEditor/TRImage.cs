using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageEditor
{
    class TrImage
    {
        public enum MaskDrawType { None, SelectedOnly, DimOthers }

        public Bitmap OriginalImage;
        public float[,] ExcessiveGreen;
        public int[,] CatecoryMask;
        public int OriginalImageHeight;
        public int OriginalImageWidth;

        public void LoadImage(string path)
        {
            OriginalImage = new Bitmap(path);
            OriginalImageHeight = OriginalImage.Height;
            OriginalImageWidth = OriginalImage.Width;

            CalculateExcessiveGreen();
        }

        /// <summary>
        /// Calculate excessive green minus excessive red 
        /// </summary>
        unsafe void CalculateExcessiveGreen()
        {
            ExcessiveGreen = new float[OriginalImageWidth, OriginalImageHeight];

            BitmapData bData = OriginalImage.LockBits(new Rectangle(0, 0, OriginalImageWidth, OriginalImageHeight), ImageLockMode.ReadWrite, OriginalImage.PixelFormat);

            byte bitsPerPixel = GetBitsPerPixel(bData.PixelFormat);

            /*This time we convert the IntPtr to a ptr*/
            byte* scan0 = (byte*)bData.Scan0.ToPointer();

            for (int y = 0; y < bData.Height; ++y)
            {
                for (int x = 0; x < bData.Width; ++x)
                {
                    byte* data = scan0 + y * bData.Stride + x * bitsPerPixel / 8;

                    float r = data[0];
                    float g = data[1];
                    float b = data[2];

                    if ((r + b + g) > 0)
                        ExcessiveGreen[x, y] = (3 * g - 2.4f * r - b) / (r + b + g);
                    else
                        ExcessiveGreen[x, y] = 0;
                }
            }

            OriginalImage.UnlockBits(bData);

        }


        public void CalculateCatecoryMask(float threshold)
        {
            CatecoryMask = new int[OriginalImageWidth, OriginalImageHeight];

            for (int x = 0; x < OriginalImageWidth; x++)
            {
                for (int y = 0; y < OriginalImageHeight; y++)
                {
                    CatecoryMask[x, y] = ExcessiveGreen[x, y] > threshold ? 1 : 0;
                }
            }
        }

        unsafe public Bitmap DrawMask(MaskDrawType type, int category)
        {
            Bitmap retval = new Bitmap(OriginalImage);

            BitmapData bDataOriginal = OriginalImage.LockBits(new Rectangle(0, 0, OriginalImageWidth, OriginalImageHeight), ImageLockMode.ReadWrite, OriginalImage.PixelFormat);
            byte bitsPerPixelOriginal = GetBitsPerPixel(bDataOriginal.PixelFormat);

            BitmapData bDataRetval = retval.LockBits(new Rectangle(0, 0, retval.Width, retval.Height), ImageLockMode.ReadWrite, retval.PixelFormat);
            byte bitsPerPixelRetval = GetBitsPerPixel(bDataRetval.PixelFormat);



            byte* scan0Original = (byte*)bDataOriginal.Scan0.ToPointer();
            byte* scan0Retval = (byte*)bDataRetval.Scan0.ToPointer();

            for (int y = 0; y < bDataOriginal.Height; ++y)
            {
                for (int x = 0; x < bDataOriginal.Width; ++x)
                {
                    byte* dataOriginal = scan0Original + y * bDataOriginal.Stride + x * bitsPerPixelOriginal / 8;
                    byte* dataRetval = scan0Retval + y * bDataRetval.Stride + x * bitsPerPixelRetval / 8;

                    byte r = dataOriginal[0];
                    byte g = dataOriginal[1];
                    byte b = dataOriginal[2];

                    switch (type)
                    {
                        case MaskDrawType.SelectedOnly:
                            if (CatecoryMask[x, y] == category)
                            {
                                dataRetval[0] = r;
                                dataRetval[1] = g;
                                dataRetval[2] = b;
                            }
                            else
                            {
                                dataRetval[0] = 0;
                                dataRetval[1] = 0;
                                dataRetval[2] = 0;
                            }
                            break;

                        case MaskDrawType.DimOthers:
                            if (CatecoryMask[x, y] == category)
                            {
                                dataRetval[0] = r;
                                dataRetval[1] = g;
                                dataRetval[2] = b;
                            }
                            else
                            {
                                dataRetval[0] = (byte)(r >> 1);
                                dataRetval[1] = (byte)(g >> 1);
                                dataRetval[2] = (byte)(b >> 1);
                            }
                            break;
                        case MaskDrawType.None:

                            dataRetval[0] = r;
                            dataRetval[1] = g;
                            dataRetval[2] = b;
                            break;
                    }
                }
            }

            OriginalImage.UnlockBits(bDataOriginal);
            retval.UnlockBits(bDataRetval);

            return (retval);
        }

        private byte GetBitsPerPixel(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format24bppRgb:
                    return 24;
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return 32;
                default:
                    throw new ArgumentException("Only 24 and 32 bit images are supported");

            }
        }
    }
}

