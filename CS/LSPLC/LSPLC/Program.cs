﻿using LSPLC.Cores;
using LSPLC.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LSPLC
{
    internal class Program
    {
        #region Properties
        public static Socket listener;

        private static List<Socket> Clients;
        private static Log log = new Log("Main");
        #endregion Properties


        #region Methods
        /// <summary>
        ///  send message to TCP clients
        /// </summary>
        /// <param name="data"></param>
        /// 
        private static void SendMessage(ModbusRTUResponse obj_response, Socket client)
        {
            //// Parsing output byte array to json, with format {variableName:value, ...}
            string data = JsonConvert.SerializeObject(obj_response);
            //// convert strign to byte stream and send over TCP
            byte[] b_data = Encoding.UTF8.GetBytes(data);
            if (client == null) return;
            try
            {
                client.Send(b_data);
                log.Write($"*** {data} ***");
            }
            catch (Exception e)
            {
                log.Write($"now: {e.Message}\n{e.StackTrace}");
                return;
            }
        }
        //private static void SendMessage(byte[] message, string startVariablePrefix = "D", int startVariableIndex = 0)
        //{
        //    if (Clients == null || Clients.Count <= 0)
        //    {
        //        Console.WriteLine("empty clients");
        //        return;
        //    }

        //    string ParseResponse(byte[] _message, string _startVariablePrefix = "D", int _startVariableIndex = 0)
        //    {
        //        Dictionary<string, int> output = new Dictionary<string, int>();
        //        for (int i = 0; i < _message.Length - 1; i += 2)
        //        {
        //            var label = $"{_startVariablePrefix}{_startVariableIndex + (i / 2)}"; //// construct the variable name
        //            output[label] = (_message[i] << 8) + _message[i]; //// take the value from byte array

        //        }
        //        return JsonConvert.SerializeObject(output);
        //    }

        //    //// Parsing output byte array to json, with format {variableName:value, ...}
        //    string data = ParseResponse(message, startVariablePrefix, startVariableIndex);
        //    //// convert strign to byte stream and send over TCP
        //    byte[] b_data = Encoding.ASCII.GetBytes(data);
        //    for (int i = 0; i < Clients.Count; i++)
        //    {
        //        var client = Clients[i];
        //        if (client == null) continue;
        //        try
        //        {
        //            client.Send(b_data);
        //            Console.WriteLine($"*** {data} ***");
        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine($"now: {e.Message}\n{e.StackTrace}");
        //            Clients.RemoveAt(i);
        //            return;
        //        }
        //    }
        //}


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



        private static List<ModbusRTURequest> ListenRequests()
        {
            //// if there is no client, just return empty list
            if (Clients.Count <= 0)
            {
                return new List<ModbusRTURequest>();
            }
            List<ModbusRTURequest> lst_requests = new List<ModbusRTURequest>();
            //// loop over each client, take request string from tcp stream, parsing to modbusRTURequest object
            //// set object client as current client, and add to return list.
            for (int i = 0; i < Clients.Count; i++)
            {
                var client = Clients[i];
                if (client == null) continue;
                try
                {
                    using (StreamReader stream = new StreamReader(new NetworkStream(client)))
                    {
                        string str_request = stream.ReadLine();
                        //Console.WriteLine($"Client request: {str_request}");
                        ModbusRTURequest obj_request = JsonConvert.DeserializeObject<ModbusRTURequest>(str_request);
                        obj_request.client = client;
                        lst_requests.Add(obj_request);
                    }
                }
                catch{
                    log.Write("Waiting for request from client...");
                }
            }
            return lst_requests;
        }


        //        try
        //                    {
        //                        byte[] b = device.ReadRequest(stationNumber: stationNumber, startVariable: startVariable, readCount: 17, functionCode: read_functionCode);
        //        // send TCP stream to agentC
        //        //SendMessage(message: b, startVariablePrefix: prefix, startVariableIndex: idx);
        //    }
        //                    catch (Exception e)
        //                    {
        //                        Console.WriteLine(e.StackTrace);
        //                        SendTextMessage($"Read Failed, Error: {e.Message}");
        //}


        /// <summary>
        /// read current value method (from D600~D606)
        /// </summary>
        /// <param name="obj_request"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static ModbusRTUResponse ReadCurrentValue(ModbusRTURequest obj_request, PLCDevice device, ref ModbusRTUResponse obj_response)
        {
            var stationNumber = obj_request.plc_no;
            var startVariable = "D207";
            string prefix = startVariable.Substring(0, 1);
            int startVariableIndex = Convert.ToInt32(startVariable.Substring(1));
            var functioncode = 4;
            try
            {
                byte[] stream = device.ReadRequest(stationNumber: stationNumber,
                    startVariable: startVariable,
                    readCount: 8, //// D207~D214
                    functionCode: functioncode);
                obj_response.cur_value = ParseWordResponse(_message: stream, _startVariablePrefix: prefix, _startVariableIndex: startVariableIndex, type:"cur_value");
                obj_response.result = "ok";
            }catch(Exception e)
            {
                obj_response.result = "error";
                obj_response.message = e.Message;
            }
            return obj_response;
        }

        /// <summary>
        /// write run_stop (k0~k17), start_end(D14~D109), set_value(K11~K106)
        /// </summary>
        /// <param name="obj_request"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static ModbusRTUResponse Write(ModbusRTURequest obj_request, PLCDevice device, string type, ref ModbusRTUResponse obj_response)
        {
            //// write run_stop: K00000~K00017
            int stationNumber = obj_request.plc_no;
            string startVariable = "";
            string prefix = "";
            int startVariableIndex = 0;
            int functioncode = 0;
            int writeLength = 0;
            List<int> values = null;
            switch (type)
            {
                case "run_stop": /// write bit k0~k17
                    startVariable = obj_request.run_stop.Keys.First(); 
                    startVariableIndex = Convert.ToInt32(startVariable.Substring(1));
                    prefix = startVariable.Substring(0, 1);
                    writeLength = obj_request.run_stop.Values.Count;
                    values = new List<int>(obj_request.run_stop.Values);
                    functioncode = 15; /// write bit continuosly
                    break;
                case "start_end": /// write word D14~D109
                    startVariable = obj_request.start_end.Keys.First();
                    startVariableIndex = Convert.ToInt32(startVariable.Substring(1));
                    prefix = startVariable.Substring(0, 1);
                    writeLength = obj_request.start_end.Values.Count;
                    values = new List<int>(obj_request.start_end.Values);
                    functioncode = 16; /// write word continuosly
                    break;
                case "set_value": //// write word K11~k106
                    startVariable = obj_request.set_value.Keys.First();
                    startVariableIndex = Convert.ToInt32(startVariable.Substring(1));
                    prefix = startVariable.Substring(0, 1);
                    writeLength = obj_request.set_value.Values.Count;
                    values = new List<int>(obj_request.set_value.Values);
                    functioncode = 16; /// write word continuosly
                    break;
                default:
                    break;
            }
            
            try
            {
                log.Write($"stationNumber: {stationNumber}");
                log.Write($"startVariable: {startVariable}");
                log.Write($"values.Count: {values.Count}");
                log.Write($"functioncode: {functioncode}");
                log.Write($"device: {device.Device.IsOpen}");
                device.WriteRequest(values,
                    stationNumber: stationNumber,
                    startVariable: startVariable,
                    outputCount: values.Count,
                    functionCode: functioncode);
                obj_response.result = "ok";

            }catch(Exception e)
            {
                log.Write($"{e.Message}\n{e.StackTrace}");
                obj_response.result = "error";
                obj_response.message = e.Message;
            }
            return obj_response;
        }
        /// <summary>
        /// parse (Word) response from bytes array into dictionary with format: {<address>:<value>}
        /// </summary>
        /// <param name="_message"> bytes array to parse</param>
        /// <param name="_startVariablePrefix"> address prefix (D, K)</param>
        /// <param name="_startVariableIndex"> address start index</param>
        /// <returns></returns>
        private static Dictionary<string, int> ParseWordResponse(byte[] _message, string _startVariablePrefix = "D", int _startVariableIndex = 0, string type="None")
        {
            Dictionary<string, int> output = new Dictionary<string, int>();
            for (int i = 0; i < _message.Length - 1; i += 2)
            {
                ///// with current value data, take only odd index (D207, D209, D211, D213)
                if (type.Equals("cur_value"))
                {
                    if ((_startVariableIndex + (i / 2)) % 2 == 1)
                    {
                        var label = $"{_startVariablePrefix}{_startVariableIndex + (i / 2)}"; //// construct the variable name
                        output[label] = (_message[i] << 8) + _message[i]; //// take the value from byte array
                    }
                }
                //// with other data, take all
                else
                {
                    var label = $"{_startVariablePrefix}{_startVariableIndex + (i / 2)}"; //// construct the variable name
                    output[label] = (_message[i] << 8) + _message[i]; //// take the value from byte array
                }
            }
            return output;
        }

        /// <summary>
        /// parse (bit) response from bytes array into dictionary with format: {<address>:<value>}
        /// </summary>
        /// <param name="_message"> array to parse</param>
        /// <param name="_startVariablePrefix"></param>
        /// <param name="_startVariableIndex"></param>
        /// <returns></returns>
        private static Dictionary<string, int> ParseBitResponse(byte[] _message, string _startVariablePrefix = "K", int _startVariableIndex = 0)
        {
            Dictionary<string, int> output = new Dictionary<string, int>();
            for (int i = 0; i < _message.Length ; i ++)
            {
                /// convert byte value to binary string (note that this string has order of 7,6,5,4,3,2,1,0
                string temp = Convert.ToString(_message[i]).PadLeft(8,'0'); 
                /// reverse string to 0,1,2,3,4,5,6,7 order
                temp = Utils.ReverseString(temp);
                for(int j = 0; j < 8; j++)
                {
                    string label = $"{_startVariablePrefix}{_startVariableIndex+=1}"; //// construct the variable name
                    output[label] = Convert.ToInt32(temp[j]);
                }                
            }
            return output;
        }

        #endregion Methods


        #region Main Program
        /// <summary>
        /// Main program
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            /// logging object 
            
            ///// max allowed connection
            int max = 1000;
            ///// create TCP server
            listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint clients = new IPEndPoint(IPAddress.Any, 3395);
            listener.Bind(clients);
            listener.Listen(max);
            //// client list
            Clients = new List<Socket>();
            ///// PLC connection
            PLCDevice device = null;
            ///// add client thread.
            Task addClientWorker = Task.Run(() => AddClient());
            //int stationNumber = 1;
            //var a = 1;
            //string startVariable = "K00000";
            //int functionCode = 1;
            /// main loop
            while (true)
            {
                ///// auto detect and connect to PLCs 
                if(device==null || device.Device == null)
                {
                    try
                    {
                        device = new PLCDevice("COM4", 9600);
                    }
                    catch (Exception e) {
                        log.Write($"Could not connect to PLC devices... Error: {e.Message}");
                    }
                }
                /// listen to client request
                List<ModbusRTURequest> lst_requests = ListenRequests();
                /// if there is any request
                if (lst_requests.Count > 0)
                {
                    foreach (ModbusRTURequest obj_request in lst_requests)
                    {
                        if (!device.IsDisposed)
                        {
                            ModbusRTUResponse obj_response = new ModbusRTUResponse();
                            if (obj_request.command.Equals("read")) //// read (word) current value (D600 ~ D606)
                            {
                                obj_response = ReadCurrentValue(obj_request, device, ref obj_response);
                            }
                            else if (obj_request.command.Equals("write"))
                            {
                                obj_response = Write(obj_request, device, type: "run_stop", ref obj_response);
                                obj_response = Write(obj_request, device, type: "start_end", ref obj_response);
                                obj_response = Write(obj_request, device, type: "set_value", ref obj_response);
                            }
                            else
                            {
                                log.Write($"Invalid request from AgentC. Request: '{JsonConvert.SerializeObject(obj_request)}'");

                                obj_response.result = "error";
                                obj_response.message = $"Invalid request from AgentC. Request: '{JsonConvert.SerializeObject(obj_request)}'";

                            }
                            SendMessage(obj_response, obj_request.client);
                        }
                    }
                }
                Thread.Sleep(5);

                //if (a == 0) { a = 1; }
                //else
                //{
                //    a = 0;
                //}
                //// listen to client.
                //if (!device.IsDisposed)
                //{
                //    int idx = int.Parse(startVariable.Substring(1));
                //    string prefix = startVariable.Substring(0, 1);
                //    int read_functionCode = 1; //// 1~4
                //    int write_functionCode = 5; //// 5,6,15,16 but we use 15 (write bit, on Kxxxx address) and 16 (write word on Dxxxx address) only

                //    switch (prefix)
                //    {
                //        case "D":
                //            read_functionCode = 4;
                //            write_functionCode = 16;
                //            break;
                //        case "K":
                //            read_functionCode = 1;
                //            write_functionCode = 15;
                //            break;
                //        default: break;
                //    }
                //    try
                //    {
                //        List<int> values = new List<int> {
                //                                        a, a, a, a, a, a, a, a,
                //                                        a, a*0, a, a, a, a, a, a,
                //                                        a*0
                //                                    };

                //        device.WriteRequest(values, stationNumber: stationNumber, startVariable: startVariable, outputCount: values.Count, functionCode: write_functionCode);
                //        SendTextMessage("Write Successed!");
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e.StackTrace);
                //        SendTextMessage($"Write Failed, Error: {e.Message}");
                //    }


                //    try
                //    {
                //        byte[] b = device.ReadRequest(stationNumber: stationNumber, startVariable: startVariable, readCount: 17, functionCode: read_functionCode);
                //        // send TCP stream to agentC
                //        SendMessage(message: b, startVariablePrefix: prefix, startVariableIndex: idx);
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e.StackTrace);
                //        SendTextMessage($"Read Failed, Error: {e.Message}");
                //    }

                //}
            }
        }
        #endregion Main Program
    }
}
