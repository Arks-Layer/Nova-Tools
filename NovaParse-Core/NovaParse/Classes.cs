using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaParse
{
    public class StringEntry
    {
        public string OriginalText { get; set; }
        public string Text { get; set; }
        public bool Enabled { get; set; }
    }
    public class ExportConfig
    {
        public List<ExportEntry> Entries = new List<ExportEntry>();
    }
    public class ExportEntry
    {
        public string Name { get; set; }
        public long MinID { get; set; }
        public long MaxID { get; set; }
    }
}
