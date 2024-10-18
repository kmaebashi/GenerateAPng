using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GenerateAPng
{
    class Chunk
    {
        public String Name;
        public byte[] Data;
    }

    class Program
    {
        private const int NumOfFrame = 12;

        static void Main(string[] args)
        {
#if true
            ReadPngFile(@"files\test.png");
            ReadPngFile(@"files\Animated_PNG_example_bouncing_beach_ball.png");
#endif
            using (FileStream fs = new FileStream("apng.png", FileMode.Create, FileAccess.Write))
            {
                int sequence = 0;
                for (int i = 0; i < NumOfFrame; i++)
                {
                    byte[] onePng = CreatePng(i);
                    List<Chunk> chunkList = ReadPng(onePng);
                    Chunk idat = SearchIdat(chunkList);
                    if (i == 0)
                    {
                        WriteHeader(fs, chunkList);
                        WriteActl(fs);
                        WriteFctl(fs, sequence++);
                        WriteChunk(fs, idat);
                    }
                    else
                    {
                        WriteFctl(fs, sequence++);
                        WriteFdat(fs, sequence++, idat);
                    }
                }
                WriteIend(fs);
            }
            ReadPngFile(@"apng.png");
        }

        // 1コマ分のPNG画像を生成する。
        static byte[] CreatePng(int frame)
        {
            const int Width = 400;
            const int Height = 400;
            const int NumOfLine = 12;
            byte[] ret = null;

            using (Bitmap bitmap = new Bitmap(Width, Height))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Brush backBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
            {
                g.FillRectangle(backBrush, 0, 0, Width, Height);
                for (int i = 0; i < NumOfLine; i++)
                {
                    int alpha = (int)(255 * ((NumOfLine - i) / (double)NumOfLine));
                    Pen pen = new Pen(Color.FromArgb(alpha, 255, 255, 255), 20);
                    double angle = (NumOfLine - i + frame) * Math.PI * 2 / NumOfLine - (Math.PI / 2);
                    g.DrawLine(pen,
                               (int)(Width / 2 + Math.Cos(angle) * Width * 0.2),
                               (int)(Height / 2 + Math.Sin(angle) * Height * 0.2),
                               (int)(Width / 2 + Math.Cos(angle) * Width * 0.5),
                               (int)(Height / 2 + Math.Sin(angle) * Height * 0.5));
                }
                using (Bitmap resizedBitmap = new Bitmap(100, 100))
                using (Graphics resizeG = Graphics.FromImage(resizedBitmap))
                using (MemoryStream ms = new MemoryStream())
                {
                    resizeG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    resizeG.DrawImage(bitmap, 0, 0, 100, 100);
                    // resizedBitmap.Save("test" + frame.ToString("00") + ".png", ImageFormat.Png);
                    resizedBitmap.Save(ms, ImageFormat.Png);
                    ret = ms.ToArray();
                }
            }
            return ret;
        }

        #region PNG読み込み用のメソッド群
        static void ReadPngFile(string path)
        {
            Console.WriteLine("********** " + path + " **********");
            byte[] buf = null;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                buf = new byte[fs.Length];
                fs.Read(buf, 0, buf.Length);
            }
            ReadPng(buf);
        }

        static List<Chunk> ReadPng(byte[] buf)
        {
            Debug.Assert(buf[0] == 0x89);
            Debug.Assert(buf[1] == 'P');
            Debug.Assert(buf[2] == 'N');
            Debug.Assert(buf[3] == 'G');
            Debug.Assert(buf[4] == 0x0D);
            Debug.Assert(buf[5] == 0x0A);
            Debug.Assert(buf[6] == 0x1A);
            Debug.Assert(buf[7] == 0x0A);

            return ReadChunks(buf);
        }

        private static List<Chunk> ReadChunks(byte[] buf)
        {
            List<Chunk> chunkList = new List<Chunk>();

            for (uint idx = 8; idx < buf.Length;)
            {
                Chunk chunk = new Chunk();
                idx = ReadChunk(buf, idx, chunk);
                chunkList.Add(chunk);
            }

            return chunkList;
        }


        private static uint ReadChunk(byte[] buf, uint idx, Chunk chunk)
        {
            uint chunkSize = ReadInt32(buf, idx);

            string chunkName = ((char)buf[idx + 4]).ToString()
                + ((char)buf[idx + 5]).ToString()
                + ((char)buf[idx + 6]).ToString()
                + ((char)buf[idx + 7]).ToString();
            Console.WriteLine("Chunk name.." + chunkName + " size.." + chunkSize);

            chunk.Name = chunkName;
            chunk.Data = new byte[chunkSize];
            Array.Copy(buf, idx + 8, chunk.Data, 0, chunkSize);


            if (chunkName == "IHDR")
            {
                DumpIHDR(buf, idx + 8);
            }
            else if (chunkName == "acTL")
            {
                DumpAcTL(buf, idx + 8);
            }
            else if (chunkName == "fcTL")
            {
                DumpFcTL(buf, idx + 8);
            }
            else if (chunkName == "fdAT")
            {
                DumpFdAT(buf, idx + 8);
            }

            uint crc = ReadInt32(buf, idx + chunkSize + 8);
            byte[] dataBuf = new byte[chunkSize + 4];
            Array.Copy(buf, idx + 4, dataBuf, 0, chunkSize + 4);
            uint computedCrc = Crc32.calcCrc(dataBuf);
            Debug.Assert(crc == computedCrc);
            // チャンクサイズはデータ部のみを指すので、チャンクサイズ、チャンクの種類、
            // 末尾のCRCの分で4バイト3つ分の12を加える。
            idx += chunkSize + 12;

            return idx;
        }

        private static void DumpIHDR(byte[] buf, uint idx)
        {
            Console.WriteLine("  Width.." + ReadInt32(buf, idx));
            Console.WriteLine("  Height.." + ReadInt32(buf, idx + 4));
            Console.WriteLine("  Bit depth.." + buf[idx + 8]);
            Console.WriteLine("  Color type.." + buf[idx + 9]);
            Console.WriteLine("  Compression method.." + buf[idx + 10]);
            Console.WriteLine("  Filter method.." + buf[idx + 11]);
            Console.WriteLine("  Interlace method.." + buf[idx + 12]);
        }

        private static void DumpAcTL(byte[] buf, uint idx)
        {
            Console.WriteLine("  num_frames.." + ReadInt32(buf, idx));
            Console.WriteLine("  num_plays.." + ReadInt32(buf, idx + 4));
        }

        private static void DumpFcTL(byte[] buf, uint idx)
        {
            uint localIdx = idx;
            Console.WriteLine("  sequence_number.." + ReadInt32(buf, localIdx));
            localIdx += 4;
            Console.WriteLine("  width.." + ReadInt32(buf, localIdx));
            localIdx += 4;
            Console.WriteLine("  height.." + ReadInt32(buf, localIdx));
            localIdx += 4;
            Console.WriteLine("  x_offset.." + ReadInt32(buf, localIdx));
            localIdx += 4;
            Console.WriteLine("  y_offset.." + ReadInt32(buf, localIdx));
            localIdx += 4;
            Console.WriteLine("  delay_num.." + ReadInt16(buf, localIdx));
            localIdx += 2;
            Console.WriteLine("  delay_den.." + ReadInt16(buf, localIdx));
            localIdx += 2;
            Console.WriteLine("  dispose_op.." + buf[localIdx]);
            localIdx += 1;
            Console.WriteLine("  blend_op.." + buf[localIdx]);
            localIdx += 1;
        }

        private static void DumpFdAT(byte[] buf, uint idx)
        {
            Console.WriteLine("  sequence_number.." + ReadInt32(buf, idx));
        }

        private static uint ReadInt32(byte[] buf, uint idx)
        {
            return (uint)(buf[idx] << 24) + (uint)(buf[idx + 1] << 16)
                 + (uint)(buf[idx + 2] << 8) + (uint)(buf[idx + 3]);
        }

        private static uint ReadInt16(byte[] buf, uint idx)
        {
            return (uint)(buf[idx] << 8) + (uint)(buf[idx + 1]);
        }

        private static Chunk SearchIdat(List<Chunk> chunkList)
        {
            foreach (Chunk chunk in chunkList)
            {
                if (chunk.Name == "IDAT")
                {
                    return chunk;
                }
            }
            throw new ApplicationException("IDATが見つかりません。、");
        }
        #endregion

        #region PNG出力用のメソッド群
        private static void WriteHeader(FileStream fs, List<Chunk> chunkList)
        {
            fs.WriteByte(0x89);
            fs.WriteByte((byte)'P');
            fs.WriteByte((byte)'N');
            fs.WriteByte((byte)'G');
            fs.WriteByte(0x0D);
            fs.WriteByte(0x0A);
            fs.WriteByte(0x1A);
            fs.WriteByte(0x0A);

            foreach (Chunk chunk in chunkList)
            {
                if (chunk.Name == "IDAT")
                {
                    break;
                }
                WriteChunk(fs, chunk);
            }
        }

        private static void WriteChunk(FileStream fs, Chunk chunk)
        {
            WriteInt32(fs, (uint)(chunk.Data.Length));
            byte[] buf = new byte[chunk.Data.Length + 4];
            StringToByteArray(chunk.Name, buf, 0);
            Array.Copy(chunk.Data, 0, buf, 4, chunk.Data.Length);

            fs.Write(buf, 0, buf.Length);
            uint crc = Crc32.calcCrc(buf);
            WriteInt32(fs, crc);
        }

        private static void WriteString(FileStream fs, string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                fs.WriteByte((byte)str[i]);
            }
        }

        private static void WriteActl(FileStream fs)
        {
            WriteInt32(fs, 8);
            byte[] buf = new byte[12];
            StringToByteArray("acTL", buf, 0);
            Int32ToByteArray(NumOfFrame, buf, 4); // num_frames
            Int32ToByteArray(0, buf, 8); // num_plays
            fs.Write(buf, 0, buf.Length);
            uint crc = Crc32.calcCrc(buf);
            WriteInt32(fs, crc);
        }

        private static void WriteFctl(FileStream fs, int sequence)
        {
            WriteInt32(fs, 26);
            byte[] buf = new byte[30];
            int offset = 0;
            StringToByteArray("fcTL", buf, offset);
            offset += 4;
            Int32ToByteArray((uint)sequence, buf, offset);
            offset += 4;
            Int32ToByteArray(100, buf, offset); // width
            offset += 4;
            Int32ToByteArray(100, buf, offset); // height
            offset += 4;
            Int32ToByteArray(0, buf, offset); // x_offset
            offset += 4;
            Int32ToByteArray(0, buf, offset); // y_offset
            offset += 4;
            Int16ToByteArray(100, buf, offset); // delay_num
            offset += 2;
            Int16ToByteArray(1000, buf, offset); // delay_den
            offset += 2;
            buf[offset++] = 1;
            buf[offset++] = 0;
            Debug.Assert(offset == 30);

            fs.Write(buf, 0, buf.Length);
            uint crc = Crc32.calcCrc(buf);
            WriteInt32(fs, crc);
        }

        private static void WriteFdat(FileStream fs, int sequence, Chunk chunk)
        {
            WriteInt32(fs, (uint)(chunk.Data.Length + 4));
            byte[] buf = new byte[chunk.Data.Length + 8];
            StringToByteArray("fdAT", buf, 0);
            Int32ToByteArray((uint)sequence, buf, 4);
            Array.Copy(chunk.Data, 0, buf, 8, chunk.Data.Length);

            fs.Write(buf, 0, buf.Length);
            uint crc = Crc32.calcCrc(buf);
            WriteInt32(fs, crc);
        }

        private static void WriteIend(FileStream fs)
        {
            WriteInt32(fs, 0);
            byte[] buf = new byte[4];
            StringToByteArray("IEND", buf, 0);
            fs.Write(buf, 0, buf.Length);
            uint crc = Crc32.calcCrc(buf);
            WriteInt32(fs, crc);
        }

        private static void WriteInt32(FileStream fs, uint value)
        {
            fs.WriteByte((byte)((value >> 24) & 0xff));
            fs.WriteByte((byte)((value >> 16) & 0xff));
            fs.WriteByte((byte)((value >> 8) & 0xff));
            fs.WriteByte((byte)(value & 0xff));
        }

        private static void StringToByteArray(string str, byte[] buf, int offset)
        {
            for (int i = 0; i < str.Length; i++)
            {
                buf[offset + i] = (byte)str[i];
            }
        }

        private static void Int32ToByteArray(uint value, byte[] buf, int offset)
        {
            buf[offset] = ((byte)((value >> 24) & 0xff));
            buf[offset + 1] = ((byte)((value >> 16) & 0xff));
            buf[offset + 2] = ((byte)((value >> 8) & 0xff));
            buf[offset + 3] = ((byte)(value & 0xff));
        }

        private static void Int16ToByteArray(uint value, byte[] buf, int offset)
        {
            buf[offset] = ((byte)((value >> 8) & 0xff));
            buf[offset + 1] = ((byte)(value & 0xff));
        }
        #endregion
    }
}
