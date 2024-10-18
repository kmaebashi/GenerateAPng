using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenerateAPng
{
    class Crc32
    {
        private static uint[] crcTable = makeCrcTable();

        private static uint[] makeCrcTable()
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

        private static uint updateCrc(uint crc, byte[] buf)
        {
            uint c = crc;
            int n;

            for (n = 0; n < buf.Length; n++)
            {
                c = crcTable[(c ^ buf[n]) & 0xff] ^ (c >> 8);
            }
            return c;
        }

        public static uint calcCrc(byte[] buf)
        {
            return updateCrc(0xffffffff, buf) ^ 0xffffffff;
        }
    }
}
