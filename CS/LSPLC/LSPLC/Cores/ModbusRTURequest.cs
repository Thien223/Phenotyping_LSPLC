using System.Collections.Generic;
using System.Net.Sockets;

namespace LSPLC.Cores
{
    public class ModbusRTURequest
    {
        public string command;
        public int plc_no;
        public Dictionary<string, int> run_stop;
        public Dictionary<string, int> start_end;
        public Dictionary<string, int> set_value;
        public Socket client;
    }
}
