using LSPLC.Utilities;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LSPLC.Cores
{
    public class PLCDevice
    {
        public PLCDevice(string port, int baudrate)
        {
            Port = port;
            Baudrate = baudrate;
            Device = new SerialPort(portName: Port, baudRate: Baudrate, parity: Parity.None, stopBits: StopBits.One, dataBits: 8);
            log = new Log(GetType().Name);
            Device.Open();
        }
        private readonly Log log;
        /// <summary>
        /// free resource
        /// </summary>
        public bool IsDisposed { get; protected set; }

        /// <summary>
        /// serial port
        /// </summary>
        private string Port { get; set; }

        private int Baudrate { get; set; }

        public SerialPort Device { get; set; }


        private readonly object openLock = new object();
        private readonly object readLock = new object();
        private readonly object writeLock = new object();
        private readonly Queue<byte> readBuffer = new Queue<byte>();
        private readonly EventWaitHandle readEventWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
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
                            log.Write("Device openned.....");
                        }
                    }
                    catch (Exception ex)
                    {
                        //Device.Close();
                        log.Write(ex.Message);
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
                        Device.DiscardInBuffer();
                        Device.Write(bytes, 0, bytes.Length);
                        //log.Write("Writing successed..");
                    }
                }
                catch (Exception ex)
                {
                    log.Write($"Error while writing to PLC: {ex.Message}");
                    //Close();
                }
            }
        }


        /// <summary>
        /// 바이트 배열 쓰기
        /// </summary>
        /// <param name="bytes">바이트 배열</param>
        public void WriteText(string str)
        {
            CheckPort();
            lock (writeLock)
            {
                try
                {
                    if (Device.IsOpen)
                    {
                        Device.Write(str);
                        //log.Write("Writing successed..");
                    }
                }
                catch (Exception ex)
                {
                    log.Write($"Error while writing to PLC: {ex.Message}");
                    //Close();
                }
            }
        }

        /// <summary>
        /// 1 바이트 읽기
        /// </summary>
        /// <param name="timeout">제한시간(밀리초)</param>
        /// <returns>읽은 바이트</returns>
        public byte ReadOne(int timeout = 2000)
        {
            lock (readLock)
            {
                try
                {
                    byte a = GetByte(timeout).Value;
                    return a;
                }catch (Exception ex)
                {
                    //log.Write($"Error while reading from PLC: {ex.Message}\n{ex.StackTrace}");
                    //log.Write($"Error while reading from PLC: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 여러 개의 바이트 읽기
        /// </summary>
        /// <param name="count">읽을 개수</param>
        /// <param name="timeout">제한시간(밀리초)</param>
        /// <returns>읽은 바이트 열거</returns>
        public IEnumerable<byte> ReadMany(uint count, int timeout)
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
        /// <param name="startVariable"></param>
        /// <param name="readCount"></param>
        /// <param name="functionCode"></param> /// read command support 1~4 function code
        /// <returns></returns>
        public byte[] ReadRequest(int stationNumber = 5, string startVariable = "D00000", int readCount = 96, int functionCode = 4)
        {
            byte[] data;
            int dec_address = int.Parse(startVariable.Substring(1));
            int validResponseNumberOfBytes;
            ////// prepare data to write
            /////
            switch (functionCode)
            {
                case 1: //// Read bit 
                    validResponseNumberOfBytes = (int)Math.Ceiling(readCount / 8.0);
                    //validResponseNumberOfBytes = readCount;
                    break;
                case 2: //// Read bit 
                    validResponseNumberOfBytes = (int)Math.Ceiling(readCount / 8.0);
                    //validResponseNumberOfBytes = readCount;
                    break;
                case 3: //// Read word
                    validResponseNumberOfBytes = readCount*2;
                    break;
                case 4: //// Read word 
                    validResponseNumberOfBytes = readCount*2;
                    break;
                default:
                    throw new NotSupportedException($"Function code <{functionCode}> is not supported");
            }

            List<byte> buffers = new List<byte>();
            int dec_addressUpper = dec_address >> 8;
            int dec_adressLower = dec_address & 0xFF;

            byte b_stationNumber = Convert.ToByte(stationNumber);
            byte b_functionCode = Convert.ToByte(functionCode);
            byte b_addressUpper = Convert.ToByte(dec_addressUpper);
            byte b_addressLower = Convert.ToByte(dec_adressLower);
            byte b_dataSizeUpper = Convert.ToByte(readCount>>8);
            byte b_dataSizeLower = Convert.ToByte(readCount&0xFF);
            List<byte> b_requestMessage = new List<byte> {
                b_stationNumber,
                b_functionCode,
                b_addressUpper,
                b_addressLower,
                b_dataSizeUpper,
                b_dataSizeLower
            };

            var crc = Crc16.ModRTU_CRC(b_requestMessage.ToArray());

            byte b_crcUpper = Convert.ToByte(crc >> 8);
            byte b_crcLower = Convert.ToByte(crc & 0xFF);
            b_requestMessage.Add(b_crcLower);
            b_requestMessage.Add(b_crcUpper);

            //Console.WriteLine("Final Read request:");
            //for (int i = 0; i < b_requestMessage.Count; i += 1)
            //{
            //    Console.Write($"{b_requestMessage[i].ToString("X2")} ");
            //}
            //Console.WriteLine("");
            //// send request to plc
            Write(b_requestMessage.ToArray());

            //// take the response
            bool stop = false;

            //Console.WriteLine("Final Read Response:");
            while (!stop)
            {
                buffers.Clear();
                byte res_stationNumber = ReadOne(); /// station code
                Console.WriteLine($"response station number: {res_stationNumber}");
                if(res_stationNumber == 0)
                {
                    //log.Write($"Error while request to PLC, check the request message...");
                    throw new Exception($"Error while sending request to PLC, check the request message...");
                }
                //Console.Write($"{res_stationNumber.ToString("X2")} ");
                if (res_stationNumber == stationNumber)
                {
                    byte res_functionCode = ReadOne();
                    //Console.Write($"{res_functionCode.ToString("X2")} ");
                    if (res_functionCode == 1 || res_functionCode == 2 || res_functionCode == 3 || res_functionCode == 4)
                    {
                        var res_numberOfBytes = ReadOne();
                        //Console.Write($"{res_numberOfBytes.ToString("X2")} ");
                        if (res_numberOfBytes == validResponseNumberOfBytes)
                        {
                            //Console.Write($"Valid ");
                            //bool error = false;
                            //while (!error)
                            //{
                            //    try
                            //    {
                            //        Console.Write($"{ReadOne().ToString("X2")} ");
                            //    }
                            //    catch (Exception e)
                            //    {
                            //        Console.WriteLine(e.StackTrace);
                            //        error = true;
                            //    }
                            //}
                            //return new byte[] { 0 };
                            data = ReadMany((uint)validResponseNumberOfBytes, 2000).ToArray();
                            //for (int i = 0; i < data.Count(); i += 1)
                            //{
                            //    Console.Write($"{data[i].ToString("X2")} ");
                            //}
                            var res_crcUpper = ReadOne();
                            var res_crcLower = ReadOne();
                            buffers.Add(res_stationNumber);
                            buffers.Add(res_functionCode);
                            buffers.Add(res_numberOfBytes);
                            buffers.AddRange(data);

                            var temp_crc = Crc16.ModRTU_CRC(buffers.ToArray());
                            if ((temp_crc >> 8) == res_crcLower && ((temp_crc & 0xFF) == res_crcUpper))
                            {
                                //b_lst_data = data.ToList();
                                //log.Write($"Final response: '{string.Join(" ", buffers.ToArray())}'");
                                //Console.WriteLine($"\n\n\n");
                                return data;
                            }
                        }
                        else
                        {
                            log.Write($"Invalid Valid {validResponseNumberOfBytes}");
                        }
                    }
                    else if (res_functionCode == 84 || functionCode == 82)
                    {
                        log.Write($"ERROR, response function code: {res_functionCode} ....");
                    }
                    stop = true;
                }
                //else
                //{
                //    throw new Exception($"PLC returns invalid station number (<{res_stationNumber}>)");
                //}
            }
            //log.Write($"buffers.Count: {buffers.Count}");
            //log.Write($"'{string.Join(" ", buffers.ToArray())}'");
            return new byte[] {0};
        }

        /// <summary>
        /// write to plc
        /// </summary>
        /// <param name="stationNumber"></param>
        /// <param name="startVariable"></param>
        /// <param name="readCount"></param>
        /// <param name="functionCode"></param> write command support function code = 5, 6, 15 or 16
        public void WriteRequest(List<int> values, int stationNumber = 0, string startVariable = "D00000", int outputCount = 96, int functionCode = 16)
        {
            if (values == null || values.Count != outputCount)
            {
                throw new FormatException("data to write could not 'null' or 'not equal to outputCount'...");
            }
            int dec_address;
            //// take address bytes from input StartVariable
            dec_address = int.Parse(startVariable.Substring(1));
            byte b_outputCount;
            switch (functionCode)
            {
                case 5:
                    throw new NotSupportedException($"Function code <{functionCode}> is not supported");
                case 6:
                    throw new NotSupportedException($"Function code <{functionCode}> is not supported");
                case 15: /// write bit K00000 ~ K00017
                    //// bit read-write considers data as byte of 8 bit ->> data size = 1 --> 1 byte, datasize = 9 --> 2 bytes, datasize = 16 --> 2 bytes
                    b_outputCount = Convert.ToByte((int)Math.Ceiling(outputCount / 8.0));
                    //dec_address = int.Parse(startVariable.Substring(1)); //// take index from address (K00000 --> 0) dont need to substract
                    break;
                case 16: ///// write word D0014 ~ D0109 or K0011~K0106
                    //string addressPrefix = startVariable.Substring(0, 1);   //// take first character of address (D0014 --> D)
                    //int temp = int.Parse(startVariable.Substring(1));
                    b_outputCount = Convert.ToByte(outputCount * 2);   ///// numberOfOuput ( = number of output x 2) by hex because each value of output takes 2 bytes
                    //if (addressPrefix.Equals("D"))
                    //{
                    //    dec_address = temp - 14; ///// D0014 = address 0 --> address = 0014 - 14
                    //}
                    //else
                    //{
                    //    dec_address = temp - 11; ///// K0011 = address 0 --> address = 0011 - 11
                    //}
                    break;
                default:
                    throw new NotSupportedException($"Function code <{functionCode}> is not supported");
            }

            List<byte> buffers = new List<byte>();
            int dec_addressUpper = dec_address >> 8;
            int dec_adressLower = dec_address & 0xFF;
            //// prepare data to write
            byte b_stationNumber = Convert.ToByte(stationNumber);
            byte b_functionCode = Convert.ToByte(functionCode);
            byte b_addressUpper = Convert.ToByte(dec_addressUpper);
            byte b_addressLower = Convert.ToByte(dec_adressLower);
            byte b_dataSizeUpper = Convert.ToByte(outputCount >> 8);
            byte b_dataSizeLower = Convert.ToByte(outputCount & 0xFF);
            
            //Console.WriteLine($"b_stationNumber: {string.Join("", b_stationNumber.ToString("X2"))}");
            //Console.WriteLine($"b_functionCode: {string.Join("", b_functionCode.ToString("X2"))}");
            //Console.WriteLine($"StartAddress: {string.Join("", b_addressUpper.ToString("X2"), b_addressLower.ToString("X2"))}");
            //Console.WriteLine($"datasize: {string.Join("", b_dataSizeUpper.ToString("X2"), b_dataSizeLower.ToString("X2"))}");
            //Console.WriteLine($"b_outputCount: {b_outputCount}");
            List<byte> b_requestMessage = new List<byte> {
                b_stationNumber,
                b_functionCode,
                b_addressUpper,
                b_addressLower,
                b_dataSizeUpper,
                b_dataSizeLower,
                b_outputCount
            };



            //// convert value to hex, with word value
            if (functionCode == 16)
            {
                //// append data to be writen to request message:
                for (int i = 0; i < outputCount; i++)
                {
                    int b_upper = values[i] >> 8;
                    int b_lower = values[i] & 0xFF;
                    b_requestMessage.Add(Convert.ToByte(b_upper));
                    b_requestMessage.Add(Convert.ToByte(b_lower));
                }
            }
            //// convert value to hex, with bit value
            /////// if write bit, parsing values into set of bytes (8bit) 
            ////// remember that byte aranged like this:
            //// 7-6-5-4-3-2-1-0 (first byte) 15-14-13-12-11-10-9(second byte) 23-22-21-20-19-18-17-16(third byte)...
            //// then convert each byte into hexa
            else if (functionCode == 15)
            {
                int offset = 0;
                int dataSize = (int)Math.Ceiling(values.Count / 8.0);
                string str_values = string.Join("", values).PadRight(dataSize*8,'0');
                for (int i = 0; i < dataSize; i++)
                {
                    //// binary string (예: 01111000)
                    string str_temp = Utils.ReverseString(str_values.Substring(offset, 8));
                    offset += 8;
                    //// convert to byte
                    byte b_temp = Convert.ToByte(Convert.ToInt32(str_temp,fromBase:2));
                    /// add to request
                    b_requestMessage.Add(b_temp);
                }
            }

            
            /// calculate CRC
            var crc = Crc16.ModRTU_CRC(b_requestMessage.ToArray());
            byte b_crcUpper = Convert.ToByte(crc >> 8);
            byte b_crcLower = Convert.ToByte(crc & 0xFF);
            //// ADD CRC to request
            b_requestMessage.Add(b_crcLower);
            b_requestMessage.Add(b_crcUpper);
            //Console.WriteLine("Final Write request:");
            //for (int i = 0; i < b_requestMessage.Count; i += 1)
            //{
            //    Console.Write($"{b_requestMessage[i].ToString("X2")} ");
            //}
            //Console.WriteLine("");
            /// request
            Write(b_requestMessage.ToArray());
            /// get and process response
            bool stop = false;
            while (!stop)
            {
                buffers.Clear();
                byte res_stationNumber = ReadOne(); /// station code

                if (res_stationNumber == 0)
                {
                    //log.Write($"Error while request to PLC, check the request message...");
                    throw new Exception($"Error while sending request to PLC, check the request message...");
                }

                if (res_stationNumber == stationNumber)
                {
                    byte res_functionCode = ReadOne();
                    if (res_functionCode == 15 || res_functionCode == 16)
                    {
                        var res_addressUpper = ReadOne();
                        var res_addressLower = ReadOne();
                        if (res_addressUpper == dec_addressUpper && res_addressLower == dec_adressLower)
                        {
                            var res_dataSizeUpper = ReadOne();
                            var res_dataSizeLower = ReadOne();

                            if (res_dataSizeUpper == b_dataSizeUpper && res_dataSizeLower == b_dataSizeLower)
                            {
                                var res_crcUpper = ReadOne();
                                var res_crcLower = ReadOne();
                                buffers.Add(res_stationNumber);
                                buffers.Add(res_functionCode);
                                buffers.Add(res_addressUpper);
                                buffers.Add(res_addressLower);
                                buffers.Add(res_dataSizeUpper);
                                buffers.Add(res_dataSizeLower);

                                var temp_crc = Crc16.ModRTU_CRC(buffers.ToArray());

                                if ((temp_crc >> 8) == res_crcLower && ((temp_crc & 0xFF) == res_crcUpper))
                                {
                                    /// write success.
                                    //log.Write($"Final response: '{string.Join(" ", buffers.ToArray())}'");
                                }
                            }
                        }

                        //else
                        //{
                        //    throw new Exception($"PLC returns unconsistant length of data (received: <{data.Length}>, expected: <6>");
                        //}
                    }
                    else if (res_functionCode == 95 || res_functionCode == 96 || res_functionCode == 90)
                    {
                        log.Write($"WRITE ERROR... ");
                    }
                    //else
                    //{
                    //    throw new Exception($"PLC returns invalid function code (<{res_functionCode}>)");
                    //}
                    stop = true;
                }
                else
                {
                    buffers.Clear();
                    break;
                }
            }
        }
    }
}
