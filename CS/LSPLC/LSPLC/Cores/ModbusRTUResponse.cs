using System.Collections.Generic;
using System.Net.Sockets;

namespace LSPLC.Cores
{
    public class ModbusRTUResponse
    {
        public string result;
        public string message;
        public Dictionary<string, int> cur_value;
    }
}
