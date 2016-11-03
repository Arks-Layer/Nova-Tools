using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

namespace NovaParse
{
    public static class Parser
    {
        public static List<string> InputFiles = new List<string>();
        public static string BaseFile = string.Empty;

        public static Dictionary<string, StringEntry> OutputEntries = new Dictionary<string, StringEntry>();
        public static List<Dictionary<string, StringEntry>> InputEntries = new List<Dictionary<string, StringEntry>>();

        private static readonly Stopwatch Watch = new Stopwatch();

        public static void Parse()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Beginning parsing process...");

            if (Program.Export)
            {
                PopulateOutputEntries();
                Export();
            }
            else
            {
                PopulateFiles();
                PopulateOutputEntries();
                PopulateInputEntries();
                ParseEntries();
            }
        }

        private static void PopulateFiles()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Adding input files...");

            try
            {
                AddFiles(Program.Config.InputPath);
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error adding input files");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Added {InputFiles.Count} input files");
        }

        private static void PopulateOutputEntries()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Populating output entries...");

            try
            {
                BaseFile = File.ReadAllText(Program.Config.TranslationJsonFile);

                OutputEntries = JsonConvert.DeserializeObject<Dictionary<string, StringEntry>>(BaseFile);
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error populating output entries");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Added {OutputEntries.Count} output entries");
        }

        private static void PopulateInputEntries()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Populating input entries...");

            try
            {
                foreach (string file in InputFiles)
                {
                    if (!file.EndsWith(".json".ToLower())) continue;

                    string data = File.ReadAllText(file);

                    // Since the array might be malformed from copy-pasting, check for and do some brace insertions to fix that if necessary
                    if (!data.StartsWith("{"))
                    {
                        data = data.Insert(0, "{\r\n");
                        data = data.Insert(data.Length, "}\r\n");
                    }

                    InputEntries.Add(JsonConvert.DeserializeObject<Dictionary<string, StringEntry>>(data));
                }
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error populating input entries");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Added {InputEntries.Count} input dictionaries with {InputEntries.Sum(dicts => dicts.Count)} total entries");
        }

        private static void ParseEntries()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Parsing entries...");

            try
            {
                Watch.Start();

                foreach (Dictionary<string, StringEntry> inputEntry in InputEntries)
                    foreach (KeyValuePair<string, StringEntry> inputKvp in inputEntry)
                        foreach (KeyValuePair<string, StringEntry> outputKvp in OutputEntries)
                        {
                            if (inputKvp.Key != outputKvp.Key) continue;
                            if (!inputKvp.Value.Enabled) continue;

                            OutputEntries[outputKvp.Key].Text = inputKvp.Value.Text;
                            OutputEntries[outputKvp.Key].Enabled = inputKvp.Value.Enabled;

                            Program.LogFile.WriteLine($"Replacing entry {inputKvp.Key}: {outputKvp.Value.OriginalText.Replace('\n', ' ').Replace('\r', ' ')} -> {inputKvp.Value.Text.Replace('\n', ' ').Replace('\r', ' ')}");

                            break;
                        }

                File.WriteAllText(Program.Config.TranslationJsonFileOutput,
                    JsonConvert.SerializeObject(OutputEntries, Formatting.Indented));

                Watch.Stop();
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error parsing entries");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Finished parsing all entries in {Watch.ElapsedMilliseconds} ms");
        }

        private static void Export()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Exporting entries...");

            try
            {
                ExportConfig exportConfig = JsonConvert.DeserializeObject<ExportConfig>(File.ReadAllText(Program.Config.ExportFile));
                Dictionary<string, Dictionary<string, StringEntry>> exports = new Dictionary<string, Dictionary<string, StringEntry>>();

                foreach (ExportEntry entry in exportConfig.Entries)
                {
                    Dictionary<string, StringEntry> entryList = (from outputKvp in OutputEntries
                                                                 let ID = Convert.ToInt64(outputKvp.Key)
                                                                 where ID >= entry.MinID && ID <= entry.MaxID
                                                                 select outputKvp).ToDictionary(outputKvp => outputKvp.Key, outputKvp => outputKvp.Value);

                    exports.Add(entry.Name, entryList);
                }

                if (!Directory.Exists("Export"))
                    Directory.CreateDirectory("Export");

                foreach (KeyValuePair<string, Dictionary<string, StringEntry>> exportKvp in exports)
                    File.WriteAllText("Export\\" + exportKvp.Key + ".json", JsonConvert.SerializeObject(exportKvp.Value, Formatting.Indented));
            }
            catch (Exception e)
            {
                Program.WriteError(e, "Error exporting entries to split JSON files");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished exporting entries");
        }

        private static void AddFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
                InputFiles.Add(file);

            foreach (string folder in Directory.GetDirectories(path))
                AddFiles(folder);
        }
    }
}
