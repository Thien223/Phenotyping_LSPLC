using LSPLC.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LSPLC.Utilities
{
    public static class Utils
    {
        public static byte[] ConvertToHex(long value, int size = 2)
        {
            return Encoding.ASCII.GetBytes(value.ToString("X" + size));
        }



        private static readonly uint[] _lookup32 = CreateLookup32();

        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
            }
            return result;
        }

        public static string ByteArrayToHexViaLookup32(byte[] bytes)
        {
            var lookup32 = _lookup32;
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }

    }


    public static class Crc16
    {
        public static ushort ComputeCRC(byte[] buf)
        {
            ushort crc = 0xFFFF;
            int len = buf.Length;

            for (int pos = 0; pos < len; pos++)
            {
                crc ^= buf[pos];

                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                        crc >>= 1;
                }
            }
            //return crc; lo-hi
            //bytes hi-lo reordered
            return (ushort)((crc >> 8) | (crc << 8));
        }

        // Compute the MODBUS RTU CRC
        public static ushort ModRTU_CRC(byte[] buf)
        {
            int len = buf.Length;
            UInt16 crc = 0xFFFF;

            for (int pos = 0; pos < len; pos++)
            {
                crc ^= (UInt16)buf[pos];          // XOR byte into least sig. byte of crc

                for (int i = 8; i != 0; i--)      // Loop over each bit
                {
                    if ((crc & 0x0001) != 0)        // If the LSB is set
                    {
                        crc >>= 1;                    // Shift right and XOR 0xA001
                        crc ^= 0xA001;
                    }
                    else                            // Else LSB is not set
                    {
                        crc >>= 1;                    // Just shift right
                    }
                }
            }
            // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
            return crc;
        }
    }
}
