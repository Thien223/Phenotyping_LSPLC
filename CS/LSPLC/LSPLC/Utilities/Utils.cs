using System;
using System.Text;

namespace LSPLC.Utilities
{
    public static class Utils
    {
        public static byte[] ConvertToHex(long value, int size = 2)
        {
            return Encoding.ASCII.GetBytes(value.ToString("X" + size));
        }
    }


    public static class Crc16
    {
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
