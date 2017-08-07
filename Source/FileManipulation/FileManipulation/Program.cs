using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
//我就是不想删引用，万一用得到呢ヽ(￣▽￣)ﾉ

namespace FileManipulation
{
    enum Zip_command
    {
        Sweep,
        Zip,
        Change
    }
    class Program
    {
        //写入和读取的流
        private static FileStream read_stream;
        private static FileStream write_stream;

        private static byte[] bytes;     //用于存储读入的字节
        private static List<byte> result_bytes = new List<byte>();     //用于存储处理后的字节
        private static int[] most_bytes = new int[256];     //记录每个字节出现的次数
        
        private static int remnant_count;     //剩下的未被处理的字节量
        private static byte zip_tag_old;     //旧tag
        private static byte zip_tag;     //新tag
        private static bool EOF;     //是否处理到文件结尾

        private static int reduce_count;     //单个循环减少的字节
        private static int reduce_total;     //减少的字节的总数,并没有计算为了保证tag的可用性而增加的字节量

        static void Main()
        {
            //打开文件
            read_stream = new FileStream("test.avi", FileMode.Open);
            write_stream = new FileStream("result.avi", FileMode.Create);
            remnant_count = (int)read_stream.Length;
            Console.WriteLine(remnant_count);
            //循环处理，如果处理完了才结束
            while (!EOF)
            {
                ReadFile();
                Manipulating();
                WriteFile();
                reduce_total += bytes.Length;
                Reset();
            }
            read_stream.Close();
            write_stream.Close();
            Console.WriteLine(reduce_total);
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
        
        private static void Manipulating()
        {
            #region 寻找最少的为tag
            int less_byte_count = int.MaxValue;
            for (int i = 0; i < bytes.Length; i++)     //进行统计每个字节出现的次数
            {
                most_bytes[bytes[i]] += 1;
            }
            for (int i = 0; i < most_bytes.Length; i++)    //寻找出现次数最少的那个
            {
                if (most_bytes[i] < less_byte_count)
                {
                    zip_tag = (byte) i;
                    less_byte_count = most_bytes[i];
                }
            }
            Console.WriteLine(zip_tag + " " + less_byte_count);
            #endregion

            //添加压缩标识，以便在解压时替换tag
            result_bytes.Add(zip_tag_old);
            result_bytes.Add(GetByte(Zip_command.Change));
            result_bytes.Add(zip_tag);

            #region 压缩
            for (int i = 3; i < bytes.Length; i++)     //处理每个字节
            {
                byte now_byte = bytes[i];
                int a = FindContinues(now_byte, i);
                if (a < 4 && now_byte == zip_tag)     //如果是无法压缩的（按我设计的压缩算法），并且还和压缩tag一样的，就按照压缩命令0进行处理
                {
                    for (int c = 0; c < a; c++)
                    {
                        result_bytes.Add(zip_tag);
                        result_bytes.Add(0);
                    }
                    i += a - 1;
                }
                else if (a > 3)     //如果可压缩，那么就把这一串都处理了
                {
                    i += a - 1;
                    while (a > 0)     //循环处理连续的byte
                    {
                        if (a > 255)     //如果连续次数大于255，那就记为255，然后继续循环
                        {
                            result_bytes.Add(zip_tag);
                            result_bytes.Add(GetByte(Zip_command.Zip));
                            result_bytes.Add(255);
                            result_bytes.Add(now_byte);
                            reduce_count += 251;
                            a -= 255;
                        }
                        if (a < 255)     //如果连续次数小于255，那就把剩下的都添加进去
                        {
                            result_bytes.Add(zip_tag);
                            result_bytes.Add(GetByte(Zip_command.Zip));
                            result_bytes.Add((byte) a);
                            result_bytes.Add(now_byte);
                            reduce_count += a - 4;
                            a = 0;
                        }
                    }
                }
                else     //剩下的既不可压缩又不是压缩tag的就原封不动的添加到压缩结果里
                {
                    for (int c = 0; c < a; c++)
                    {
                        result_bytes.Add(bytes[i]);
                    }
                    i += a - 1;
                }
            }
            zip_tag_old = zip_tag;     //一轮压缩完毕，现在用的tag就成旧tag了
            #endregion
        }

        //一轮压缩完毕后重置        
        private static void Reset()
        {
            bytes = null;
            result_bytes = new List<byte>();
            most_bytes = new int[256];
            GC.Collect();
        }

        /// <summary>
        /// 获得一个byte连续出现多少次
        /// </summary>
        /// <param name="b">要检测的byte</param>
        /// <param name="count">在数组中的位置</param>
        /// <returns>返回连续出现的次数</returns>
        private static int FindContinues(Byte b, int count)
        {
            if (bytes.Length == count + 1) return 1;
            if (bytes[count + 1] == b)
            {
                return FindContinues(b, count + 1) + 1;
            }
            return 1;
        }

        //读取文件
        private static void ReadFile()
        {
            int count = 10000000;     //单次读取的byte个数
            int remaining = count;
            int offset = 0;
            while (remaining > 0)     //read方法可能一次读不完全部，循环保证是否读完
            {
                int read;
                if (remnant_count > count)
                {
                    bytes = new byte[count];
                    read = read_stream.Read(bytes, offset, count);
                    remnant_count -= count;
                }
                else
                {
                    bytes = new byte[remnant_count];
                    read = read_stream.Read(bytes, offset, remnant_count);
                    remaining = remnant_count;
                    remnant_count = 0;
                    EOF = true;
                }
                if (read <= 0)     //未读取到数据，报错~~
                {
                    Console.WriteLine("Read file failed!");
                    return;
                }
                offset += read;
                remaining -= read;
            }
        }

        //把处理后的结果写到文件内
        private static void WriteFile()
        {
            bytes = result_bytes.ToArray();
            write_stream.Write(bytes, 0, bytes.Length);
        }

        //打印内容到控制台,没什么用
        private static void Print()
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                Console.WriteLine(bytes[i]);
            }
        }

        //返回各个命令对应的字节
        private static byte GetByte(Zip_command c)
        {
            switch (c)
            {
                case Zip_command.Sweep:
                {
                    return 0;
                }
                case Zip_command.Zip:
                {
                    return 1;
                }
                case Zip_command.Change:
                {
                    return 3;
                }
                default:     //其他的东西？报错~~
                    throw new ArgumentOutOfRangeException(nameof(c), c, null);
            }
        }
    }
}
