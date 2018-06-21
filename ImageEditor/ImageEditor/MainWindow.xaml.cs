using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageEditor.Annotations;

namespace ImageEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow :INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        string _filename;
        TrImage _image;
        private double _scale=1;
        private TrImage.MaskDrawType MaskType;
        private int MaskLayer;

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

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadImageToView();
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
            if(Maskthreshold==null)
                return;
            
            CultureInfo invC = CultureInfo.InvariantCulture;

            if (float.TryParse(Maskthreshold.Text,NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, invC, out float threshold))
            {
                _image = new TrImage();
                _image.LoadImage(_filename);
                _image.CalculateCatecoryMask(threshold);
                Image.Source = ImageSourceForBitmap(_image.DrawMask(MaskType, MaskLayer));
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


        public double Scale
        {
            get { return _scale; }
            set
            {
                if (value.Equals(_scale)) return;
                _scale = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void rb_OnChecked(object sender, RoutedEventArgs e)
        {
            if(rbDimBg==null || rbOnlyBg == null || rbDimPlant == null || rbOnlyPlant == null )
                return;
            
            if (rbDimBg.IsChecked ?? false)
            {
                MaskType = TrImage.MaskDrawType.DimOthers;
                MaskLayer = 1;
            }


            if (rbOnlyBg.IsChecked ?? false)
            {
                MaskType = TrImage.MaskDrawType.SelectedOnly;
                MaskLayer = 1;
            }

            if (rbDimPlant.IsChecked ?? false)
            {
                MaskType = TrImage.MaskDrawType.DimOthers;
                MaskLayer = 0;
            }


            if (rbOnlyPlant.IsChecked ?? false)
            {
                MaskType = TrImage.MaskDrawType.SelectedOnly;
                MaskLayer = 0;
            }

            if (rbNone.IsChecked ?? false)
            {
                MaskType = TrImage.MaskDrawType.None;
                MaskLayer = 0;
            }
            

            LoadImageToView();
        }
    }
}
