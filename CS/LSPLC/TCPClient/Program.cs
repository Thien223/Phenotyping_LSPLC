using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPClient
{
    internal class Program
    {

        static TcpClient me = null;
        static readonly string serverIP = "1.214.32.67";
        static readonly int serverPort = 3395;

        public static int SendHeartBeatCount { get; set; }
        public static bool IsConnected=false;
        static string Data;
        /// <summary>
        /// read bytes from tcp stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        private static byte[] ReadBytes(NetworkStream stream, int len)
        {
            try
            {
                byte[] buffer = new byte[len];
                int sum = 0;

                int cnt = stream.Read(buffer, sum, len - sum);
                if (cnt <= 0)
                {
                    return null; ;
                }
                else
                {
                    return buffer;
                }
                //while (len > sum)
                //{
                //    int cnt = stream.Read(buffer, sum, len - sum);
                //    if (cnt <= 0)
                //    {
                //        break;
                //    }
                //    else
                //    {
                //        sum += cnt;
                //    }
                //} // end while
                //return buffer;
            }
            catch
            {
                return null;
            }
        }
        private static void CheckRead()
        {
            if (me.Client.Poll(1 * 500 * 1000, SelectMode.SelectRead))
            {
                byte[] stream = ReadBytes(me.GetStream(), 90000);

                if (stream == null)
                {
                    CloseClient();
                }
                else
                {
                    int len = stream.Length;
                    //// loop and cut off the null byte
                    for (int i = stream.Length - 1; i > 0; i--)
                    {
                        if (stream[i] == 0)
                        {
                            len--;
                        }
                    }
                    //var newStream = new byte[len];
                    Array.Resize(ref stream, len);
                    Data = Encoding.ASCII.GetString(stream);
                }
                Task.Delay(50);
            } // end if
        }
        /// <summary>
        /// keep in touch with TCP server
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private static bool SendHeartBeat(TcpClient client)
        {
            try
            {
                byte[] stream = new byte[12];
                int offset = 0;
                offset = sam.ArrayHelper2.Concat(ref stream, offset, 1);

                client.GetStream().Write(stream, 0, stream.Length);
                Console.WriteLine($"Heartbeat sent....");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error {ex.Message}\n{ex.StackTrace}....");
                return false;
            }
        }


        private static void CreateClient()
        {
            try
            {
                me = new TcpClient();
                me.Connect(serverIP, serverPort);
                SendHeartBeatCount = 1;
                IsConnected = true;
                Console.WriteLine($"Connected to {serverIP}:{serverPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not connect to {serverIP}:{serverPort}");
                Console.WriteLine($"error: {ex.Message}\n{ex.StackTrace}....");
                IsConnected = false;
                CloseClient();
            }
        }
        private static void CloseClient()
        {
            if (me != null)
            {
                me.Close();
                me = null;
            }
        }
        static void Main(string[] args)
        {
            while (true)
            {
                DateTime lastHeartBeatTime = DateTime.Now;

                if (me == null)
                {
                    CreateClient();
                    System.Threading.Thread.Sleep(1000);
                    continue;
                }
                CheckRead();
                Console.WriteLine(Data);
                Console.WriteLine("\n\n");
                if ((DateTime.Now - lastHeartBeatTime).TotalSeconds > 60 && me != null)
                {
                    var sent = SendHeartBeat(me);
                    if (!sent)
                    {
                        CloseClient();
                    }
                } // end if

                System.Threading.Thread.Sleep(10);
            }
        }
    }
}
