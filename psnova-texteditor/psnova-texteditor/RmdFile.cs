using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace psnova_texteditor
{
    class RmdFile
    {
        public Bitmap Font;
        public Dictionary<uint, Rectangle> FontMapping;
        public Dictionary<uint, Tuple<int, int>> GlyphSizes;
        public Dictionary<ulong, byte[][]> Strings;
        public int GlyphWidth, GlyphHeight;

        public RmdFile(string filename, int charsetBaseRangeStart)
        {
            using(BinaryReader reader = new BinaryReader(File.OpenRead(filename)))
            {
                Console.WriteLine("Loading {0}...", filename);

                if(Encoding.ASCII.GetString(reader.ReadBytes(4)) != " DMR")
                {
                    Console.WriteLine("Not a valid RMD file");
                    return;
                }

                reader.BaseStream.Seek(0x0c, SeekOrigin.Begin);
                var fontFileOffset = reader.ReadInt32();

                reader.BaseStream.Seek(0x30, SeekOrigin.Begin);
                var stringEntryTableOffset = reader.ReadInt32() + 0x10;
                var stringTableOffset = reader.ReadInt32() + 0x10;
                var fontTableOffset = reader.ReadInt32() + 0x10;

                reader.BaseStream.Seek(0x44, SeekOrigin.Begin);
                var charsetSize = reader.ReadInt32();

                reader.BaseStream.Seek(0x54, SeekOrigin.Begin);
                GlyphWidth = reader.ReadInt32();
                GlyphHeight = reader.ReadInt32();

                reader.BaseStream.Seek(0x64, SeekOrigin.Begin);
                var stringEntries = reader.ReadInt32();
                var fontFileSize = reader.ReadInt32();

                reader.BaseStream.Seek(0x70, SeekOrigin.Begin);
                var fontImageWidth = reader.ReadInt32();
                var fontImageHeight = reader.ReadInt32();


                // Read string entries
                Strings = new Dictionary<ulong, byte[][]>();
                reader.BaseStream.Seek(stringEntryTableOffset, SeekOrigin.Begin);
                for(int i = 0; i < stringEntries; i++)
                {
                    var id = reader.ReadUInt64();
                    var offset = reader.ReadInt32();

                    reader.BaseStream.Seek(0x0c, SeekOrigin.Current);

                    // Read string
                    var currentOffset = reader.BaseStream.Position;

                    reader.BaseStream.Seek(stringTableOffset + offset, SeekOrigin.Begin);

                    List<byte[]> stringData = new List<byte[]>();
                    while(true)
                    {
                        List<byte> curCommand = new List<byte>();

                        var c = reader.ReadByte();
                        if (c == 0)
                            break;
                        var c2 = reader.ReadByte();

                        curCommand.Add(c);
                        curCommand.Add(c2);

                        var cmd = (int)(c2 << 8) | c;

                        if (cmd >= 0x8080)
                        {
                            if (cmd == 0x8080)
                            {
                                // Nothing
                            }
                            else if (cmd == 0x8081)
                            {
                                curCommand.Add(reader.ReadByte());
                            }
                            else if (cmd == 0x8082)
                            {
                                // Nothing
                            }
                            else if (cmd == 0x8090)
                            {
                                byte d;
                                while ((d = reader.ReadByte()) != 0)
                                    curCommand.Add(d);
                            }
                            else if (cmd == 0x8091)
                            {
                                // Nothing
                            }
                            else if (cmd == 0x8094)
                            {
                                curCommand.Add(reader.ReadByte());
                                curCommand.Add(reader.ReadByte());
                            }
                            else if (cmd == 0x8099)
                            {
                                curCommand.Add(reader.ReadByte());
                            }
                            else if(cmd != 0x8080)
                            {
                                Console.WriteLine("CHECK UNKNOWN OPCODE: {0:x4} @ {1:x8} in {2}", c, reader.BaseStream.Position - 2, filename);
                            }
                        }
                        stringData.Add(curCommand.ToArray());
                    }

                    Strings.Add(id, stringData.ToArray());

                    reader.BaseStream.Seek(currentOffset, SeekOrigin.Begin);
                }



                // Read font image
                // ASSUMPTION: The font AIF will always have only one image, and the information is all located in the same spot.
                // This assumption is made so we don't have to write a full AIF parser for this tool.

                // Read image format
                reader.BaseStream.Seek(fontFileOffset + 0xa0, SeekOrigin.Begin);
                var format = reader.ReadByte();

                // Read width and height
                reader.BaseStream.Seek(fontFileOffset + 0xa8, SeekOrigin.Begin);
                var width = reader.ReadUInt16();
                var height = reader.ReadUInt16();

                // Read font data size
                reader.BaseStream.Seek(fontFileOffset + 0x1a0, SeekOrigin.Begin);
                var fontDataSize = reader.ReadInt32();

                reader.BaseStream.Seek(fontFileOffset + 0x1e0, SeekOrigin.Begin);
                var fontData = reader.ReadBytes(fontDataSize);

                GXTConvert.FileFormat.SceGxmTextureFormat imageFormat = GXTConvert.FileFormat.SceGxmTextureFormat.U8U8U8U8_RGBA;
                GXTConvert.FileFormat.SceGxmTextureType imageType = GXTConvert.FileFormat.SceGxmTextureType.Swizzled;
                switch(format)
                {
                    case 0x08:
                        imageFormat = GXTConvert.FileFormat.SceGxmTextureFormat.U8U8U8U8_RGBA; // RGBA
                        imageType = GXTConvert.FileFormat.SceGxmTextureType.Linear;
                        break;
                    case 0x10:
                        imageFormat = GXTConvert.FileFormat.SceGxmTextureFormat.UBC1_ABGR; // DXT1
                        break;
                    case 0x12:
                        imageFormat = GXTConvert.FileFormat.SceGxmTextureFormat.UBC2_ABGR; // DXT3
                        break;
                    case 0x14:
                        imageFormat = GXTConvert.FileFormat.SceGxmTextureFormat.UBC3_ABGR; // DXT5
                        break;
                    default:
                        Console.WriteLine("Unknown image format: {0:x8}", format);
                        break;
                }
                
                var info = new GXTConvert.FileFormat.SceGxtTextureInfoRaw((uint)imageType, (uint)imageFormat, width, height, 0, (uint)fontData.Length);
                var texture = new GXTConvert.Conversion.TextureBundle(new BinaryReader(new MemoryStream(fontData)), info);

                Font = texture.CreateTexture();

                // Generate font mapping from character -> offset in font image
                FontMapping = new Dictionary<uint, Rectangle>();
                GlyphSizes = new Dictionary<uint, Tuple<int, int>>();

                reader.BaseStream.Seek(fontTableOffset, SeekOrigin.Begin);
                for (int i = charsetBaseRangeStart, charsetOffset = 0, x = 0, y = 0; i < charsetBaseRangeStart + charsetSize; i++)
                {
                    if (((i + 1) % 0x80) == 0)
                        charsetOffset += 0x80;

                    var id = (ushort)(i + charsetOffset);
                    FontMapping[id] = new Rectangle(x, y, GlyphWidth, GlyphHeight);
                    x += GlyphWidth;

                    if(x + GlyphWidth >= Font.Width)
                    {
                        y += GlyphHeight;
                        x = 0;
                    }

                    if (y >= Font.Height)
                        break;

                    var w = reader.ReadByte();
                    var h = reader.ReadByte();
                    reader.BaseStream.Seek(0x12, SeekOrigin.Current);
                    GlyphSizes[id] = new Tuple<int, int>(w, h);
                }
            }
        }
    }
}
