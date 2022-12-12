using LSPLC.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VagabondK.Protocols.Channels;
using VagabondK.Protocols.Logging;
using LSPLC.Cores;
using System.Reflection;
using VagabondK.Protocols.LSElectric;
using System.Runtime.ExceptionServices;
using static System.Net.Mime.MediaTypeNames;

namespace LSPLC.Cores
{
    public class PLCDevice
    {
        public PLCDevice(string port, int baudrate)
        {
            Port = port;
            Baudrate = baudrate;
            Device = new SerialPort(portName: Port, baudRate: Baudrate, parity: Parity.None);
            log = new Log(GetType().Name);
        }
        private Log log;
        /// <summary>
        /// free resource
        /// </summary>
        public bool IsDisposed { get; protected set; }

        /// <summary>
        /// serial port
        /// </summary>
        private string Port { get; set; }

        private int Baudrate { get; set; }

        private SerialPort Device { get; set; }

        private readonly object openLock = new object();
        private readonly object closeLock = new object();
        private readonly object readLock = new object();
        private readonly object writeLock = new object();
        private readonly Queue<byte> readBuffer = new Queue<byte>();
        private readonly EventWaitHandle readEventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly string description;
        private bool isRunningReceive = false;


        /// <summary>
        /// free the resource
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Close();
            }
        }

        /// <summary>
        /// close the device connection
        /// </summary>
        private void Close()
        {
            lock (openLock)
            {
                if (Device != null && Device.IsOpen)
                {
                    Device.Close();
                    this.Dispose();
                    log.Write("Serial Device Closed..");
                }
            }
        }

        /// <summary>
        /// 채널에 남아있는 모든 바이트 읽기
        /// </summary>
        /// <returns>읽은 바이트 열거</returns>
        public IEnumerable<byte> ReadAllRemain()
        {
            lock (readLock)
            {
                while (readBuffer.Count > 0)
                    yield return readBuffer.Dequeue();

                if (!Device.IsOpen)
                    yield break;

                try
                {
                    Device.DiscardInBuffer();
                }
                catch { }
            }
        }


        private void CheckPort()
        {
            lock (openLock)
            {
                if (!IsDisposed)
                {
                    try
                    {
                        if (!Device.IsOpen)
                        {
                            Device.Open();
                            ReadAllRemain();

                            //log.Write("Read..");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write(ex.Message);
                        this.Dispose();
                    }
                }
            }
        }

        private byte? GetByte(int timeout)
        {
            lock (readBuffer)
            {
                if (readBuffer.Count == 0)
                {
                    readEventWaitHandle.Reset();

                    Task.Factory.StartNew(() =>
                    {
                        if (!isRunningReceive)
                        {
                            isRunningReceive = true;
                            try
                            {
                                CheckPort();
                                if (Device.IsOpen)
                                {
                                    byte[] buffer = new byte[8192];
                                    while (true)
                                    {
                                        if (Device.BytesToRead > 0)
                                        {
                                            int received = Device.Read(buffer, 0, buffer.Length);
                                            lock (readBuffer)
                                            {
                                                for (int i = 0; i < received; i++)
                                                    readBuffer.Enqueue(buffer[i]);
                                                readEventWaitHandle.Set();
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                Close();
                            }
                            readEventWaitHandle.Set();
                            isRunningReceive = false;
                        }
                    }, TaskCreationOptions.LongRunning);
                }
                else return readBuffer.Dequeue();
            }

            if (timeout == 0 ? readEventWaitHandle.WaitOne() : readEventWaitHandle.WaitOne(timeout))
                return readBuffer.Count > 0 ? readBuffer.Dequeue() : (byte?)null;
            else
                return null;
        }

        /// <summary>
        /// 바이트 배열 쓰기
        /// </summary>
        /// <param name="bytes">바이트 배열</param>
        public void Write(byte[] bytes)
        {
            CheckPort();
            lock (writeLock)
            {
                try
                {
                    if (Device.IsOpen)
                    {
                        Device.Write(bytes, 0, bytes.Length);
                        //log.Write("Writing successed..");
                    }
                }
                catch (Exception ex)
                {
                    Close();
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 1 바이트 읽기
        /// </summary>
        /// <param name="timeout">제한시간(밀리초)</param>
        /// <returns>읽은 바이트</returns>
        public byte Read(int timeout)
        {
            lock (readLock)
            {
                byte a = GetByte(timeout) ?? throw new TimeoutException();
                return a;
            }
        }

        /// <summary>
        /// 여러 개의 바이트 읽기
        /// </summary>
        /// <param name="count">읽을 개수</param>
        /// <param name="timeout">제한시간(밀리초)</param>
        /// <returns>읽은 바이트 열거</returns>
        public IEnumerable<byte> Read(uint count, int timeout)
        {
            lock (readLock)
            {
                for (int i = 0; i < count; i++)
                {
                    yield return GetByte(timeout) ?? throw new TimeoutException();
                }
            }
        }

        /// <summary>
        /// 수신 버퍼에 있는 데이터의 바이트 수입니다.
        /// </summary>
        public uint BytesToRead
        {
            get
            {
                uint available = 0;

                try
                {
                    available = (uint)Device.BytesToRead;
                }
                catch { }
                return (uint)readBuffer.Count + available;
            }
        }



        internal static bool TryParseByte(IList<byte> bytes, int index, out byte value)
            => byte.TryParse($"{(char)bytes[index]}{(char)bytes[index + 1]}", System.Globalization.NumberStyles.HexNumber, null, out value);
        internal static bool TryParseWord(IList<byte> bytes, int index, out ushort value)
            => ushort.TryParse($"{(char)bytes[index]}{(char)bytes[index + 1]}{(char)bytes[index + 2]}{(char)bytes[index + 3]}", System.Globalization.NumberStyles.HexNumber, null, out value);
        internal static bool TryParseDoubleWord(IList<byte> bytes, int index, out uint value)
            => uint.TryParse($"{(char)bytes[index]}{(char)bytes[index + 1]}{(char)bytes[index + 2]}{(char)bytes[index + 3]}{(char)bytes[index + 4]}{(char)bytes[index + 5]}{(char)bytes[index + 6]}{(char)bytes[index + 7]}", System.Globalization.NumberStyles.HexNumber, null, out value);
        internal static bool TryParseLongWord(IList<byte> bytes, int index, out ulong value)
            => ulong.TryParse($"{(char)bytes[index]}{(char)bytes[index + 1]}{(char)bytes[index + 2]}{(char)bytes[index + 3]}{(char)bytes[index + 4]}{(char)bytes[index + 5]}{(char)bytes[index + 6]}{(char)bytes[index + 7]}{(char)bytes[index + 8]}{(char)bytes[index + 9]}{(char)bytes[index + 10]}{(char)bytes[index + 11]}{(char)bytes[index + 12]}{(char)bytes[index + 13]}{(char)bytes[index + 14]}{(char)bytes[index + 15]}", System.Globalization.NumberStyles.HexNumber, null, out value);


        /// <summary>
        /// request data from PLC from <startvariable>, to next <readcount> variables
        /// </summary>
        /// <param name="stationNumber"></param>
        /// <param name="header"></param>
        /// <param name="tail"></param>
        /// <param name="startVariable"></param>
        /// <param name="readCount"></param>
        /// <returns></returns>
        public List<byte> ReadRequest(int stationNumber = 5, string startVariable = "%DW0014", int readCount = 96)
        {
            List<byte> buffers = new List<byte>();
            byte head = 0x05;
            byte[] sNumber = Utils.ConvertToHex(stationNumber);

            byte Command = (byte)CnetCommand.Read; //// 'R' if use BCC, 'r' if not
            var CommandType = CnetCommandType.Continuous;  //// SS for read continuosly


            DeviceVariable address = new DeviceVariable();
            DeviceVariable.TryParse(startVariable, out address);
            StringBuilder stringBuilder = new StringBuilder($"%{(char)address.DeviceType}{(char)address.DataType}{address.Index}");
            byte[] variableName = Encoding.ASCII.GetBytes(stringBuilder.ToString());

            byte[] variableSize = Utils.ConvertToHex(variableName.Length);
            byte[] numberOfData = Utils.ConvertToHex(readCount);
            byte tail = 0x04;


            List<byte> requestMessage = new List<byte>();
            requestMessage.Add(head);

            log.Write($"ENQ: {string.Join(" ", head)} ({Convert.ToChar(head)})");
            requestMessage.AddRange(sNumber);
            log.Write($"sNumber: {string.Join(" ", sNumber)} ({Encoding.ASCII.GetString(sNumber)})");
            requestMessage.Add(Command);
            log.Write($"Command: {string.Join(" ", Command)} ({Convert.ToChar(Command)})");
            requestMessage.Add((byte)((int)CommandType >> 8));
            requestMessage.Add((byte)((int)CommandType & 0xFF));
            log.Write($"CommandType: {string.Join(" ", (byte)((int)CommandType >> 8), (byte)((int)CommandType & 0xFF))} ({Convert.ToChar((byte)((int)CommandType >> 8))}{Convert.ToChar((byte)((int)CommandType & 0xFF))})");
            //log.Write($"(byte)((int)CommandType & 0xFF): {string.Join(" ", (byte)((int)CommandType & 0xFF))} ()");
            requestMessage.AddRange(variableSize);
            log.Write($"variableSize: {string.Join(" ", variableSize)} ({Encoding.ASCII.GetString(variableSize)})");
            requestMessage.AddRange(variableName);
            log.Write($"variableName: {string.Join(" ", variableName)} ({Encoding.ASCII.GetString(variableName)})");
            requestMessage.AddRange(numberOfData);
            log.Write($"numberOfData: {string.Join(" ", numberOfData)} ({Encoding.ASCII.GetString(numberOfData)})");
            requestMessage.Add(tail);
            log.Write($"EXT: {string.Join(" ", tail)} ({Convert.ToChar(tail)})");

            log.Write($"Final message: {string.Join(" ", requestMessage)}");
            //log.Write($"original message: {Encoding.ASCII.GetString(requestMessage.ToArray())}");
            //log.Write($"hex message: {Utils.ByteArrayToHexViaLookup32(requestMessage.ToArray())}");
            if (this.Device.IsOpen)
            {
                Write(requestMessage.ToArray());
            }
            else { return null; }
            
            log.Write($"Message have been writen to PLC... {this.Device.IsOpen}");
            //requestMessage.Add(header);
            //requestMessage.AddRange(Utils.ConvertToHex(stationNumber));
            //log.Write($"stationNumber: {string.Join(" ", Utils.ConvertToHex(stationNumber.ToString()))}");
            //requestMessage.AddRange(Utils.ConvertToHex(Command.ToString()));

            ////log.Write($"Command: {string.Join(" ", requestMessage)}");
            ////// add command type (SS for read individual, SB for read continuosly)
            //requestMessage.AddRange(Utils.ConvertToHex(CommandType));
            ////log.Write($"CommandType: {string.Join(" ", requestMessage)}");
            ///// add data area
            //requestMessage.AddRange(Utils.ConvertToHex(variableSize));
            ////log.Write($"variableSize: {string.Join(" ", requestMessage)}");
            //requestMessage.AddRange(Utils.ConvertToHex(variableName));
            ////log.Write($"variableName: {string.Join(" ", requestMessage)}");
            ////requestMessage.AddRange(Encoding.ASCII.GetBytes(numberOfData.ToString()));
            ///// 

            ////requestMessage.AddRange(Utils.ConvertToHex(variableSize1));
            //////log.Write($"variableSize1: {string.Join(" ", requestMessage)}");
            ////requestMessage.AddRange(Utils.ConvertToHex(variableName1));
            ////log.Write($"variableName1: {string.Join(" ", requestMessage)}");
            ////// add EOT 
            //requestMessage.Add(tail);
            ///// add BCC
            ///// requestMessage.AddRange(Encoding.ASCII.GetBytes((requestMessage.Sum(b => b) % 256).ToString("X2")));
            //log.Write($"requestMessage: {string.Join(" ", requestMessage.ToArray())}");
            //Write(requestMessage.ToArray());
            //var res_header = Read(2000);

            //log.Write($"res_header: '{string.Join("", res_header)}'");

            log.Write($"Start reading response");
            bool stop = false;
            while (!stop)
            {
                byte first = Read(2000);
                buffers.Clear();
                if (first == 0x06 || first==0x15)
                {
                    //Console.WriteLine($"\n");
                    buffers.Add(first);
                    //Console.Write($"{string.Join(" ", first)} ");
                    while (true)
                    {
                        var next = Read(2000);
                        buffers.Add(next);
                        //Console.Write($"{string.Join(" ", next)} ");
                        if (next == 0x03 || next==0x06)
                        {
                            //Console.WriteLine($"\n");
                            stop = true;
                            break;
                        }
                        
                    }
                }
            }
            log.Write($"Final response: {string.Join(" ", buffers.ToArray())}");
            log.Write($"\n\n\n");

            //log.Write($"buffers.Count: {buffers.Count}");
            //log.Write($"'{string.Join(" ", buffers.ToArray())}'");
            return buffers;
        }

        /// <summary>
        /// write to plc
        /// </summary>
        /// <param name="stationNumber"></param>
        /// <param name="header"></param>
        /// <param name="tail"></param>
        /// <param name="startVariable"></param>
        /// <param name="readCount"></param>
        public void WriteRequest(byte stationNumber = 0x35, byte header = 0x05, byte tail = 0x04, string startVariable = "%MW100", int readCount = 96)
        {
            var Command = 'W'; //// 'R' if use BCC, 'r' if not
            var CommandType = "SB"; //// SS for read individual, SB for continuosly
            var variableSize = "6";
            var variableName = startVariable;
            var numberOfData = readCount;

            List<byte> requestMessage = new List<byte>
            {
                /// add ENQ
                header,
                //// add station number
                stationNumber,
                //// add command (r,R for read, w,W for write)
                (byte)Command,


            };
            //// add command type (SS for read individual, SB for read continuosly)
            //byte[] cmdType = Utils.ConvertToBytes(CommandType); /// convert command to 
            //requestMessage.AddRange(cmdType);
            /// add data area
            requestMessage.AddRange(Encoding.ASCII.GetBytes(variableSize));
            requestMessage.AddRange(Encoding.ASCII.GetBytes(variableName));
            requestMessage.AddRange(Encoding.ASCII.GetBytes(numberOfData.ToString()));
            /// 
            //// add EOT 
            requestMessage.Add(tail);
        }
    }
}
