using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenerateAPng
{
    // このコードは以下のPNG仕様書のAppendixからの移植版です。
    // https://datatracker.ietf.org/doc/html/rfc2083#section-15
    class Crc32
    {
        private static uint[] crcTable = MakeCrcTable();

        private static uint[] MakeCrcTable()
        {
            crcTable = new uint[256];

            uint c;
            int n, k;
            for (n = 0; n < 256; n++)
            {
                c = (uint)n;
                for (k = 0; k < 8; k++)
                {
                    if ((c & 1) > 0)
                    {
                        c = 0xedb88320 ^ (c >> 1);
                    }
                    else
                    {
                        c = c >> 1;
                    }
                }
                crcTable[n] = c;
            }
            return crcTable;
        }

        private static uint UpdateCrc(uint crc, byte[] buf)
        {
            uint c = crc;
            int n;

            for (n = 0; n < buf.Length; n++)
            {
                c = crcTable[(c ^ buf[n]) & 0xff] ^ (c >> 8);
            }
            return c;
        }

        public static uint CalcCrc(byte[] buf)
        {
            return UpdateCrc(0xffffffff, buf) ^ 0xffffffff;
        }
    }
}
