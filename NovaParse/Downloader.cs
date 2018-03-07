using System;
using System.Net;
using System.Threading;

using Ionic.Zip;

namespace NovaParse
{
    public static class Downloader
    {
        public static void DownloadZip()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; //Fuck off Cloudflare [Aida]
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Downloading commit ZIP file...");

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadProgressChanged += Client_DownloadProgressChanged;
                    client.DownloadFileCompleted += Client_DownloadFileCompleted;

                    client.DownloadFileAsync(new Uri(Program.Config.DownloadUrl), Program.Config.DownloadFileName);

                    // This is a stupid kludge, but in order for the WebClient to propogate the Download* events, it needs to be run async
                    while (client.IsBusy)
                        Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error while downloading ZIP file");
            }
        }

        public static void ExtractZip()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Extracting commit ZIP file...");

            try
            {
                using (ZipFile file = ZipFile.Read(Program.Config.DownloadFileName))
                {
                    file.ExtractProgress += File_ExtractProgress;

                    file.ExtractAll(".", ExtractExistingFileAction.OverwriteSilently);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Done!".PadRight(40));
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error extracting ZIP file");
            }
        }

        private static void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write((e.BytesReceived / 1024 + " KB / " + e.TotalBytesToReceive / 1024 + " KB").PadRight(40) + "\r");
        }

        private static void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done!".PadRight(80));
        }

        private static void File_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (e.EntriesExtracted > 0)
                Console.Write((e.ArchiveName + ": " + e.EntriesExtracted + " / " + e.EntriesTotal).PadRight(40) + "\r");
        }
    }
}
