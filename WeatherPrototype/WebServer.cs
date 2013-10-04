using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;
using SecretLabs.NETMF.Hardware.Netduino;

namespace AdvancedButtonApp
{
    public class WebServer : IDisposable
    {
        //open connection to onbaord led so we can blink it with every request
        private readonly OutputPort led = new OutputPort(Pins.ONBOARD_LED, false);
        private readonly Socket socket;

        public WebServer()
        {
            //Initialize Socket class
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //Request and bind to an IP from DHCP server
            socket.Bind(new IPEndPoint(IPAddress.Any, 80));
            //Debug print our IP address
            Debug.Print(NetworkInterface.GetAllNetworkInterfaces()[0].IPAddress);
            //Start listen for web requests
            socket.Listen(10);
            ListenForRequest();
        }

        public void ListenForRequest()
        {
            while (true)
            {
                using (var clientSocket = socket.Accept())
                {
                    var bytesReceived = clientSocket.Available;

                    if (bytesReceived > 0)
                    {
                        //Get request
                        var buffer = new byte[bytesReceived];
                        clientSocket.Receive(buffer, bytesReceived, SocketFlags.None);

                        var request = new string(Encoding.UTF8.GetChars(buffer));
                        Debug.Print(request);

                        var firstLine = request.Substring(0, request.IndexOf('\n')); //Example "GET /activatedoor HTTP/1.1"
                        var words = firstLine.Split(' '); //Split line into words
                        var command = string.Empty;

                        if (words.Length > 2)
                        {
                            command = words[1].TrimStart('/'); //Second word is our command - remove the forward slash
                        }

                        string header;

                        switch (command.ToLower())
                        {
                            case "raw":
                                //Compose a response
                                var fileInfo = new FileInfo(@"\SD\Sensor.log");

                                header = "HTTP/1.0 200 OK\r\nContent-Type: text; charset=utf-8\r\nContent-Length: " + fileInfo.Length + "\r\nConnection: close\r\n\r\n";
                                clientSocket.Send(Encoding.UTF8.GetBytes(header), header.Length, SocketFlags.None);

                                using (var file = new FileStream(@"\SD\Sensor.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    var block = new byte[1024];
                                    int size = file.Read(block, 0, block.Length);

                                    while (size > 0)
                                    {
                                        clientSocket.Send(block, size, SocketFlags.None);
                                        size = file.Read(block, 0, block.Length);
                                    }
                                }

                                break;
                            case "chart":
                                break;
                            default:
                                //Did not recognize command
                                const string response = "Bad command";
                                header = "HTTP/1.0 200 OK\r\nContent-Type: text; charset=utf-8\r\nContent-Length: " + response.Length.ToString() + "\r\nConnection: close\r\n\r\n";
                                clientSocket.Send(Encoding.UTF8.GetBytes(header), header.Length, SocketFlags.None);
                                clientSocket.Send(Encoding.UTF8.GetBytes(response), response.Length, SocketFlags.None);
                                break;

                        }

                        //Blink the onboard LED
                        led.Write(true);
                        Thread.Sleep(150);
                        led.Write(false);
                    }
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Close();
            }
        }

        ~WebServer()
        {
            Dispose();
        }

        #endregion
    }
}