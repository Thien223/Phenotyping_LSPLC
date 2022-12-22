using LSPLC.Cores;
using LSPLC.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LSPLC
{
    internal class Program
    {
        public static Socket listener;

        private static List<Socket> Clients;

        /// <summary>
        ///  send message to TCP clients
        /// </summary>
        /// <param name="data"></param>
        private static void SendMessage(byte[] message, string startVariablePrefix="D", int startVariableIndex = 0)
        {
            if(Clients==null || Clients.Count<=0)
            {
                Console.WriteLine("empty clients");
                return;
            }

            string ParseResponse(byte[] _message, string _startVariablePrefix = "D", int _startVariableIndex = 0)
            {
                Dictionary<string, int> output = new Dictionary<string, int>();
                for(int i = 0; i < _message.Length-1; i += 2)
                {
                    var label = $"{_startVariablePrefix}{_startVariableIndex+(i/2)}"; //// construct the variable name
                    output[label] = (_message[i] << 8) + _message[i]; //// take the value from byte array

                }
                return JsonConvert.SerializeObject(output);
            }

            //// Parsing output byte array to json, with format {variableName:value, ...}
            string data = ParseResponse(message, startVariablePrefix, startVariableIndex);
            //// convert strign to byte stream and send over TCP
            byte[] b_data = Encoding.ASCII.GetBytes(data);
            for (int i = 0; i < Clients.Count; i++)
            {
                var client = Clients[i];
                if (client == null) continue;
                try
                {
                    client.Send(b_data);
                    Console.WriteLine($"*** {data} ***");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"now: {e.Message}\n{e.StackTrace}");
                    Clients.RemoveAt(i);
                    return;
                }
            }
        }


        /// <summary>
        ///  send message to TCP clients
        /// </summary>
        /// <param name="data"></param>
        private static void SendTextMessage(string data)
        {
            //// convert strign to byte stream and send over TCP
            byte[] b_data = Encoding.ASCII.GetBytes(data);
            for (int i = 0; i < Clients.Count; i++)
            {
                var client = Clients[i];
                if (client == null) continue;
                try
                {
                    client.Send(b_data);
                    Console.WriteLine($"*** {data} ***");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"now: {e.Message}\n{e.StackTrace}");
                    Clients.RemoveAt(i);
                    return;
                }
            }
        }



        /// <summary>
        /// wait for a while (different from Thread.Sleep())
        /// </summary>
        /// <param name="MS"></param>
        /// <returns></returns>
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

        /// <summary>
        /// add client to clients list
        /// </summary>
        private static void AddClient()
        {
            while (true)
            {
                var client = listener.Accept();
                if (!Clients.Contains(client))
                {
                    Clients.Add(client);
                }
                Thread.Sleep(5000);
            }
        }


        /// <summary>
        /// Main program
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            /// logging object 
            Log log = new Log("Main");
            ///// max allowed connection
            int max = 1000;
            ///// create TCP server
            listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint clients = new IPEndPoint(IPAddress.Any, 3395);
            listener.Bind(clients);
            listener.Listen(max);
            //// client list
            Clients = new List<Socket> ();
            ///// PLC connection
            PLCDevice device = new PLCDevice("COM4", 9600);
            ///// add client thread.
            Task addClientWorker = Task.Run(() => AddClient());
            int stationNumber = 3;
            var a = 0;

            /// main loop
            while (true)
            {
                if (a ==0) { a = 1; }else
                {
                    a = 0;
                }
                //// listen to client.
                if (!device.IsDisposed)
                {
                    var startVariable = "K00000";
                    int idx = int.Parse(startVariable.Substring(1));
                    string prefix = startVariable.Substring(0, 1);
                    int read_functionCode = 1; //// 1~4, but we use 3, 4 (read word) only.
                    int write_functionCode = 5; /// 5,6,15,16 but we use 15 (write bit, on Kxxxx address) and 16 (write word on Dxxxx address) only

                    switch (prefix)
                    {
                        case "D":
                            read_functionCode = 4;
                            write_functionCode = 16;
                            break;
                        case "K":
                            read_functionCode= 2;
                            write_functionCode = 15;
                            break;
                        default: break;
                    }
                    try
                    {
                        List<int> values = new List<int> {
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,
                                                        a, a, a, a, a, a, a, a,a,a,a,
                                                        a, a, a, a, a, a, a, a
                                                    };
                        device.WriteRequest(values, stationNumber: stationNumber, startVariable: startVariable, outputCount: 96+27, functionCode: write_functionCode);
                        SendTextMessage("Write Successed!");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                        SendTextMessage($"Write Failed, Error: {e.Message}");
                    }


                    try
                    {
                        Console.WriteLine($"read_functionCode: {read_functionCode}");
                        byte[] b = device.ReadRequest(stationNumber: stationNumber, startVariable: startVariable, readCount: 16, functionCode: read_functionCode);

                        // send TCP stream to agentC
                        SendMessage(message: b, startVariablePrefix: prefix, startVariableIndex: idx);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.StackTrace);
                        SendTextMessage($"Read Failed, Error: {e.Message}");
                    }

                    //List<int> values = new List<int> { a};
                    //List<int> values = new List<int> {01,01,00,00,00,00,00,00};


                }
                Thread.Sleep(5000);
            }
        }
    }
}
