using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hardware_Specs_Client
{
    public static class SendData
    {
        public static void Send(string Message, string ip, int port)
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPAddress serverAddr = IPAddress.Parse(ip);
            IPEndPoint endPoint = new IPEndPoint(serverAddr, port);
            byte[] send_buffer = Encoding.ASCII.GetBytes(Message);

            sock.SendTo(send_buffer, endPoint);

            Console.WriteLine($"Data Send to {ip}");
        }
    }
}
