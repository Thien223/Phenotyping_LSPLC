﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSPLC.Cores
{
    public class ModbusRTURequest
    {
        public int stationNumber;
        public int functionCode;
        public string startVariable;
        public int dataSize;

    }
}
