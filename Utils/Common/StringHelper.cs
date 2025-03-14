using System.IO;
using System.Text;


namespace RevitBIMTool.Utils.Common;
internal static class StringHelper
{
    public static string ReplaceInvalidChars(string inputString)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();

        StringBuilder stringBuilder = new(inputString.Length);

        if (!string.IsNullOrEmpty(inputString))
        {
            foreach (char c in inputString)
            {
                if (!invalidChars.Contains(c))
                {
                    stringBuilder.Append(c);
                }
            }
        }

        string resultString = stringBuilder.ToString();
        resultString = resultString.Normalize();
        resultString = resultString.Trim('_');

        return resultString;
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


