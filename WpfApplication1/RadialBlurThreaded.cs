using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace WpfRadialBlur
{
    public class RadialBlurThreaded
    {
        private const int ThreadNum = 4;
        private readonly object _imgLock = new object();
        private readonly object _outLock = new object();

        public RadialBitmap ConvertToRadialThreaded(Bitmap bmp)
        {
            try
            {
                var destImg = new Bitmap((bmp.Height + bmp.Width - 2) * 2,
                    (int) Math.Ceiling(Math.Sqrt(bmp.Height * bmp.Height + bmp.Width * bmp.Width) / 2));

                var rect = new Rectangle(0, 0, destImg.Width, destImg.Height);
                var data = destImg.LockBits(rect, ImageLockMode.ReadWrite, destImg.PixelFormat);
                var depth = Image.GetPixelFormatSize(data.PixelFormat) / 8; //bytes per pixel

                var buffer = new byte[data.Width * data.Height * depth];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                var centerPoint = new Vector(bmp.Width / 2.0, bmp.Height / 2.0);

                var pixels = new ConcurrentQueue<DVector<int>>();

                for (var x = 0; x < destImg.Width; x++)
                {
                    var direcrionVector = DirectionVector(x, bmp, centerPoint);

                    for (var y = 0; y < destImg.Height; y++)
                    {
                        pixels.Enqueue(new DVector<int>(x, y, direcrionVector));
                    }
                }

                using (var countdownEvent = new CountdownEvent(ThreadNum))
                {
                    for (var i = 0; i < ThreadNum; i++)
                    {
                        ThreadPool.QueueUserWorkItem(x =>
                        {
                            ConvertToRadialThreadProcess(bmp, buffer, centerPoint, pixels,
                                destImg.Width,
                                depth);
                            // ReSharper disable once AccessToDisposedClosure
                            countdownEvent.Signal();
                        });
                    }

                    countdownEvent.Wait();
                }

                //Copy the buffer back to image
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);

                destImg.UnlockBits(data);

                return new RadialBitmap(destImg, bmp.Width, bmp.Height);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                throw;
            }
        }

        private void ConvertToRadialThreadProcess(Bitmap bmp, byte[] destImg, Vector centerPoint,
            ConcurrentQueue<DVector<int>> pixels, int newWidth, int depth)
        {
            try
            {
                while (!pixels.IsEmpty)
                {
                    if (pixels.TryDequeue(out var workingPixel))
                    {
                        var offset = (workingPixel.y * newWidth + workingPixel.x) * depth;
                        var point = centerPoint + workingPixel.Direction * (workingPixel.y + 1);
                        var c = InterperlatePointFromImg(bmp, point);
                        lock (_outLock)
                        {
                            try
                            {
                                if (depth == 3)
                                {
                                    destImg[offset] = c.R;
                                    destImg[offset + 1] = c.G;
                                    destImg[offset + 2] = c.B;
                                }
                                else if (depth == 4)
                                {
                                    destImg[offset] = c.R;
                                    destImg[offset + 1] = c.G;
                                    destImg[offset + 2] = c.B;
                                    destImg[offset + 3] = c.A;
                                }
                                else
                                {
                                    MessageBox.Show("image colour depth = " + depth +
                                                    ". Only depths 3 and 4 are implemented.");
                                }
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("X = " + workingPixel.x + " Width = " + newWidth + "\nY = " +
                                                workingPixel.y + " Hight = " + destImg.Length / (newWidth * depth) +
                                                "\n" + e.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + e.StackTrace);
            }
        }

        public Bitmap Blur(Bitmap bmp, int str)
        {
            var toReturn = ConvertToRadialThreaded(bmp);
            toReturn.img = OneDimentionalBoxBlur(toReturn.img, str);
            return ConvertFromRadial(toReturn);
        }

        private Color InterperlatePointFromImg(Bitmap img, Vector p)
        {
            // reference pixels
            int width, height;
            var x0 = int.MaxValue;
            var x1 = int.MaxValue;
            var y0 = int.MaxValue;
            var y1 = int.MaxValue;
            lock (_imgLock)
            {
                if (p.X < 0 || p.X >= img.Width || p.Y < 0 || p.Y >= img.Height)
                {
                    return Color.Black;
                }

                width = img.Width;
                height = img.Height;
            }

            try
            {
                x0 = ((int) Math.Floor(p.X - 0.5) % width + width) % width;
                x1 = ((int) Math.Floor(p.X + 0.5) % width + width) % width;
                y0 = ((int) Math.Floor(p.Y - 0.5) % height + height) % height;
                y1 = ((int) Math.Floor(p.Y + 0.5) % height + height) % height;

                Color p00, p10, p01, p11;

                lock (_imgLock)
                {
                    p00 = img.GetPixel(x0, y0);
                    p10 = img.GetPixel(x1, y0);
                    p01 = img.GetPixel(x0, y1);
                    p11 = img.GetPixel(x1, y1);
                }

                var fx0 = 1.5 + (int) Math.Floor(p.X - 0.5) - p.X;
                var fx1 = 1 - fx0;
                var fy0 = 1.5 + (int) Math.Floor(p.Y - 0.5) - p.Y;
                var fy1 = 1 - fy0;

                var a = (int) Math.Floor(fx0 * fy0 * p00.A + fx1 * fy0 * p10.A + fx0 * fy1 * p01.A + fx1 * fy1 * p11.A);
                var r = (int) Math.Floor(fx0 * fy0 * p00.R + fx1 * fy0 * p10.R + fx0 * fy1 * p01.R + fx1 * fy1 * p11.R);
                var g = (int) Math.Floor(fx0 * fy0 * p00.G + fx1 * fy0 * p10.G + fx0 * fy1 * p01.G + fx1 * fy1 * p11.G);
                var b = (int) Math.Floor(fx0 * fy0 * p00.B + fx1 * fy0 * p10.B + fx0 * fy1 * p01.B + fx1 * fy1 * p11.B);

                return Color.FromArgb(a, r, g, b);
            }
            catch (Exception e)
            {
                MessageBox.Show("P = (" + p.X + "," + p.Y + ")\nx0 = " + x0 + " x1 = " + x1 + " y0 = " + y0 + " y1 = " +
                                y1 + "\n" + e.Message);
                return Color.Black;
            }
        }

        public Bitmap ConvertFromRadial(RadialBitmap rbmp)
        {
            var destImg = new Bitmap(rbmp.originalWidth, rbmp.originalHight);
            var pixelAcc = new double[destImg.Width, destImg.Height, 4];
            var pixelSaturation = new double[destImg.Width, destImg.Height];
            var centerPoint = new Vector(destImg.Width / 2.0, destImg.Height / 2.0);

//            var rect = new Rectangle(0, 0, destImg.Width, destImg.Height);
//            var data = destImg.LockBits(rect, ImageLockMode.ReadWrite, destImg.PixelFormat);
//            var depth = Image.GetPixelFormatSize(data.PixelFormat) / 8; //bytes per pixel

//            var buffer = new byte[data.Width * data.Height * depth];
//            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            var pixels = new ConcurrentQueue<DVector<int>>();
            for (var x = 0; x < rbmp.width; x++)
            {
                var directionVector = DirectionVector(x, destImg, centerPoint);
                for (var y = 0; y < rbmp.hight; y++)
                {
                    var pixelLocation = y * directionVector;
                    pixels.Enqueue(new DVector<int>(x, y, pixelLocation));
                }
            }

            using (var countdownEvent = new CountdownEvent(ThreadNum))
            {
                for (var i = 0; i < ThreadNum; i++)
                {
                    ThreadPool.QueueUserWorkItem(x =>
                    {
                        DisperceRadial(rbmp, pixels, pixelAcc, pixelSaturation);
                        // ReSharper disable once AccessToDisposedClosure
                        countdownEvent.Signal();
                    });
                }

                countdownEvent.Wait();
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

        private static Vector DirectionVector(int x, Image destImg, Vector centerPoint)
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

        private void DisperceRadial(RadialBitmap rbmp, ConcurrentQueue<DVector<int>> pixels, double[,,] output,
            double[,] pixelSaturation)
        {
            try
            {
                while (!pixels.IsEmpty)
                {
                    if (pixels.TryDequeue(out var workingPixel))
                    {
                        var px = workingPixel.Direction.X;
                        var py = workingPixel.Direction.Y;
                        int l1, l2;
                        lock (output)
                        {
                            l1 = output.GetLength(0);
                            l2 = output.GetLength(1);
                        }

                        Color cp;
                        lock (_imgLock)
                        {
                            cp = rbmp.img.GetPixel(workingPixel.x, workingPixel.y);
                        }

                        if (px < -l1 / 2.0 - 1 || px > l1 / 2.0 + 1 ||
                            py < -l2 / 2.0 - 1 || py > l2 / 2.0 + 1)
                        {
                            return;
                        }

                        var dx0 = 1.5 + Math.Floor(px - 0.5) - px;
                        var dx1 = 1 - dx0;
                        var dy0 = 1.5 + Math.Floor(py - 0.5) - py;
                        var dy1 = 1 - dy0;

                        var x0 = ((int) Math.Floor(px - 0.5) % l1 + l1) % l1;
                        var x1 = ((int) Math.Floor(px + 0.5) % l1 + l1) % l1;
                        var y0 = ((int) Math.Floor(py - 0.5) % l2 + l2) % l2;
                        var y1 = ((int) Math.Floor(py + 0.5) % l2 + l2) % l2;


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
                        lock (_outLock)
                        {
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
                            MessageBox.Show(
                                (output[x0, y0, 0] += c00[0] / pixelSaturation[x0, y0]).ToString(CultureInfo
                                    .InvariantCulture));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + "\n" + e.StackTrace);
                throw;
            }
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
            for (var x = 0;
                x < bmp.Width;
                x++)
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

    public class DVector<T>
    {
        public readonly T x;
        public readonly T y;
        public Vector Direction { get; }

        public DVector(T x, T y, Vector d)
        {
            this.x = x;
            this.y = y;
            Direction = d;
        }
    }
}