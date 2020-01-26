using RobotDriver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace RobotClient
{
    class SocketClient
    {
        public delegate void OnImageReceivedEvent(byte[] image);

        public event OnImageReceivedEvent OnImageReceived;

        string _host;
        Socket socket;
        public SocketClient(string host)
        {
            _host = host;
        }

        public void Connect()
        {
            //IPHostEntry ipHostInfo = Dns.GetHostEntry(_host);
            //IPAddress ipAddress = ipHostInfo.AddressList.Where(i=>i.AddressFamily==AddressFamily.InterNetwork).FirstOrDefault();
            IPAddress ipAddress = new IPAddress(new byte[] { 192, 168, 0, 1 });
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 9997);

            socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.Connect(remoteEP);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            Task.Run(() => ReceiverThread(socket));
        }

        void ReceiverThread(Socket s)
        {
            while (true)
            {
                byte[] buf = new byte[4];
                RcvAll(s, buf);

                var len = BitConverter.ToInt32(buf, 0);
                byte[] buf2 = new byte[len];
                RcvAll(s, buf2);
                var i = Image.Parser.ParseFrom(buf2);
                OnImageReceived?.Invoke(i.Data.ToByteArray());
            }
        }

        void RcvAll(Socket s, byte[] buf)
        {
            int pos = 0;

            while (pos < buf.Length)
            {
                pos += s.Receive(buf, pos, buf.Length - pos, SocketFlags.None);
            }
        }

        public void SendCommand(string cmd)
        {
            Steering r = new Steering();
            r.Command = cmd;
            byte[] msg = r.ToByteArray();
            socket.Send(BitConverter.GetBytes(msg.Length));
            socket.Send(msg);
        }
    }
}
