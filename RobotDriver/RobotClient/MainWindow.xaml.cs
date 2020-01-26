using RobotDriver;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RobotClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        XInputController input = new XInputController();
        ImageConverter _imageConverter = new ImageConverter();

        SocketClient sock;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //sock = new SocketClient("localhost");
            sock = new SocketClient("192.168.98.52");
            sock.OnImageReceived += Sock_OnImageReceived;
            sock.Connect();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            input.Update();
            Console.WriteLine("LT"+input.leftTrigger);

            string cmd =
                "\r" + (input.leftTrigger>200?"Z\r":"") +
                 "W" + String.Format("{0:+000;-000;+000}", -input.LeftThumbY*2.55) + "\r" +
                 "S" + String.Format("{0:+000;-000;+000}", -input.LeftThumbX/3) + "\r";

            Console.WriteLine(cmd);
            sock.SendCommand(cmd);
        }

        private void Sock_OnImageReceived(byte[] image)
        {

            Bitmap bm = (Bitmap)_imageConverter.ConvertFrom(image);
            Dispatcher.Invoke(() =>
            {
                img.Source = BitmapToImageSource(bm);
            });

        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
