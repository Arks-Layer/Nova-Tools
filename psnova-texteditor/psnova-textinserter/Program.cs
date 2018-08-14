using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace psnova_textinserter
{
    class TranslationEntry
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public string OriginalText = "";

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public string Text = "";

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Enabled = false;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Base = null;
    }

    class GlyphEntry
    {
        public string Filename;
        public string Text;
        public List<Tuple<string, uint>> References;
    }

    class Program
    {
        const string charmapDatabaseFilename = "glyphs.json";
        const string translationDatabaseFilename = "translations.json";
        const string translationDLCDatabaseFilename = "translations_dlc.json";

        static Dictionary<string, Dictionary<string, ushort>> charmapReverse;

        static Dictionary<ulong, TranslationEntry> translationDatabase = new Dictionary<ulong, TranslationEntry>();
        static Dictionary<ulong, TranslationEntry> translationDLCDatabase = new Dictionary<ulong, TranslationEntry>();

        static void Main(string[] args)
        {
            if (args.Count() == 0)
            {
                if(File.Exists(translationDatabaseFilename) == false)
                {
                    Console.WriteLine("Couldn't find {0}!", translationDatabaseFilename);
                    Console.ReadLine();
                    return;
                }
                LoadCharacterMapping();
                LoadTranslationDatabase(translationDatabaseFilename);

                foreach (var entry in translationDatabase)
                {
                    if (entry.Value.Enabled)
                    {
                        string filename = String.Format("msg_{0}.rmd", entry.Key);
                        string baseFilename = null;

                        Console.WriteLine("Inserting {0}...", filename);

                        if (!String.IsNullOrWhiteSpace(entry.Value.Base))
                            baseFilename = String.Format("msg_{0}.rmd", entry.Value.Base);


                        var data = TranslateString(String.Format("msg_{0}", entry.Key), entry.Value.Text);
                        InsertTranslation(filename, entry.Key, data, baseFilename);
                    }
                }
            }
            else if(args[0] == "-dlc")
            {
                if (File.Exists(translationDLCDatabaseFilename) == false)
                {
                    Console.WriteLine("Couldn't find {0}!", translationDLCDatabaseFilename);
                    Console.ReadLine();
                    return;
                }
                LoadCharacterMapping();
                LoadTranslationDatabase(translationDLCDatabaseFilename, true);

                foreach (var entry in translationDLCDatabase)
                {
                    if (entry.Value.Enabled)
                    {
                        string filename = String.Format("msg_{0}.rmd", entry.Key);
                        string baseFilename = null;

                        Console.WriteLine("Inserting {0}...", filename);

                        if (!String.IsNullOrWhiteSpace(entry.Value.Base))
                            baseFilename = String.Format("msg_{0}.rmd", entry.Value.Base);


                        var data = TranslateString(String.Format("msg_{0}", entry.Key), entry.Value.Text);
                        InsertTranslation(filename, entry.Key, data, baseFilename, true);
                    }
                }
            }
        }

        static private void InsertTranslation(string filename, ulong id, byte[] text, string baseFilename = null, bool IsDLC = false)
        {
            string sourceDirectory;
            string outputDirectory;

            if (IsDLC == true)
            {
                sourceDirectory = "scripts_dlc";
                outputDirectory = "output_dlc";
            }
            else
            {
                sourceDirectory = "scripts";
                outputDirectory = "output";
            }

            if (!Directory.Exists(sourceDirectory))
            {
                Console.WriteLine("Please create a folder named '{0}' and put original .rmd files inside of it", sourceDirectory);
                return;
            }

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var sourceFilename = Path.Combine(sourceDirectory, filename);
            var outputFilename = Path.Combine(outputDirectory, filename);

            if(baseFilename != null)
                sourceFilename = Path.Combine(sourceDirectory, baseFilename);

            if(!File.Exists(sourceFilename))
            {
                if (IsDLC == true)
                {
                    string DLCName;
                    DirectoryInfo SearchDirectory = new DirectoryInfo(sourceDirectory);
                    FileInfo[] filesInDir = SearchDirectory.GetFiles(filename,SearchOption.AllDirectories);

                    foreach (FileInfo foundFile in filesInDir)
                    {
                        sourceFilename = foundFile.FullName;
                        string[] DLCNameSplit = sourceFilename.Split('\\');
                        DLCName = DLCNameSplit[DLCNameSplit.Length - 2];
                        outputFilename = Path.Combine(@"output_dlc\" + DLCName, filename);
                    }

                    if (!File.Exists(sourceFilename))
                    {
                        Console.WriteLine("Please copy the original {0} into the '{1}' folder", sourceFilename, sourceDirectory);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Please copy the original {0} into the '{1}' folder", sourceFilename, sourceDirectory);
                    return;
                }
            }

            int stringIndexTableOffset;
            int stringTableOffset;
            int fontTableOffset;
            int fontOffset;

            using (BinaryReader reader = new BinaryReader(File.OpenRead(sourceFilename)))
            using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(outputFilename)))
            {
                reader.BaseStream.Seek(0x04, SeekOrigin.Begin);
                fontOffset = reader.ReadInt32();

                reader.BaseStream.Seek(0x30, SeekOrigin.Begin);
                stringIndexTableOffset = reader.ReadInt32();
                stringTableOffset = reader.ReadInt32();
                fontTableOffset = reader.ReadInt32();

                reader.BaseStream.Seek(stringIndexTableOffset, SeekOrigin.Begin);

                int diff = fontTableOffset - stringTableOffset;
                int newFontTableOffset = fontTableOffset - diff;
                int newFontOffset = fontOffset - diff;

                int padding = 0x40 - (text.Length % 0x40);
                int textDataLength = text.Length + padding;

                newFontTableOffset += textDataLength;
                newFontOffset += textDataLength;

                reader.BaseStream.Seek(0x00, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes(4));
                writer.Write(newFontOffset);
                reader.BaseStream.Seek(0x08, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes(4));
                writer.Write(newFontOffset);
                reader.BaseStream.Seek(0x10, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes(0x20));
                writer.Write(reader.ReadBytes(4));
                writer.Write(reader.ReadBytes(4));
                writer.Write(newFontTableOffset);
                writer.Write(newFontOffset - 0x10);
                writer.Write(newFontOffset - 0x10);
                reader.BaseStream.Seek(0x44, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes((int)((stringIndexTableOffset + 0x10) - reader.BaseStream.Position)));
                writer.Write(id);
                reader.BaseStream.Seek(stringIndexTableOffset + 0x10 + 0x08, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes((int)((stringTableOffset + 0x10) - reader.BaseStream.Position)));
                writer.Write(text);

                for(int i = 0; i < padding; i++)
                    writer.Write((byte)0x00);

                reader.BaseStream.Seek(fontTableOffset + 0x10, SeekOrigin.Begin);
                writer.Write(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
            }
        }

        static private byte[] TranslateString(string script, string input)
        {
            List<byte> output = new List<byte>();

            // Convert newline in string to opcode newline
            input = input.Replace("\n", "[n]");

            for(int i = 0; i < input.Length; i++)
            {
                var c = input[i];
                bool foundControlCode = false;

                if (c == '[' && input.Contains(']'))
                {
                    var args = input.Remove(0, i + 1);

                    int end = args.IndexOf(']');
                    if (args.Contains(' '))
                        args = args.Remove(0, args.IndexOf(' ') + 1 < end ? args.IndexOf(' ') + 1 : end);

                    args = args.Substring(0, args.IndexOf(']'));
                    args = args.Replace(" ", "").Trim();

                    // Check for a control code
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[n]")
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8080));
                        foundControlCode = true;
                    }
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[a " && args.Length != 0)
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8081));
                        output.AddRange(GetExtraBytes(args));
                        foundControlCode = true;
                    }
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[b]")
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8082));
                        foundControlCode = true;
                    }
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[c " && args.Length != 0)
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8090));
                        output.AddRange(GetExtraBytes(args));
                        output.Add(0x00);
                        foundControlCode = true;
                    }
                    else if ((i + 2 < input.Length && input.Substring(i, 3) == "[d]") || (input.Length - i >= 6 && input.Substring(i + 1, 5) == "/ruby"))
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8091));
                        foundControlCode = true;
                    }
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[e " && args.Length != 0)
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8094));
                        output.AddRange(GetExtraBytes(args));
                        foundControlCode = true;
                    }
                    if (i + 2 < input.Length && input.Substring(i, 3) == "[f " && args.Length != 0)
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8099));
                        output.AddRange(GetExtraBytes(args));
                        foundControlCode = true;
                    }
                    else if (input.Length - i >= 5 && input.Substring(i + 1, 4) == "ruby")
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8090));

                        foreach(var r in args)
                        {
                            if (charmapReverse.ContainsKey("BasicRubySet") && charmapReverse["BasicRubySet"].ContainsKey(r.ToString()))
                            {
                                output.Add((byte)(charmapReverse["BasicRubySet"][r.ToString()] - 0x881 + 1));
                            }
                        }

                        output.Add(0x00);
                        foundControlCode = true;
                    }

                    if(foundControlCode)
                        i = input.IndexOf(']', i);
                }

                if (!foundControlCode)
                {
                    if(charmapReverse.ContainsKey(script) && charmapReverse[script].ContainsKey(c.ToString()))
                        output.AddRange(BitConverter.GetBytes(charmapReverse[script][c.ToString()]));
                    else if (charmapReverse.ContainsKey("BasicCharSet") && charmapReverse["BasicCharSet"].ContainsKey(c.ToString()))
                        output.AddRange(BitConverter.GetBytes(charmapReverse["BasicCharSet"][c.ToString()]));
                }
            }

            output.Add(0x00);

            return output.ToArray();
        }

        static private byte[] GetExtraBytes(string input)
        {
            List<byte> output = new List<byte>();

            for (int x = 0; x < input.Length; x += 2)
            {
                string b = "";

                if (x + 1 < input.Length)
                    b = input.Substring(x, 2);
                else
                    b = input.Substring(x);

                output.Add(byte.Parse(b, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));
            }

            return output.ToArray();
        }

        static private void LoadTranslationDatabase(string filename, bool IsDLC = false)
        {
            if (File.Exists(filename))
            {
                var data = File.ReadAllText(filename);

                if (String.IsNullOrWhiteSpace(data))
                {
                    if (IsDLC == false)
                    {
                        translationDatabase = new Dictionary<ulong, TranslationEntry>();
                    }
                    else
                    {
                        translationDLCDatabase = new Dictionary<ulong, TranslationEntry>();
                    }
                }
                else
                {
                    if (IsDLC == false)
                    {
                        translationDatabase = JsonConvert.DeserializeObject<Dictionary<ulong, TranslationEntry>>(data);
                    }
                    else
                    {
                        translationDLCDatabase = JsonConvert.DeserializeObject<Dictionary<ulong, TranslationEntry>>(data);
                    }
                }
            }
            else
            {
                if (IsDLC == false)
                {
                    translationDatabase = new Dictionary<ulong, TranslationEntry>();
                }
                else
                {
                    translationDLCDatabase = new Dictionary<ulong, TranslationEntry>();
                }
            }
        }

        static private void LoadCharacterMapping()
        {
            Dictionary<string, GlyphEntry> charmap;

            if (File.Exists(charmapDatabaseFilename))
            { 
                var data = File.ReadAllText(charmapDatabaseFilename);

                if (String.IsNullOrWhiteSpace(data))
                {
                    charmap = new Dictionary<string, GlyphEntry>();
                }
                else
                {
                    charmap = JsonConvert.DeserializeObject<Dictionary<string, GlyphEntry>>(data);
                }
            }
            else
            {
                charmap = new Dictionary<string, GlyphEntry>();
            }

            // Build the reverse lookup table
            charmapReverse = new Dictionary<string, Dictionary<string, ushort>>();
            foreach(var glyph in charmap)
            {
                if(glyph.Value.References == null)
                {
                    Console.WriteLine(glyph.Key);
                    Console.WriteLine("null");
                    Environment.Exit(1);
                }

                foreach (var reference in glyph.Value.References)
                {
                    if (!charmapReverse.ContainsKey(reference.Item1))
                    {
                        charmapReverse[reference.Item1] = new Dictionary<string, ushort>();
                    }

                    charmapReverse[reference.Item1][glyph.Value.Text] = (ushort)reference.Item2;
                }
            }
        }
    }
}
