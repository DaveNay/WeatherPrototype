using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using NetduinoPlusTesting;
using SecretLabs.NETMF.Hardware.Netduino;
using Math = System.Math;

/* NOTE: make sure you change the deployment target from the Emulator to your Netduino before running this
 * Netduino sample app.  To do this, select "Project menu > AdvancedButtonApp Properties > .NET Micro Framework" and 
 * then change the Transport type to USB.  Finally, close the AdvancedButtonApp properties tab to save these settings. */

namespace AdvancedButtonApp
{
    public class Program
    {
        private const int UpdateFrequency = 15;
        private static readonly OutputPort Led = new OutputPort(Pins.GPIO_PIN_D13, false);
        private static BMP085 _sensor;
        private static Timer _sensorTimer;
        private static double _elevation = 263.0;

        public static void Main()
        {
            // write your code here
            Utility.SetLocalTime(NtpTime("pool.ntp.org"));

            //DirectoryInfo rootDirectory = new DirectoryInfo(@"\SD\");
            //RecurseFolders(rootDirectory);

            FlashLed(Led, 3);

            _sensor = new BMP085(0x77, BMP085.DeviceMode.UltraHighResolution);

            // Take new measurements every 30 seconds.
            _sensorTimer = new Timer(TakeMeasurements, null, 0, UpdateFrequency*1000);

//            Thread.Sleep(Timeout.Infinite);
            var webServer = new WebServer();
            webServer.ListenForRequest();
        }

        private static void TakeMeasurements(object state)
        {
            Debug.Print(string.Empty);
            Debug.Print(DateTime.Now.ToString("G"));
            Debug.Print("BMP085 Pascal: " + _sensor.Pascal);
            Debug.Print("BMP085 InchesMercury: " + _sensor.InchesMercury.ToString("F2"));
            Debug.Print("BMP085 Temp*C: " + _sensor.Celsius.ToString("F2"));
            Debug.Print("BMP085 Temp*F: " + _sensor.Fahrenheit.ToString("F2"));

            double temp = 1 - ((0.0065 * _elevation) / (_sensor.Celsius + (0.0065 * _elevation) + 273.15));
            double inchesHgSeaLevel = _sensor.InchesMercury*Math.Pow(temp, -5.257);

            DateTime timestamp = DateTime.Now;
            string url = @"http://rtupdate.wunderground.com/weatherstation/updateweatherstation.php?ID=KILWATER5&PASSWORD=cycewimi&dateutc=" +
                         timestamp.Year + "-" + timestamp.Month + "-" + timestamp.Day + "+" + timestamp.Hour + "%3A" + timestamp.Minute + "%3A" +
                         timestamp.Second + "&tempf=" + _sensor.Fahrenheit + "&baromin=" + inchesHgSeaLevel +
                         "&action=updateraw&realtime=1&rtfreq=" + UpdateFrequency;

            SendToWeatherunderground(url);
//            try
//            {
//                using (var file = new StreamWriter(@"\SD\Sensor.log", true))
//                {
//                    file.WriteLine(DateTime.Now.ToString("G") + "," + _sensor.Pascal + "," + _sensor.InchesMercury.ToString("F2") + "," +
//                                   _sensor.Celsius.ToString("F2") + "," + _sensor.Fahrenheit.ToString("F2"));
//                }
//            }
//            catch (Exception e)
//            {
//                Debug.Print(e.Message);
//            }

            FlashLed(Led, 1);
        }

        private static void SendToWeatherunderground(string url)
        {
            Debug.Print(url);
            // Create an HTTP Web request.
            var request = WebRequest.Create(url) as HttpWebRequest;

            // Set request.KeepAlive to use a persistent connection. 
            if (request != null)
            {
                request.KeepAlive = false;

                // Get a response from the server.
                WebResponse resp = null;

                try
                {
                    resp = request.GetResponse();
                }
                catch (Exception e)
                {
                    Debug.Print("ERROR: Exception for WU: " + e);
                }

                // Get the network response stream to read the page data.
                if (resp != null)
                {
                    using (Stream dataStream = resp.GetResponseStream())
                    {
                        using (var reader = new StreamReader(dataStream))
                        {
                            string responseFromServer = reader.ReadToEnd();
                            Debug.Print(responseFromServer);
                            reader.Close();
                            dataStream.Close();
                        }
                    }
                }

                request.Dispose();

                GC.WaitForPendingFinalizers();
                Debug.GC(true);
            }
        }

        private static void FlashLed(OutputPort led, int count)
        {
            for (int i = 0; i < count; i++)
            {
                led.Write(true);
                Thread.Sleep(200);
                led.Write(false);
                Thread.Sleep(200);
            }
        }

        private static void RecurseFolders(DirectoryInfo directory)
        {
            if (directory.Exists)
            {
                Debug.Print(directory.FullName);

                foreach (FileInfo file in directory.GetFiles())
                {
                    Debug.Print(file.FullName);
                }

                foreach (DirectoryInfo subDirectory in directory.GetDirectories())
                {
                    RecurseFolders(subDirectory);
                }
            }
        }

        public static DateTime NtpTime(String timeServer)
        {
            // Find endpoint for timeserver
            var ep = new IPEndPoint(Dns.GetHostEntry(timeServer).AddressList[0], 123);

            // Connect to timeserver
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect(ep);

            // Make send/receive buffer
            var ntpData = new byte[48];
            Array.Clear(ntpData, 0, 48);

            // Set protocol version
            ntpData[0] = 0x1B;

            // Send Request
            s.Send(ntpData);

            // Receive Time
            s.Receive(ntpData);

            const byte offsetTransmitTime = 40;

            ulong intpart = 0;
            ulong fractpart = 0;

            for (int i = 0; i <= 3; i++)
            {
                intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];
            }

            for (int i = 4; i <= 7; i++)
            {
                fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];
            }

            ulong milliseconds = (intpart*1000 + (fractpart*1000)/0x100000000L);

            s.Close();

            TimeSpan timeSpan = TimeSpan.FromTicks((long) milliseconds*TimeSpan.TicksPerMillisecond);
            var dateTime = new DateTime(1900, 1, 1);
            dateTime += timeSpan;

            return dateTime;
        }
    }
}