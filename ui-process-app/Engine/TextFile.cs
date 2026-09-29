using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MapUiApp.Engine
{
    /// <summary>
    /// Client text files ship as either UTF-8 or GBK (the PakV4 INI/Lua/string
    /// tables are GBK). Decode by content so both load correctly.
    /// </summary>
    public static class TextFile
    {
        public static IEnumerable<string> ReadLines(string path)
        {
            var text = Decode(File.ReadAllBytes(path));
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        public static string ReadAll(string path) => Decode(File.ReadAllBytes(path));

        private static string Decode(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    return Encoding.GetEncoding(936).GetString(bytes);
                }
                catch
                {
                    return Encoding.UTF8.GetString(bytes);
                }
            }
        }
    }
}
