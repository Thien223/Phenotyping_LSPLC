using LSPLC.Cores;
using LSPLC.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


//using System;
//using System.Linq;
//using System.Threading;
//using VagabondK.Protocols.Channels;
//using VagabondK.Protocols.Logging;
//using VagabondK.Protocols.LSElectric;
//using VagabondK.Protocols.LSElectric.Cnet;
//using Newtonsoft.Json;
//using System.IO.Ports;

namespace LSPLC
{
    internal class Program
    {
        private static DateTime Delay(int MS)
        {
            DateTime ThisMoment = DateTime.Now;
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, MS);
            DateTime AfterWards = ThisMoment.Add(duration);

            while (AfterWards >= ThisMoment)
            {
                System.Windows.Forms.Application.DoEvents();
                ThisMoment = DateTime.Now;
            }

            return DateTime.Now;
        }

        static void Main(string[] args)
        {
            //Console.WriteLine($"started...");
            //CnetClient client = new CnetClient(new SerialPortChannel("COM5", 9600, 8, StopBits.One, Parity.None, Handshake.None));

            //foreach (var item in client.Read(5, "%MW100", "%MW200"))
            //    Console.WriteLine($"변수: {item.Key}, 값: {item.Value.WordValue}");

            //foreach (var item in client.Read(5, "%MW100", 5))
            //    Console.WriteLine($"변수: {item.Key}, 값: {item.Value.WordValue}");

            //client.Write(5, ("%MW102", 10), ("%MW202", 20));

            //client.Write(5, "%MW300", 10, 20);

            //var monitorExecute1 = client.RegisterMonitor(5, 1, "%MW100", "%MW200");
            //foreach (var item in client.Read(monitorExecute1))
            //    Console.WriteLine($"변수: {item.Key}, 값: {item.Value.WordValue}");

            //var monitorExecute2 = client.RegisterMonitor(5, 2, "%MW100", 5);
            //foreach (var item in client.Read(monitorExecute2))
            //    Console.WriteLine($"변수: {item.Key}, 값: {item.Value.WordValue}");

            Log log = new Log("Main");
            PLCDevice device = new PLCDevice("COM8", 9600);
            //device.Device.Open();

            var a = 255;
            while (true)
            {
                if (!device.IsDisposed)
                {
                    //log.Write($"Start sending request....");
                    var b = device.ReadRequest(stationNumber: 0, startVariable:"D00600", readCount:8, functionCode:3);
                    List<int> values = new List<int> {a++, a, a, a, a, a, a, a };
                    //List<int> values = new List<int> {01,01,00,00,00,00,00,00};
                    try
                    {
                        device.WriteRequest(values, stationNumber: 0, startVariable: "D00600", outputCount: 8, functionCode: 16);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                    }
                    //foreach (var item in b)
                    //{
                    //    log.Write(item.ToString());
                    //}
                }
                Delay(100);
            }
        }
    }
}
