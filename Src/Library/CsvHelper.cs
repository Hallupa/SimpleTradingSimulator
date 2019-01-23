using System.Collections.Generic;
using System.Linq;

namespace Hallupa.Library
{
    public static class CsvHelper
    {
        public static string[] GetCsvValues(string line, bool trimValues = true)
        {
            var startIndex = 0;
            var inText = false;
            var ret = new List<string>();

            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    inText = !inText;

                    if (!inText)
                    {
                        ret.Add(line.Substring(startIndex, i - startIndex));

                        // Skip next letter which should be ','
                        startIndex = i + 2;
                        i++;
                    }
                    else
                    {
                        startIndex = i + 1;
                    }
                }
                else if ((line[i] == ',' && !inText))
                {
                    ret.Add(line.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
                else if (i == line.Length - 1)
                {
                    ret.Add(line.Substring(startIndex, i - startIndex + 1));
                }
            }

            return ret.Select(x => x.Trim()).ToArray();
        }
    }
}