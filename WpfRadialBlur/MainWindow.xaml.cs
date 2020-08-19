using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WpfRadialBlur
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLoadFromFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                var fileUri = new Uri(openFileDialog.FileName);
                ImgDynamic.Source = new BitmapImage(fileUri);
            }
        }

        private void BtnLoadFromResource_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var img = Image.FromFile("cat.png");
                Bitmap bmp = null;
                bmp = new Bitmap(img);
                bmp = RadialBlur.Blur(bmp, 10);

                ImgDynamic.Source = ConvertImageToImgSrc(bmp);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void BtnLoadFromResource_Click2(object sender, RoutedEventArgs e)
        {
            try
            {
                var img = Image.FromFile("cat.png");
                Bitmap bmp = null;
                bmp = new Bitmap(img);
                bmp = RadialBlurOld.Blur(bmp, 10);

                ImgDynamic.Source = ConvertImageToImgSrc(bmp);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private static BitmapImage ConvertImageToImgSrc(Image src)
        {
            var ms = new MemoryStream();
            src.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            var image = new BitmapImage();
            image.BeginInit();
            ms.Seek(0, SeekOrigin.Begin);
            image.StreamSource = ms;
            image.EndInit();
            return image;
        }

        private void BtnLoadFromResourceThreaded_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var img = Image.FromFile("cat.png");
                Bitmap bmp =null;
                var rbt = new RadialBlurThreaded();
                var sw =new Stopwatch();
                sw.Start();
                for (var i = 0; i < 10; i++)
                {
                    bmp = new Bitmap(img);
                    bmp = rbt.Blur(bmp,10);
                }

                MessageBox.Show(sw.ElapsedMilliseconds / 1000.0 + "");
                sw.Stop();
                ImgDynamic.Source = ConvertImageToImgSrc(bmp);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }
    }
}