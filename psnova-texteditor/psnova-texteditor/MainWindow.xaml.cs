using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace psnova_texteditor
{
    class TranslationEntry
    {
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
        public List<string> References;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        RmdFile currentRmd = null;
        RmdFile basicCharSetRmd = null;
        RmdFile basicRubySetRmd = null;

        Dictionary<ulong, TranslationEntry> translationDatabase = new Dictionary<ulong, TranslationEntry>();
        Dictionary<string, GlyphEntry> glyphDatabase = new Dictionary<string, GlyphEntry>();
        const string translationDatbaseFilename = "translations.json";
        const string glyphDatabaseFilename = "glyphs.json";

        public MainWindow()
        {
            InitializeComponent();

            translationDatabase = LoadTranslationDatabase(translationDatbaseFilename);
            glyphDatabase = LoadGlyphDatabase(glyphDatabaseFilename);

            var scriptsFolder = "scripts";

            if (!Directory.Exists(scriptsFolder))
            {
                MessageBox.Show(String.Format("Please place all *.rmd files into a folder named \"{0}\" and load the program again", scriptsFolder));
            }
            else
            {
                var scripts = Directory.EnumerateFiles(scriptsFolder, "*.rmd", SearchOption.AllDirectories);

                var basicCharSetPath = System.IO.Path.Combine(scriptsFolder, "BasicCharSet.rmd");
                if (File.Exists(basicCharSetPath))
                    basicCharSetRmd = new RmdFile(basicCharSetPath, 0x81);

                var basicRubySetPath = System.IO.Path.Combine(scriptsFolder, "BasicRubySet.rmd");
                if (File.Exists(basicRubySetPath))
                    basicRubySetRmd = new RmdFile(basicRubySetPath, 0x881);

                foreach (var script in scripts)
                    ScriptList.Items.Add(script);
            }
        }

        private Dictionary<ulong, TranslationEntry> LoadTranslationDatabase(string filename)
        {
            Dictionary<ulong, TranslationEntry> output = new Dictionary<ulong, TranslationEntry>();

            if (File.Exists(filename))
            {
                var data = File.ReadAllText(filename);

                if (!String.IsNullOrWhiteSpace(data))
                {
                    output = JsonConvert.DeserializeObject<Dictionary<ulong, TranslationEntry>>(data);
                }
            }

            if (currentSelectedScript != null)
            {
                bool hasTranslation = translationDatabase.ContainsKey(currentSelectedId);
                if (hasTranslation)
                {
                    skipTextChanged = true;
                    TextEditor.Text = translationDatabase[currentSelectedId].Text;
                }
            }

            return output;
        }

        private Dictionary<string, GlyphEntry> LoadGlyphDatabase(string filename)
        {
            Dictionary<string, GlyphEntry> output = new Dictionary<string, GlyphEntry>();

            if (File.Exists(filename))
            {
                var data = File.ReadAllText(filename);

                if (!String.IsNullOrWhiteSpace(data))
                {
                    output = JsonConvert.DeserializeObject<Dictionary<string, GlyphEntry>>(data);
                }
            }

            return output;
        }

        private void SaveTranslationDatabase(string filename)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(translationDatabase, Formatting.Indented));
        }

        private void SaveGlyphDatabase(string filename)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(glyphDatabase, Formatting.Indented));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveTranslationDatabase(translationDatbaseFilename);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save JSON translation script
            SaveTranslationDatabase(translationDatbaseFilename);
        }

        private void ReloadDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Reload JSON translation script
            LoadTranslationDatabase(translationDatbaseFilename);
        }
        
        private void ToggleInsertion_Click(object sender, RoutedEventArgs e)
        {
            // Toggle flag in JSON translation script
            if (!translationDatabase.ContainsKey(currentSelectedId))
            {
                translationDatabase[currentSelectedId] = new TranslationEntry();
                //translationDatabase[currentSelectedId].Id = currentSelectedId;
                //translationDatabase[currentSelectedId].Filename = currentSelectedScript;
                translationDatabase[currentSelectedId].Text = TextEditor.Text;
            }

            translationDatabase[currentSelectedId].Enabled = !translationDatabase[currentSelectedId].Enabled;
            UpdateToggleInsertionContent();
        }

        private void UpdateToggleInsertionContent()
        {
            if (translationDatabase.ContainsKey(currentSelectedId) && translationDatabase[currentSelectedId].Enabled)
            {
                ToggleInsertion.Content = "Unmark for Insertion";
            }
            else
            {
                ToggleInsertion.Content = "Mark for Insertion";
            }
        }

        ulong currentSelectedId;
        private void StringList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StringList.SelectedItem != null)
            {
                // Change what string to display
                var id = (ulong)StringList.SelectedItem;
                var entry = currentRmd.Strings[id];

                var output = DrawString(entry, DisplayBorder.ActualWidth, currentRmd);

                var outputImage = output.Item1;
                var outputText = output.Item2;

                Preview.Source = BitmapToImageSource(outputImage);
                //Preview.Stretch = Stretch.None;
                Preview.MaxWidth = outputImage.Width;
                Preview.MaxHeight = outputImage.Height;

                currentSelectedId = id;
                bool hasTranslation = translationDatabase.ContainsKey(currentSelectedId);
                if (hasTranslation && !(!translationDatabase[currentSelectedId].Enabled && String.IsNullOrWhiteSpace(translationDatabase[currentSelectedId].Text)))
                {
                    skipTextChanged = true;
                    TextEditor.Text = translationDatabase[currentSelectedId].Text;
                }
                else if (!hasTranslation && outputText != null)
                {
                    TextEditor.Text = outputText;
                }
                else
                {
                    skipTextChanged = true;
                    TextEditor.Text = "";
                }

            }
            else
            {
                Preview.Source = null;
            }

            UpdateToggleInsertionContent();
        }

        bool skipTextChanged = false;
        private void TextEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(skipTextChanged)
            {
                skipTextChanged = false;
                return;
            }

            // Update translation data
            if(!translationDatabase.ContainsKey(currentSelectedId))
            {
                translationDatabase[currentSelectedId] = new TranslationEntry();
            }

            //translationDatabase[currentSelectedId].Id = currentSelectedId;
            //translationDatabase[currentSelectedId].Filename = currentSelectedScript;
            translationDatabase[currentSelectedId].Text = TextEditor.Text;
        }

        private void TextEditor_KeyDown(object sender, RoutedEventArgs e)
        {
            // Update status bar so you can see what character position you are at (for wordwrapping purposes)
            StatusBar2.Text = String.Format("{0}/{1}", TextEditor.SelectionStart, TextEditor.Text.Length);
        }

        string currentSelectedScript;
        private void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Change what script to show strings from
            // This will update the StringList contents with a list of IDs for the selected script
            // Consider asking the user if they want to save the current input data if they've changed the text but didn't insert it

            // Load RMD script
            var path = e.AddedItems[0].ToString();
            currentRmd = new RmdFile(path, 0x391);
            currentSelectedScript = System.IO.Path.GetFileName(path);

            if (currentRmd.Strings.Count > 1)
                StatusBar.Text = String.Format("Loaded {0}... found {1} strings", e.AddedItems[0], currentRmd.Strings.Count);
            else
                StatusBar.Text = String.Format("Loaded {0}... found {1} string", e.AddedItems[0], currentRmd.Strings.Count);
            
            // Remove anything from the string list from the old script
            StringList.Items.Clear();
            foreach (var str in currentRmd.Strings) {
                StringList.Items.Add(str.Key);
            }

            if(StringList.Items.Count > 0)
            {
                StringList.SelectedIndex = 0;
            }

            UpdateToggleInsertionContent();
        }

        private void DumpScripts_Click(object sender, RoutedEventArgs e)
        {
            const string outputFoldername = "output";

            if (!Directory.Exists(outputFoldername))
                Directory.CreateDirectory(outputFoldername);

            // Generate an image for every string in a file, and then generate an image based on all of those images

            var scripts = ScriptList.Items;
            int dumped = 0;
            foreach (string file in scripts)
            {
                var baseFilename = System.IO.Path.GetFileNameWithoutExtension(file);

                if (String.Compare(baseFilename, "BasicCharSet", true) == 0 || String.Compare(baseFilename, "BasicRubySet", true) == 0 || String.Compare(baseFilename, "msg_0", true) == 0)
                    continue;

                string outputFilename = System.IO.Path.Combine(outputFoldername, baseFilename + ".png");
                Console.WriteLine("Saving {0}...", outputFilename);

                RmdFile rmd = new RmdFile(file, 0x391);

                if (rmd.Strings != null)
                {
                    var scriptHeader = DrawTextToBitmap(System.IO.Path.GetFileName(file), 30, System.Drawing.Color.LightBlue);

                    List<Tuple<ulong, Bitmap, Bitmap>> images = new List<Tuple<ulong, Bitmap, Bitmap>>();
                    foreach (var str in rmd.Strings)
                    {
                        var header = DrawTextToBitmap(String.Format("{0}:", str.Key), 20, System.Drawing.Color.Red);
                        var d = DrawString(str.Value, -1, rmd);
                        images.Add(new Tuple<ulong, Bitmap, Bitmap>(str.Key, header, d.Item1));

                        var key = System.IO.Path.GetFileName(file);

                        if(String.Compare(key, String.Format("msg_{0}", str.Key)) == 0)
                        {
                            MessageBox.Show("Found msg that broke expected format of no double keys");
                        }

                        if (!translationDatabase.ContainsKey(str.Key))
                        {
                            translationDatabase[str.Key] = new TranslationEntry();

                            if(d.Item2 != null)
                                translationDatabase[str.Key].Text = d.Item2;
                            else
                                translationDatabase[str.Key].Text = "";
                        }
                    }

                    int xpadding = 10, ypadding = 10;
                    int w = scriptHeader.Width + xpadding, h = scriptHeader.Height + 20 + ypadding;
                    foreach (var image in images)
                    {
                        if (image.Item2.Width > w)
                            w = image.Item2.Width;

                        if (image.Item3.Width > w)
                            w = image.Item3.Width;

                        h += image.Item2.Height + 10;
                        h += image.Item3.Height + 20;
                    }

                    Bitmap output = new Bitmap(w + xpadding * 2, h + ypadding * 2);
                    int x = xpadding, y = ypadding;
                    using (Graphics g = Graphics.FromImage(output))
                    {
                        g.Clear(System.Drawing.Color.Black);

                        g.DrawImage(scriptHeader, x, y);
                        y += scriptHeader.Height + 20;

                        foreach (var image in images)
                        {
                            g.DrawImage(image.Item2, x, y);
                            y += image.Item2.Height + 10;

                            g.DrawImage(image.Item3, x, y);
                            y += image.Item3.Height + 20;
                        }
                    }

                    output.Save(outputFilename);
                    dumped++;
                }
            }

            SaveTranslationDatabase(System.IO.Path.Combine(outputFoldername, translationDatbaseFilename));
            LoadTranslationDatabase(translationDatbaseFilename); // Get rid of all the extra entries we created for dumping

            Console.WriteLine("Finished dumping {0} scripts!", dumped);
            MessageBox.Show(String.Format("Finished dumping {0} scripts!", dumped));
        }

        private void DumpGlyphs_Click(object sender, RoutedEventArgs e)
        {
            // Dump all unique glyphs used in the scripts
            const string outputFoldername = "output_glyphs";

            if (!Directory.Exists(outputFoldername))
                Directory.CreateDirectory(outputFoldername);

            int dumped = 0;
            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
            {
                var scripts = ScriptList.Items;
                foreach (string file in scripts)
                {
                    var baseFilename = System.IO.Path.GetFileNameWithoutExtension(file);

                    if (String.Compare(baseFilename, "BasicCharSet", true) == 0 || String.Compare(baseFilename, "BasicRubySet", true) == 0 || String.Compare(baseFilename, "msg_0", true) == 0)
                        continue;

                    string outputFilename = System.IO.Path.Combine(outputFoldername, baseFilename + ".png");

                    RmdFile rmd = new RmdFile(file, 0x391);
                    foreach (var gm in rmd.FontMapping)
                    {
                        Rectangle glyphMetrics = gm.Value;

                        Bitmap glyph = new Bitmap(glyphMetrics.Width, glyphMetrics.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        using (Graphics g = Graphics.FromImage(glyph))
                        {
                            g.DrawImage(rmd.Font, new RectangleF(0, 0, glyphMetrics.Width, glyphMetrics.Height), glyphMetrics, GraphicsUnit.Pixel);
                        }

                        using (MemoryStream s = new MemoryStream())
                        {
                            glyph.Save(s, System.Drawing.Imaging.ImageFormat.Bmp);

                            var hash = sha1.ComputeHash(s.GetBuffer());

                            var hashStr = BitConverter.ToString(hash).ToLower().Replace("-", ""); // Preferred format

                            if (hashStr != "d919fb1eb06492a666fbec418813f723e97a6e4f") // No glyph
                            {
                                if (!glyphDatabase.ContainsKey(hashStr))
                                {
                                    var baseGlyphFilename = String.Format("{0:d6}.png", dumped);
                                    var outputGlyphFilename = Path.Combine(outputFoldername, baseGlyphFilename);

                                    Console.WriteLine("Saving {0}...", outputGlyphFilename);

                                    if (Directory.Exists(outputGlyphFilename))
                                        Directory.CreateDirectory(outputGlyphFilename);

                                    glyph.Save(outputGlyphFilename);

                                    glyphDatabase[hashStr] = new GlyphEntry();
                                    glyphDatabase[hashStr].Filename = baseGlyphFilename;
                                    glyphDatabase[hashStr].Text = "";
                                    glyphDatabase[hashStr].References = new List<string>();

                                    dumped++;
                                }

                                if (!glyphDatabase[hashStr].References.Contains(baseFilename))
                                    glyphDatabase[hashStr].References.Add(baseFilename);
                            }
                        }
                    }
                }
            }
            
            var outputDatabaseFilename = Path.Combine(outputFoldername, glyphDatabaseFilename);
            SaveGlyphDatabase(outputDatabaseFilename); 

            Console.WriteLine("Finished dumping {0} glyphs!", dumped);
            MessageBox.Show(String.Format("Finished dumping {0} glyphs!", dumped));
        }

        private void DumpText_Click(object sender, RoutedEventArgs e)
        {
            const string outputFoldername = "output_text";

            if (!Directory.Exists(outputFoldername))
                Directory.CreateDirectory(outputFoldername);

            // Generate an image for every string in a file, and then generate an image based on all of those images
            var scripts = ScriptList.Items;
            int dumpedStrings = 0, dumpedScripts = 0;
            foreach (string file in scripts)
            {
                var baseFilename = System.IO.Path.GetFileNameWithoutExtension(file);

                if (String.Compare(baseFilename, "BasicCharSet", true) == 0 || String.Compare(baseFilename, "BasicRubySet", true) == 0 || String.Compare(baseFilename, "msg_0", true) == 0)
                    continue;

                string outputFilename = System.IO.Path.Combine(outputFoldername, baseFilename + ".png");
                Console.WriteLine("Saving {0}...", outputFilename);

                RmdFile rmd = new RmdFile(file, 0x391);

                if (rmd.Strings != null)
                {
                    foreach (var str in rmd.Strings)
                    {
                        var d = DrawString(str.Value, -1, rmd);
                        var key = System.IO.Path.GetFileName(file);

                        if (String.Compare(key, String.Format("msg_{0}", str.Key)) == 0)
                        {
                            MessageBox.Show("Found msg that broke expected format of no double keys");
                        }

                        if (!translationDatabase.ContainsKey(str.Key))
                        {
                            translationDatabase[str.Key] = new TranslationEntry();

                            if (d.Item2 != null)
                            {
                                translationDatabase[str.Key].Text = d.Item2;
                                dumpedStrings++;
                            }
                            else
                            {
                                translationDatabase[str.Key].Text = "";
                            }
                        }
                    }
                }

                dumpedScripts++;
            }

            SaveTranslationDatabase(System.IO.Path.Combine(outputFoldername, translationDatbaseFilename));
            LoadTranslationDatabase(translationDatbaseFilename); // Get rid of all the extra entries we created for dumping

            Console.WriteLine("Finished dumping {0} strings from {1} scripts!", dumpedStrings, dumpedScripts);
            MessageBox.Show(String.Format("Finished dumping {0} strings from {1} scripts!", dumpedStrings, dumpedScripts));
        }

        private BitmapImage BitmapToImageSource(Bitmap input)
        {
            var bitmapSource = new MemoryStream();
            input.Save(bitmapSource, System.Drawing.Imaging.ImageFormat.Png);

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = bitmapSource;
            image.EndInit();
            return image;
        }

        private Tuple<Bitmap, string> DrawString(byte[][] text, double frameWidth, RmdFile currentRmd)
        {
            int glyphWidth = currentRmd.GlyphWidth;
            int glyphHeight = currentRmd.GlyphHeight;
            int requiredWidth = 0;
            int x = 0, y = 0;

            string outputText = "";

            // Calculate required image width and height
            int lines = 0;
            var tiles = new List<Tuple<Bitmap, RectangleF>>();
            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                var cmd = BitConverter.ToUInt16(c, 0);
                if (cmd >= 0x8080)
                {
                    string opcodeText = null;

                    // Control codes
                    if (cmd == 0x8080)
                    {
                        opcodeText = "n";
                    }
                    else if (cmd == 0x8081)
                    {
                        opcodeText = "a";
                    }
                    else if (cmd == 0x8082)
                    {
                        opcodeText = "b";
                    }
                    else if (cmd == 0x8090)
                    {
                        opcodeText = "c";
                    }
                    else if (cmd == 0x8091)
                    {
                        opcodeText = "d";
                    }
                    else if (cmd == 0x8094)
                    {
                        opcodeText = "e";
                    }
                    else if (cmd == 0x8099)
                    {
                        opcodeText = "f";
                    }

                    opcodeText = "[" + opcodeText + " ";
                    for (int j = 2; j < c.Length; j++)
                        opcodeText += String.Format("{0:x2} ", c[j]);
                    opcodeText = opcodeText.Trim() + "]";

                    if (opcodeText != null)
                    {
                        // Write text to be displayed
                        var tile = DrawTextToBitmap(opcodeText, glyphWidth * 0.75f, System.Drawing.Color.Yellow);

                        if (frameWidth != -1 && x + tile.Width >= frameWidth)
                        {
                            if (x > requiredWidth)
                                requiredWidth = x;

                            y += glyphHeight;
                            x = 0;
                        }

                        tiles.Add(new Tuple<Bitmap, RectangleF>(tile, new RectangleF(x, y, tile.Width, tile.Height)));

                        x += tile.Width;

                        outputText += opcodeText;
                    }

                    if (cmd == 0x8080)
                    {
                        if (x > requiredWidth)
                            requiredWidth = x;

                        // New line
                        lines++;
                        x = 0;
                        y += glyphHeight;
                    }
                }
                else
                {
                    RmdFile targetFont = null;

                    if (basicCharSetRmd != null && basicCharSetRmd.FontMapping.ContainsKey(cmd))
                    {
                        targetFont = basicCharSetRmd;
                    }
                    else if (basicRubySetRmd != null && basicRubySetRmd.FontMapping.ContainsKey(cmd))
                    {
                        targetFont = basicRubySetRmd;
                    }
                    else if (currentRmd != null && currentRmd.FontMapping.ContainsKey(cmd))
                    {
                        targetFont = currentRmd;
                    }

                    if (targetFont != null)
                    {
                        if (frameWidth != -1 && x + glyphWidth >= frameWidth)
                        {
                            y += glyphHeight;
                            x = 0;
                        }

                        var glyphMetrics = targetFont.FontMapping[cmd];
                        Bitmap tile = new Bitmap(glyphMetrics.Width, glyphMetrics.Height);
                        using (Graphics g = Graphics.FromImage(tile))
                            g.DrawImage(targetFont.Font, new RectangleF(0, 0, glyphMetrics.Width, glyphMetrics.Height), glyphMetrics, GraphicsUnit.Pixel);
                        tiles.Add(new Tuple<Bitmap, RectangleF>(tile, new RectangleF(x, y, glyphMetrics.Width, glyphMetrics.Height)));


                        using (MemoryStream s = new MemoryStream())
                        {
                            tile.Save(s, System.Drawing.Imaging.ImageFormat.Bmp);

                            using (SHA1CryptoServiceProvider sha1 = new SHA1CryptoServiceProvider())
                            {
                                var hash = sha1.ComputeHash(s.GetBuffer());
                                var hashStr = BitConverter.ToString(hash).ToLower().Replace("-", ""); // Preferred format

                                if (hashStr == "d919fb1eb06492a666fbec418813f723e97a6e4f") // No image
                                {
                                    outputText += " ";
                                }
                                else if (glyphDatabase.ContainsKey(hashStr))
                                {
                                    outputText += glyphDatabase[hashStr].Text;
                                }
                            }
                        }

                        var w = targetFont.GlyphSizes[cmd].Item1;
                        var h = targetFont.GlyphSizes[cmd].Item2;
                        x += w;
                    }
                    else
                    {
                        Console.WriteLine("Could not find {0:x2} in font mapping", cmd);
                    }
                }
            }
            
            if (x > requiredWidth)
                requiredWidth = x;

            if (requiredWidth == 0)
                requiredWidth = x;

            var output = new Bitmap(requiredWidth + 1, (lines + 1) * glyphHeight);

            using (Graphics g = Graphics.FromImage(output))
            {
                foreach(var tile in tiles)
                {
                    g.DrawImage(tile.Item1, tile.Item2, new RectangleF(0, 0, tile.Item1.Width, tile.Item1.Height), GraphicsUnit.Pixel);
                }
            }

            outputText = outputText.Replace("[n]", "\n");
            if(String.IsNullOrWhiteSpace(outputText))
                outputText = null;

            return new Tuple<Bitmap, string>(output, outputText);
        }

        private static Bitmap DrawTextToBitmap(string text, float fontsize, System.Drawing.Color color)
        {
            var drawFont = new Font("Arial", fontsize, GraphicsUnit.Pixel);
            var drawBrush = new SolidBrush(color);
            var drawBackground = System.Drawing.Color.Transparent;

            var tile = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(tile);
            SizeF textSize = drawing.MeasureString(text, drawFont);
            tile.Dispose();
            drawing.Dispose();

            tile = new Bitmap((int)textSize.Width, (int)textSize.Height);
            drawing = Graphics.FromImage(tile);
            drawing.Clear(drawBackground);
            drawing.DrawString(text, drawFont, drawBrush, 0, 0);
            drawing.Save();

            drawBrush.Dispose();
            drawing.Dispose();

            return tile;
        }
    }
}
