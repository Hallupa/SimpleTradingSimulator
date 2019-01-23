using log4net;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace Hallupa.Library
{
    public static class ZipHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Extract(string zipPath, string extactDirectory, string checkFilePath, long checkFileLength)
        {
            var archive = ZipFile.OpenRead(zipPath);

            if (File.Exists(checkFilePath))
            {
                var existingChromeInfo = new FileInfo(checkFilePath);
                var newChromInfo = archive.Entries.First(n => n.FullName == Path.GetFileName(checkFilePath));

                if (newChromInfo.Length == existingChromeInfo.Length)
                {
                    return;
                }
            }

            Log.Debug("Extracting files");

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var fullName = entry.FullName.Replace("/", "\\");
                if (fullName.EndsWith("\\"))
                {
                    Directory.CreateDirectory(Path.Combine(extactDirectory, fullName));
                }
                else
                {
                    entry.ExtractToFile(Path.Combine(extactDirectory, fullName), true);
                }
            }

            Log.Debug("Extracted files");
        }
    }
}