using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using Image = System.Drawing.Image;

namespace WpfRadialBlur
{
    public static class RadialBlur
    {
        public static RadialBitmap ConvertToRadial(Bitmap bmp)
        {
            var destImg = new Bitmap((bmp.Height + bmp.Width - 2) * 2,
                (int) Math.Ceiling(Math.Sqrt(bmp.Height * bmp.Height + bmp.Width * bmp.Width) / 2));

            var rect = new Rectangle(0, 0, destImg.Width, destImg.Height);
            var data = destImg.LockBits(rect, ImageLockMode.ReadWrite, destImg.PixelFormat);
            var depth = Image.GetPixelFormatSize(data.PixelFormat) / 8; //bytes per pixel

            var buffer = new byte[data.Width * data.Height * depth];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            var centerPoint = new Vector(bmp.Width / 2.0, bmp.Height / 2.0);
            
            for (var x = 0; x < destImg.Width; x++)
            {
                var directionVector = DirectionVector(x, bmp, centerPoint);

                for (var y = 0; y < destImg.Height; y++)
                {
                    var point = centerPoint + directionVector * (y + 1);
                    var c = InterperlatePointFromImg(bmp, point);
                    if (depth == 4)
                    {
                        try
                        {
                            var offset = (y * destImg.Width + x) * (depth);
                            buffer[offset] = c.B;
                            buffer[offset + 1] = c.G;
                            buffer[offset + 2] = c.R;
                            buffer[offset + 3] = c.A;
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("X = " + x + " Width = " + destImg.Width + "\nY = " + y +
                                            " Hight = " +
                                            destImg.Height + "\n" + e.Message);
                        }
                    }
                    else if (depth == 3)
                    {
                        try
                        {
                            var offset = (y * destImg.Width + x) * (depth - 1);
                            buffer[offset] = c.R;
                            buffer[offset + 1] = c.G;
                            buffer[offset + 2] = c.B;
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show("X = " + x + " Width = " + destImg.Width + "\nY = " + y +
                                            " Hight = " +
                                            destImg.Height + "\n" + e.Message);
                        }
                    }
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);

            destImg.UnlockBits(data);

            return new RadialBitmap(destImg, bmp.Width, bmp.Height);
        }

        public static Bitmap Blur(Bitmap bmp, int str)
        {
            var toReturn = ConvertToRadial(bmp);
            toReturn.img = OneDimentionalBoxBlur(toReturn.img, str);
            return ConvertFromRadial(toReturn);
        }

        private static Color InterperlatePointFromImg(Bitmap img, Vector p)
        {
            // reference pixels

            if (p.X < 0 || p.X >= img.Width || p.Y < 0 || p.Y >= img.Height)
            {
                return Color.Black;
            }

            var x0 = int.MaxValue;
            var x1 = int.MaxValue;
            var y0 = int.MaxValue;
            var y1 = int.MaxValue;
            try
            {
                x0 = ((int) Math.Floor(p.X - 0.5) % img.Width + img.Width) % img.Width;
                x1 = ((int) Math.Floor(p.X + 0.5) % img.Width + img.Width) % img.Width;
                y0 = ((int) Math.Floor(p.Y - 0.5) % img.Height + img.Height) % img.Height;
                y1 = ((int) Math.Floor(p.Y + 0.5) % img.Height + img.Height) % img.Height;

                var p00 = img.GetPixel(x0, y0);
                var p10 = img.GetPixel(x1, y0);
                var p01 = img.GetPixel(x0, y1);
                var p11 = img.GetPixel(x1, y1);

                var fx0 = 1.5 + (int) Math.Floor(p.X - 0.5) - p.X;
                var fx1 = 1 - fx0;
                var fy0 = 1.5 + (int) Math.Floor(p.Y - 0.5) - p.Y;
                var fy1 = 1 - fy0;

                var a = (int) Math.Floor(fx0 * fy0 * p00.A + fx1 * fy0 * p10.A + fx0 * fy1 * p01.A + fx1 * fy1 * p11.A);
                var r = (int) Math.Floor(fx0 * fy0 * p00.R + fx1 * fy0 * p10.R + fx0 * fy1 * p01.R + fx1 * fy1 * p11.R);
                var g = (int) Math.Floor(fx0 * fy0 * p00.G + fx1 * fy0 * p10.G + fx0 * fy1 * p01.G + fx1 * fy1 * p11.G);
                var b = (int) Math.Floor(fx0 * fy0 * p00.B + fx1 * fy0 * p10.B + fx0 * fy1 * p01.B + fx1 * fy1 * p11.B);

//                var a = (int) Math.Floor(Blurp(p00.A, p10.A, p01.A, p11.A, p.X-(int) Math.Floor(p.X - 0.5)-0.5, p.Y-(int) Math.Floor(p.Y - 0.5)-0.5));
//                var r = (int) Math.Floor(Blurp(p00.R, p10.G, p01.R, p11.R, p.X-(int) Math.Floor(p.X - 0.5)-0.5, p.Y-(int) Math.Floor(p.Y - 0.5)-0.5));
//                var g = (int) Math.Floor(Blurp(p00.G, p10.G, p01.G, p11.G, p.X-(int) Math.Floor(p.X - 0.5)-0.5, p.Y-(int) Math.Floor(p.Y - 0.5)-0.5));
//                var b = (int) Math.Floor(Blurp(p00.B, p10.B, p01.B, p11.B, p.X-(int) Math.Floor(p.X - 0.5)-0.5, p.Y-(int) Math.Floor(p.Y - 0.5)-0.5));
                return Color.FromArgb(a, r, g, b);
            }
            catch (Exception e)
            {
                MessageBox.Show("P = (" + p.X + "," + p.Y + ")\nx0 = " + x0 + " x1 = " + x1 + " y0 = " + y0 + " y1 = " +
                                y1 + "\n" + e.Message);
                return Color.Black;
            }
        }

        public static Bitmap ConvertFromRadial(RadialBitmap rbmp)
        {
            var destImg = new Bitmap(rbmp.originalWidth, rbmp.originalHight);
            var pixelAcc = new double[destImg.Width, destImg.Height, 4];
            var pixelSaturation = new double[destImg.Width, destImg.Height];
            var centerPoint = new Vector(destImg.Width / 2.0, destImg.Height / 2.0);

            for (var x = 0; x < rbmp.width; x++)
            {
                var directionVector = DirectionVector(x, destImg, centerPoint);
                for (var y = 0; y < rbmp.hight; y++)
                {
                    var pixelLocation = y * directionVector;
                    DisperceRadial(rbmp.img.GetPixel(x, y), pixelLocation, pixelAcc, pixelSaturation);
                }
            }

            for (var x = 0; x < destImg.Width; x++)
            {
                for (var y = 0; y < destImg.Height; y++)
                {
                    try
                    {
                        destImg.SetPixel((x + (int) centerPoint.X) % destImg.Width,
                            (y + (int) centerPoint.Y) % destImg.Height,
                            Color.FromArgb((int) Math.Floor(pixelAcc[x, y, 0] / pixelSaturation[x, y]),
                                (int) Math.Floor(pixelAcc[x, y, 1] / pixelSaturation[x, y]),
                                (int) Math.Floor(pixelAcc[x, y, 2] / pixelSaturation[x, y]),
                                (int) Math.Floor(pixelAcc[x, y, 3] / pixelSaturation[x, y])));
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
                        destImg.SetPixel(x, y, Color.HotPink);
                    }
                }
            }

            return destImg;
        }

        private static Vector DirectionVector(int x, Bitmap destImg, Vector centerPoint)
        {
            Vector directionVector;
            if (x < destImg.Width)
            {
                directionVector = new Vector(x, 0) - centerPoint;
            }
            else if (x < destImg.Width + destImg.Height - 1)
            {
                directionVector = new Vector(destImg.Width - 1, x - (destImg.Width - 1)) - centerPoint;
            }
            else if (x < 2 * destImg.Width + destImg.Height - 2)
            {
                directionVector = new Vector(2 * destImg.Width + destImg.Height - 3 - x, destImg.Height - 1) -
                                  centerPoint;
            }
            else
            {
                directionVector = new Vector(0, 2 * destImg.Height + 2 * destImg.Width - 4 - x) - centerPoint;
            }

            directionVector.Normalize();
            return directionVector;
        }

        public static void DisperceRadial(Color cp, Vector pp, double[,,] output, double[,] pixelSaturation)
        {
            if (pp.X < -output.GetLength(0) / 2.0 - 1 || pp.X > output.GetLength(0) / 2.0 + 1 ||
                pp.Y < -output.GetLength(1) / 2.0 - 1 || pp.Y > output.GetLength(1) / 2.0 + 1)
            {
                return;
            }

            var dx0 = 1.5 + Math.Floor(pp.X - 0.5) - pp.X;
            var dx1 = 1 - dx0;
            var dy0 = 1.5 + Math.Floor(pp.Y - 0.5) - pp.Y;
            var dy1 = 1 - dy0;

            var x0 = ((int) Math.Floor(pp.X - 0.5) % output.GetLength(0) + output.GetLength(0)) % output.GetLength(0);
            var x1 = ((int) Math.Floor(pp.X + 0.5) % output.GetLength(0) + output.GetLength(0)) % output.GetLength(0);
            var y0 = ((int) Math.Floor(pp.Y - 0.5) % output.GetLength(1) + output.GetLength(1)) % output.GetLength(1);
            var y1 = ((int) Math.Floor(pp.Y + 0.5) % output.GetLength(1) + output.GetLength(1)) % output.GetLength(1);


            double[] c00 =
            {
                cp.A * dx0 * dy0,
                cp.R * dx0 * dy0,
                cp.G * dx0 * dy0,
                cp.B * dx0 * dy0
            };
            double[] c10 =
            {
                cp.A * dx1 * dy0,
                cp.R * dx1 * dy0,
                cp.G * dx1 * dy0,
                cp.B * dx1 * dy0
            };
            double[] c01 =
            {
                cp.A * dx0 * dy1,
                cp.R * dx0 * dy1,
                cp.G * dx0 * dy1,
                cp.B * dx0 * dy1
            };
            double[] c11 =
            {
                cp.A * dx1 * dy1,
                cp.R * dx1 * dy1,
                cp.G * dx1 * dy1,
                cp.B * dx1 * dy1
            };
            for (var i = 0; i < 4; i++)
            {
                output[x0, y0, i] += c00[i];
                output[x1, y0, i] += c10[i];
                output[x0, y1, i] += c01[i];
                output[x1, y1, i] += c11[i];
            }

            pixelSaturation[x0, y0] += dx0 * dy0;
            pixelSaturation[x1, y0] += dx1 * dy0;
            pixelSaturation[x0, y1] += dx0 * dy1;
            pixelSaturation[x1, y1] += dx1 * dy1;

//            if (x0== 0 &&y0==0)
//            {
//                MessageBox.Show("dx0 = "+dx0+"\n"+
//                                "dx1 = "+dx1+"\n"+
//                                "dy0 = "+dy0+"\n"+
//                                "dy1 = "+dy1+"\n"+
//                                "cpA = "+cp.A+"\n"+
//                                "x0 = "+x0+"\n"+
//                                "y0 = "+y0+"\n"+
//                                "px = "+pp.X+"\n"+
//                                "py = "+pp.Y+"\n");
//            }
        }

/*
        /// <summary>
        /// Finds the value bilinearly interpreted between 4 values modeled on a unit square.
        /// </summary>
        /// <param name="p00">Double representing the value at point (0,0) on the unit square.</param>
        /// <param name="p10">Double representing the value at point (1,0) on the unit square.</param>
        /// <param name="p01">Double representing the value at point (0,1) on the unit square.</param>
        /// <param name="p11">Double representing the value at point (1,1) on the unit square.</param>
        /// <param name="x">Double between 0 and 1, defaults to 0.5.</param>
        /// <param name="y">Double between 0 and 1, defaults to 0.5.</param>
        /// <returns>Double between the max and min values of p00, p10, p01, and p11.</returns>
        private static double Blurp(double p00, double p10, double p01, double p11, double x, double y)
        {
            return p00 * (1 - x) * (1 - y) + p10 * x * (1 - y) + p01 * (1 - x) * y + p11 * x * y;
        }
*/

        private static Bitmap OneDimentionalBoxBlur(Bitmap bmp, int str)
        {
            var newBmp = new Bitmap(bmp.Width, bmp.Height);
            for (var x = 0; x < bmp.Width; x++)
            {
                for (var y = 0; y < bmp.Height; y++)
                {
                    var acc = new double[4];
                    for (var i = 0; i < str * 2 + 1; i++)
                    {
                        acc[0] += bmp.GetPixel((x - str + i + bmp.Width) % bmp.Width, y).A / (str * 2.0 + 1);
                        acc[1] += bmp.GetPixel((x - str + i + bmp.Width) % bmp.Width, y).R / (str * 2.0 + 1);
                        acc[2] += bmp.GetPixel((x - str + i + bmp.Width) % bmp.Width, y).G / (str * 2.0 + 1);
                        acc[3] += bmp.GetPixel((x - str + i + bmp.Width) % bmp.Width, y).B / (str * 2.0 + 1);
                    }

                    newBmp.SetPixel(x, y,
                        Color.FromArgb((int) Math.Floor(acc[0]), (int) Math.Floor(acc[1]), (int) Math.Floor(acc[2]),
                            (int) Math.Floor(acc[3])));
                }
            }

            return newBmp;
        }
    }


    public class RadialBitmap
    {
        public Bitmap img;
        public readonly int originalWidth;
        public readonly int originalHight;
        public readonly int width;
        public readonly int hight;

        public RadialBitmap(Bitmap img, int originalWidth, int originalHight)
        {
            this.img = img;
            this.originalHight = originalHight;
            this.originalWidth = originalWidth;
            width = img.Width;
            hight = img.Height;
        }
    }
}