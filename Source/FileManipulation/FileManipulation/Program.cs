using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace FileManipulation
{
    enum zip_command
    {
        Sweep,
        Zip,
        Change
    }
    class Program
    {
        private static FileStream read_stream;
        private static FileStream write_stream;
        private static int read_remaining;

        private static byte[] bytes;
        private static List<byte> list_bytes = new List<byte>();
        private static int[] most_bytes = new int[256];

        private static byte zip_tag_old;
        private static byte zip_tag;
        private static bool EOF;

        private static int less_count;
        private static int total;

        static void Main()
        {
            read_stream = new FileStream("test.avi", FileMode.Open);
            write_stream = new FileStream("result.avi", FileMode.Create);
            read_remaining = (int)read_stream.Length;
            Console.WriteLine(read_remaining);
            while (!EOF)
            {
                ReadFile();
                Zipping();
                WriteFile();
                total += bytes.Length;
                Reset();
            }
            Console.WriteLine(less_count);
            Console.WriteLine(total);
            //Print();
            Console.ReadKey();
        }

        #region 压缩命令
        /*
         * 使用最少的数为tag，把那些数换成0(少于4的替换成0)
         * 格式:
         * tag
         * 0
         */
        /*
         * 把较多的连续的数替换(多于3)
         * 格式:
         * tag
         * 命令序号
         * 数量
         * 被替换的byte
         */
        /*
         * 更换tag
         * 格式:
         * tag
         * 命令序号
         * 新tag
         */
        #endregion
        
        private static void Zipping()
        {
            #region 寻找最少的为tag
            int less_byte_count = int.MaxValue;
            for (int i = 0; i < bytes.Length; i++)
            {
                most_bytes[bytes[i]] += 1;
            }
            for (int i = 0; i < most_bytes.Length; i++)
            {
                if (most_bytes[i] < less_byte_count)
                {
                    zip_tag = (byte) i;
                    less_byte_count = most_bytes[i];
                }
            }
            Console.WriteLine(zip_tag + " " + less_byte_count);
            #endregion

            list_bytes.Add(zip_tag_old);
            list_bytes.Add(GetByte(zip_command.Change));
            list_bytes.Add(zip_tag);

            #region 压缩
            int most_continue = 0;
            int most_continue_count = 1;
            for (int i = 3; i < bytes.Length; i++)
            {
                byte now_byte = bytes[i];
                int a = FindContinues(now_byte, i);
                if (a > most_continue_count)
                {
                    most_continue = now_byte;
                    most_continue_count = a;
                }
                if (a < 4 && now_byte == zip_tag)
                {
                    for (int c = 0; c < a; c++)
                    {
                        list_bytes.Add(zip_tag);
                        list_bytes.Add(0);
                    }
                    i += a - 1;
                }
                else if (a > 3)
                {
                    i += a - 1;
                    while (a > 0)
                    {
                        if (a > 255)
                        {
                            list_bytes.Add(zip_tag);
                            list_bytes.Add(GetByte(zip_command.Zip));
                            list_bytes.Add(255);
                            list_bytes.Add(now_byte);
                            less_count += 251;
                            a -= 255;
                        }
                        if (a < 255)
                        {
                            list_bytes.Add(zip_tag);
                            list_bytes.Add(GetByte(zip_command.Zip));
                            list_bytes.Add((byte) a);
                            list_bytes.Add(now_byte);
                            less_count += a - 4;
                            a = 0;
                        }
                    }
                }
                else
                {
                    for (int c = 0; c < a; c++)
                    {
                        list_bytes.Add(bytes[i]);
                    }
                    i += a - 1;
                }
            }
            zip_tag_old = zip_tag;
            //Console.WriteLine(most_continue + " " + most_continue_count);
            #endregion
        }

        private static void Reset()
        {
            bytes = null;
            list_bytes = new List<byte>();
            most_bytes = new int[256];
            GC.Collect();
        }

        private static int FindContinues(Byte b, int count)
        {
            if (bytes.Length == count + 1) return 1;
            if (bytes[count + 1] == b)
            {
                return FindContinues(b, count + 1) + 1;
            }
            return 1;
        }

        private static void ReadFile()
        {
            int count = 10000000;
            int remaining = count;
            int offset = 0;
            while (remaining > 0)
            {
                int read;
                if (read_remaining > count)
                {
                    bytes = new byte[count];
                    read = read_stream.Read(bytes, offset, count);
                    read_remaining -= count;
                }
                else
                {
                    bytes = new byte[read_remaining];
                    read = read_stream.Read(bytes, offset, read_remaining);
                    remaining = read_remaining;
                    read_remaining = 0;
                    EOF = true;
                }
                if (read <= 0)
                {
                    Console.WriteLine("Read file failed!");
                    return;
                }
                offset += read;
                remaining -= read;
            }
        }

        private static void WriteFile()
        {
            bytes = list_bytes.ToArray();
            write_stream.Write(bytes, 0, bytes.Length);
        }

        private static void Print()
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                Console.WriteLine(bytes[i]);
            }
        }

        private static byte GetByte(zip_command c)
        {
            switch (c)
            {
                case zip_command.Sweep:
                {
                    return 0;
                }
                case zip_command.Zip:
                {
                    return 1;
                }
                case zip_command.Change:
                {
                    return 3;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(c), c, null);
            }
        }
    }
}
