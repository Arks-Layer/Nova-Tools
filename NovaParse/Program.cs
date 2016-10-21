using System;
using System.IO;
using System.Threading.Tasks;

namespace NovaParse
{
    internal static class Program
    {
        public static Config Config;
        public static StreamWriter LogFile;

        public static void WriteError(Exception e, string message)
        {
            string text = $"{message} - {e}\n{e.InnerException}";

            Console.ForegroundColor = ConsoleColor.Red;

            Console.Write(text);
            LogFile.Write(text);

            Console.ReadLine();
            Environment.Exit(-1);
        }

        private static void Main(string[] args)
        {
            Console.Title = "NovaParse";

            try
            {
                Config = Config.Load();

                File.Delete(Config.LogFile);
                LogFile = new StreamWriter(Config.LogFile);

                Task download = Task.Factory.StartNew(Downloader.DownloadZip);
                download.Wait();

                Task extract = Task.Factory.StartNew(Downloader.ExtractZip);
                extract.Wait();

                Task parse = Task.Factory.StartNew(Parser.Parse);
                parse.Wait();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done!");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                WriteError(e, "Generic error");
            }
        }
    }
}
