using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace NovaParse
{
    internal static class Program
    {
        public static Config Config;
        public static StreamWriter LogFile;

        public static bool Auto = false;
        public static bool Export = true;

        public static void WriteError(Exception e, string message)
        {
            string text = $"{message} - {e}\n{e.InnerException}";

            Console.ForegroundColor = ConsoleColor.Red;

            Console.Write(text);
            LogFile.Write(text);

            if (!Auto)
                Console.ReadLine();
            Environment.Exit(-1);
        }

        private static void ParseArgs(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.ToLower() == "--auto")
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Automation mode active");

                    Auto = true;

                    break;
                }

                if (arg.ToLower() == "--export")
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Exporting JSON files");

                    Export = true;

                    break;
                }
            }
        }

        private static void Main(string[] args)
        {
            Console.Title = "NovaParse";

            try
            {
                ParseArgs(args);

                Config = Config.Load();

                File.Delete(Config.LogFile);
                LogFile = new StreamWriter(Config.LogFile);

                if (!Export)
                {
                    Task download = Task.Factory.StartNew(Downloader.DownloadZip);
                    download.Wait();

                    Task extract = Task.Factory.StartNew(Downloader.ExtractZip);
                    extract.Wait();
                }

                Task parse = Task.Factory.StartNew(Parser.Parse);
                parse.Wait();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done!");
                Console.ForegroundColor = ConsoleColor.Gray;

                if (!Auto)
                    Console.ReadLine();
            }
            catch (Exception e)
            {
                WriteError(e, "Generic error");
            }
        }
    }
}
