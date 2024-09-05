using System;

namespace Emby.Jimaku.Model
{
    public class JimakuFile
    {
        public string Url { get; set; } // The file's download URL
        public string Name { get; set; } // The file's name
        public long Size { get; set; } // The file's size in bytes
        public DateTime LastModified { get; set; } // The date the file was last modified as an RFC3339 string
    }
}
