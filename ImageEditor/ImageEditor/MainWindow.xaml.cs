using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEditor
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

        string _filename;
        TrImage _image;

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".jpg",
                Filter =
                    "JPG Files (*.jpg)|*.jpg|JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|GIF Files (*.gif)|*.gif"
            };


            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                _filename = dlg.FileName;
                Filename.Text = _filename;
                LoadImageToView();
            }
        }


        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            NavigateImage(-1);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            NavigateImage(1);
        }

        void NavigateImage(int direction)
        {
            string currentFileName = Path.GetFileName(_filename);
            string dir = Path.GetDirectoryName(_filename);
            if (dir != null)
            {
                string[] files = Directory.GetFiles(dir, "*.jpg").Select(s => Path.GetFileName(s)).OrderBy(i => i).ToArray();
                var index = Array.FindIndex(files, i => string.Compare(i, currentFileName, StringComparison.OrdinalIgnoreCase) == 0);
                if (index + direction > 0 && index + direction < files.Length)
                {
                    _filename = Path.Combine(dir, files[index - direction]);
                    LoadImageToView();
                }
            }
        }

        private void LoadImageToView()
        {
            CultureInfo invC = CultureInfo.InvariantCulture;

            if (float.TryParse(Maskthreshold.Text,NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, invC, out float threshold))
            {
                _image = new TrImage();
                _image.LoadImage(_filename);
                _image.CalculateCatecoryMask(threshold);
                Image.Source = ImageSourceForBitmap(_image.DrawMask(TrImage.MaskDrawType.DimOthers, 0));
            }
            else
                MessageBox.Show("Bad threshold");
        }


        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }
    }
}
