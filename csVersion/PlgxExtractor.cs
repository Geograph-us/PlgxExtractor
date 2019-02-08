using System;
using System.Text;
using System.Linq;
using System.IO;
using System.IO.Compression;

namespace PlgxExtractor
{
    class Program
    {
        static string LastError = "\r\n";

        static void Main()
        {
            var files = Directory.GetFiles(".", "*.plgx").ToList();
            Console.WriteLine("Select PLGX-files to extract (comma separated):");
            Console.WriteLine("0 - All");
            for (int i = 0; i < files.Count; i++)
            {
                Console.WriteLine((i + 1).ToString() + " - " + files[i]);
            }
            var choice = Console.ReadLine();
            Console.WriteLine();

            var choices = choice.Split(',').Select(int.Parse);
            if (!choices.Contains(0)) files = files.Where((x, i) => choices.Contains(i + 1)).ToList();

            for (int i = 0; i < files.Count; i++)
            {
                Extract(files[i]);
            }

            Console.Write(LastError);

            Console.Write("Press Enter to exit...");
            Console.ReadLine();
        }

        static void Extract(string filename)
        {
            var filesBeginPattern = BitConverter.GetBytes(0x0004000000000003);
            var dirname = Path.GetFileNameWithoutExtension(filename);
            Console.Write("[" + filename + "] ");

            using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var br = new BinaryReader(fs))
                {
                    if (br.ReadInt32() != 0x65d90719)
                    {
                        Console.WriteLine("ERROR: not PLGX-file");
                        LastError += "ERROR: not PLGX-file " + filename + "\r\n";
                        return;
                    }
                    // Plugin-name
                    fs.Position = 0x24;
                    var size = br.ReadInt32();
                    var value = br.ReadBytes(size);
                    var plugName = Encoding.Default.GetString(value);

                    // Plugin creation date
                    fs.Position += 2;
                    size = br.ReadInt32();
                    value = br.ReadBytes(size);
                    var date = DateTime.Parse(Encoding.Default.GetString(value));

                    // Plugin creation tool
                    fs.Position += 2;
                    size = br.ReadInt32();
                    value = br.ReadBytes(size);
                    var creationToolName = Encoding.Default.GetString(value);
                    Console.WriteLine("[" + plugName + "] [" + date.ToString() + "] [" + creationToolName + "]");

                    // Go to files list
                    var bytesCount = 500;
                    var filesBegin = Search(br.ReadBytes(bytesCount), filesBeginPattern);

                    if (filesBegin == -1)
                    {
                        Console.WriteLine("ERROR: files list not found");
                        LastError += "ERROR: files list not found " + filename + "\r\n";
                        return;
                    }

                    fs.Position = fs.Position - bytesCount + filesBegin + 14;

                    var isOk = true;
                    while (true)
                    {
                        size = br.ReadInt32();
                        if (size < 1) break;
                        value = br.ReadBytes(size);

                        // filename
                        var name = Encoding.Default.GetString(value);
                        fs.Position += 2;

                        // gzipped file size
                        size = br.ReadInt32();
                        var buffer = GzipDecompress(br.ReadBytes(size));
                        Console.WriteLine(name + ". Compressed Size: " + size.ToString() + ". Size: " + buffer.Length);

                        // fix relative path
                        name = name.Replace("../", "").Replace(@"..\", "");
                        var path = Path.Combine(dirname, name);
                        var folder = Path.GetDirectoryName(path);
                        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                        if (buffer.Length > 0 || size == 0) File.WriteAllBytes(path, buffer);
                        else
                        {
                            Console.WriteLine("Can't Extract File: " + name);
                            isOk = false;
                        }

                        fs.Position += 14;
                    }

                    if (!isOk) LastError += "ERROR: not all files was extracted " + filename + "\r\n";
                }
            }
            Console.WriteLine("Extracted!");
            Console.WriteLine();
        }

        static int Search(byte[] sIn, byte[] sFor)
        {
            int[] numArray = new int[256];
            int num1 = 0;
            int num2 = sFor.Length - 1;
            for (int index = 0; index < 256; ++index) numArray[index] = sFor.Length;
            for (int index = 0; index < num2; ++index) numArray[(int)sFor[index]] = num2 - index;
            while (num1 <= sIn.Length - sFor.Length)
            {
                for (int index = num2; (int)sIn[num1 + index] == (int)sFor[index]; --index)
                {
                    if (index == 0) return num1;
                }
                num1 += numArray[(int)sIn[num1 + num2]];
            }
            return -1;
        }

        static byte[] GzipDecompress(byte[] data)
        {
            byte[] decompressedArray = null;
            try
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (MemoryStream compressStream = new MemoryStream(data))
                    {
                        using (var deflateStream = new GZipStream(compressStream, CompressionMode.Decompress))
                        {
                            deflateStream.CopyTo(decompressedStream);
                        }
                    }
                    decompressedArray = decompressedStream.ToArray();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            return decompressedArray;
        }
    }
}
