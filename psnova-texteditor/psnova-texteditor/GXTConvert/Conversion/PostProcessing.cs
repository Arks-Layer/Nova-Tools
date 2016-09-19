﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace GXTConvert.Conversion
{
    public static class PostProcessing
    {
        // Unswizzle logic by @FireyFly
        // http://xen.firefly.nu/up/rearrange.c.html

        #region Untile

        static readonly int[] tileOrder =
        {
            0, 1, 8, 9,
            2, 3, 10, 11,
            16, 17, 24, 25,
            18, 19, 26, 27,
            
            4, 5, 12, 13,
            6, 7, 14, 15,
            20, 21, 28, 29,
            22, 23, 30, 31,

            32, 33, 40, 41,
            34, 35, 42, 43,
            48, 49, 56, 57,
            50, 51, 58, 59,
            
            36, 37, 44, 45,
            38, 39, 46, 47,
            52, 53, 60, 61,
            54, 55, 62, 63
        };

        private static int GetTilePixelIndex(int t, int x, int y, int width)
        {
            return (int)((((tileOrder[t] / 8) + y) * width) + ((tileOrder[t] % 8) + x));
        }

        private static int GetTilePixelOffset(int t, int x, int y, int width, PixelFormat pixelFormat)
        {
            return (GetTilePixelIndex(t, x, y, width) * (Bitmap.GetPixelFormatSize(pixelFormat) / 8));
        }

        public static byte[] UntileTexture(byte[] pixelData, int width, int height, PixelFormat pixelFormat)
        {
            byte[] untiled = new byte[pixelData.Length];

            int s = 0;
            for (int y = 0; y < height; y += 8)
            {
                for (int x = 0; x < width; x += 8)
                {
                    for (int t = 0; t < (8 * 8); t++)
                    {
                        int pixelOffset = GetTilePixelOffset(t, x, y, width, pixelFormat);
                        Buffer.BlockCopy(pixelData, s, untiled, pixelOffset, 4);
                        s += 4;
                    }
                }
            }

            return untiled;
        }

        #endregion

        #region Unswizzle (Morton)

        private static int Compact1By1(int x)
        {
            x &= 0x55555555;                 // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            return x;
        }

        private static int DecodeMorton2X(int code)
        {
            return Compact1By1(code >> 0);
        }

        private static int DecodeMorton2Y(int code)
        {
            return Compact1By1(code >> 1);
        }

        public static byte[] UnswizzleTexture(byte[] pixelData, int width, int height, PixelFormat pixelFormat)
        {
            int bytesPerPixel = (Bitmap.GetPixelFormatSize(pixelFormat) / 8);
            byte[] unswizzled = new byte[pixelData.Length];

            for (int i = 0; i < width * height; i++)
            {
                int min = width < height ? width : height;
                int k = (int)Math.Log(min, 2);

                int x, y;
                if (height < width)
                {
                    // XXXyxyxyx → XXXxxxyyy
                    int j = i >> (2 * k) << (2 * k)
                        | (DecodeMorton2Y(i) & (min - 1)) << k
                        | (DecodeMorton2X(i) & (min - 1)) << 0;
                    x = j / height;
                    y = j % height;
                }
                else
                {
                    // YYYyxyxyx → YYYyyyxxx
                    int j = i >> (2 * k) << (2 * k)
                        | (DecodeMorton2X(i) & (min - 1)) << k
                        | (DecodeMorton2Y(i) & (min - 1)) << 0;
                    x = j % width;
                    y = j / width;
                }

                if (y >= height || x >= width) continue;

                Buffer.BlockCopy(pixelData, i * bytesPerPixel, unswizzled, ((y * width) + x) * bytesPerPixel, bytesPerPixel);
            }

            return unswizzled;
        }

        #endregion
    }
}
