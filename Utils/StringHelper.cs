using System.IO;


namespace RevitBIMTool.Utils;
internal static class StringHelper
{
    public static string ReplaceInvalidChars(string textLine)
    {
        if (!string.IsNullOrEmpty(textLine))
        {
            textLine = string.Join(string.Empty, textLine.Split(Path.GetInvalidFileNameChars()));
            textLine = textLine.Normalize();
            textLine = textLine.Trim();
        }

        return textLine;
    }


    public static string NormalizeText(string textLine, int maxLenght = 100)
    {
        textLine = ReplaceInvalidChars(textLine);

        if (!string.IsNullOrEmpty(textLine) && textLine.Length > maxLenght)
        {
            int strIndex = textLine.LastIndexOf(' ', maxLenght);
            if (strIndex != -1)
            {
                textLine = $"{textLine.Substring(0, maxLenght).Trim()}...";
            }
        }

        return textLine;
    }
}


