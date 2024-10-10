using System.IO;
using System.Text;


namespace RevitBIMTool.Utils.Common;
internal static class StringHelper
{
    public static string ReplaceInvalidChars(string textLine)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();

        StringBuilder result = new(textLine.Length);

        if (!string.IsNullOrWhiteSpace(textLine))
        {
            textLine = textLine.TrimEnd('_');
            textLine = textLine.Normalize();

            foreach (char c in textLine)
            {
                if (!invalidChars.Contains(c))
                {
                    _ = result.Append(c);
                }
            }
        }

        return result.ToString();
    }


    public static string NormalizeLength(string textLine, int maxLenght = 100)
    {
        if (!string.IsNullOrEmpty(textLine) && textLine.Length > maxLenght)
        {
            int emptyIndex = textLine.LastIndexOf(string.Empty, maxLenght);

            if (emptyIndex != -1)
            {
                textLine = $"{textLine.Substring(0, emptyIndex).Trim()}...";
            }
        }

        return textLine;
    }
}


