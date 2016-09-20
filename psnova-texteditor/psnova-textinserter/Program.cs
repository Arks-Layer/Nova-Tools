using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace psnova_textinserter
{
    class CharacterEntry
    {
        public ushort Id;
        public char Text;
    }

    class TranslationEntry
    {
        public string Text = "";

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public bool Enabled = false;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Base = null;
    }

    class Program
    {
        const string charmapDatbaseFilename = "charmap.json";
        const string translationDatabaseFilename = "translations.json";

        static Dictionary<ushort, CharacterEntry> charmap;
        static Dictionary<char, CharacterEntry> charmapReverse;

        static Dictionary<ulong, TranslationEntry> translationDatabase = new Dictionary<ulong, TranslationEntry>();

        static void Main(string[] args)
        {
            LoadCharacterMapping();

            if (args.Length != 0)
            {
                Console.WriteLine("Input string: {0}", args[0]);
                Console.Write("Output hex: ");

                var c = TranslateString(args[0]);
                for(int i = 0; i < c.Length - 1; i++)
                {
                    Console.Write("{0:x2} ", c[i]);
                }

                Console.WriteLine();

                Environment.Exit(1);
            }

            LoadTranslationDatabase(translationDatabaseFilename);

            foreach (var entry in translationDatabase)
            {
                if (entry.Value.Enabled)
                {
                    string filename = String.Format("msg_{0}.rmd", entry.Key);
                    string baseFilename = null;

                    Console.WriteLine("Inserting {0}...", filename);

                    if(!String.IsNullOrWhiteSpace(entry.Value.Base))
                        baseFilename = String.Format("msg_{0}.rmd", entry.Value.Base);


                    var data = TranslateString(entry.Value.Text);
                    InsertTranslation(filename, entry.Key, data, baseFilename);
                }
            }
        }

        static private void InsertTranslation(string filename, ulong id, byte[] text, string baseFilename = null)
        {
            const string sourceDirectory = "scripts";
            const string outputDirectory = "output";

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
                Console.WriteLine("Please copy the original {0} into the '{1}' folder", sourceFilename, sourceDirectory);
                return;
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

        static private byte[] TranslateString(string input)
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
                    var args = input.Remove(0, i + 2);
                    args = args.Substring(0, args.IndexOf(']'));
                    args = args.Replace(" ", "").Trim();

                    // Check for a control code
                    if (input[i + 1] == 'n')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8080));
                    }
                    else if (input[i + 1] == 'a')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8081));
                        output.AddRange(GetExtraBytes(args));
                    }
                    else if (input[i + 1] == 'b')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8082));
                    }
                    else if (input[i + 1] == 'c')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8090));
                        output.AddRange(GetExtraBytes(args));
                        output.Add(0x00);
                    }
                    else if (input[i + 1] == 'd')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8091));
                    }
                    else if (input[i + 1] == 'e')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8094));
                        output.AddRange(GetExtraBytes(args));
                    }
                    else if (input[i + 1] == 'f')
                    {
                        output.AddRange(BitConverter.GetBytes((ushort)0x8099));
                        output.AddRange(GetExtraBytes(args));
                    }
                    
                    i = input.IndexOf(']', i);
                    foundControlCode = true;
                }

                if (!foundControlCode && charmapReverse.ContainsKey(c))
                {
                    output.AddRange(BitConverter.GetBytes(charmapReverse[c].Id));
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

        static private void LoadTranslationDatabase(string filename)
        {
            if (File.Exists(filename))
            {
                var data = File.ReadAllText(filename);

                if (String.IsNullOrWhiteSpace(data))
                {
                    translationDatabase = new Dictionary<ulong, TranslationEntry>();
                }
                else
                {
                    translationDatabase = JsonConvert.DeserializeObject<Dictionary<ulong, TranslationEntry>>(data);
                }
            }
            else
            {
                translationDatabase = new Dictionary<ulong, TranslationEntry>();
            }
        }

        static private void LoadCharacterMapping()
        {
            if (!File.Exists(charmapDatbaseFilename))
                GenerateCharMap();

            if (File.Exists(charmapDatbaseFilename))
            { 
                var data = File.ReadAllText(charmapDatbaseFilename);

                if (String.IsNullOrWhiteSpace(data))
                {
                    charmap = new Dictionary<ushort, CharacterEntry>();
                }
                else
                {
                    charmap = JsonConvert.DeserializeObject<Dictionary<ushort, CharacterEntry>>(data);
                }
            }
            else
            {
                charmap = new Dictionary<ushort, CharacterEntry>();
            }

            // Build the reverse lookup table
            charmapReverse = new Dictionary<char, CharacterEntry>();
            foreach(var c in charmap)
            {
                charmapReverse[c.Value.Text] = c.Value;
            }
        }

        static private void SaveCharacterMapping()
        {
            File.WriteAllText(charmapDatbaseFilename, JsonConvert.SerializeObject(charmap, Formatting.Indented));
        }

        static private void GenerateCharMap()
        {
            charmap = new Dictionary<ushort, CharacterEntry>();

            int baseCharset = 0x81;
            string basicCharSet =
                "0123456789-.'ABCDEFG" +
                "HIJKLMNOPQRSTUVWXYZa" +
                "bcdefghijklmnopqrstu" +
                "vwxyzをぁぃぅぇぉゃゅょっゎあいうえ" +
                "おかきくけこさしすせそたちつてとなにぬね" +
                "のはひふへほまみむめもやゆよらりるれろわ" +
                "んがぎぐげござじずぜぞだぢづでどばびぶべ" +
                "ぼぱぴぷぺぽヲァィゥェォャュョッヮーアイ" +
                "ウエオカキクケコサシスセソタチツテトナニ" +
                "ヌネノハヒフヘホマミムメモヤユヨラリルレ" +
                "ロワンヴガギグゲゴザジズゼゾダヂヅデドバ" +
                "ビブベボパピプペポヵヶ 　、、。。・?？" +
                "!！)）>＞]］}｝」』$%&,:;=|" +
                "(（<＜[［{｛「『#＃@\"”*+＋-." +
                "❜％＆：；＿＝＊^／｜❜￥＼￥￥/~^_" +
                "'`～…♪×＄，０１２３４５６７８９ＡＢ" +
                "ＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶ" +
                "ＷＸＹＺａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐ" +
                "ｑｒｓｔｕｖｗｘｙｚ☆★＠";

            int charsetOffset = 0;
            for (int i = 0; i < basicCharSet.Length; i++)
            {
                if (((i + baseCharset + 1) % 0x80) == 0)
                    charsetOffset += 0x80;

                CharacterEntry entry = new CharacterEntry();
                entry.Id = (ushort)(i + charsetOffset + baseCharset);
                entry.Text = basicCharSet[i];
                charmap[entry.Id] = entry;
            }

            /*
            baseCharset = 0x881;
            string basicRubySet = 
                "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽぁぃぅぇぉゃゅょっゎ・ー" +
                "ヴアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワンヴガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポァィゥェォャュョッヮヵヶ●1234567890-N";

            charsetOffset = 0;
            for (int i = 0; i < basicRubySet.Length; i++)
            {
                if (((i + baseCharset + 1) % 0x80) == 0)
                    charsetOffset += 0x80;

                CharacterEntry entry = new CharacterEntry();
                entry.Id = i + charsetOffset + baseCharset;
                entry.Text = basicRubySet[i];
                charmap[entry.Id] = entry;
            }
            */

            SaveCharacterMapping();
        }
    }
}
