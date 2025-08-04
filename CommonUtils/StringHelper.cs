using System.Diagnostics;
using System.Text;


namespace CommonUtils
{
    public static class StringHelper
    {
        public static string ReplaceInvalidChars(this string text)
        {
            HashSet<char> invalidChars = [.. Path.GetInvalidFileNameChars()];

            if (!string.IsNullOrEmpty(text))
            {
                text = text.Normalize().Trim();

                StringBuilder result = new(text.Length);

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];

                    if (!invalidChars.Contains(c))
                    {
                        _ = result.Append(c);
                    }
                }

                return result.ToString();
            }

            return string.Empty;
        }


        public static string NormalizeLength(this string textLine, int lenght = 100)
        {
            if (!string.IsNullOrEmpty(textLine) && textLine.Length > lenght)
            {
                int emptyIndex = textLine.LastIndexOf(string.Empty, lenght);

                if (emptyIndex != -1)
                {
                    textLine = $"{textLine[..emptyIndex].Trim()}...";
                }
            }

            return textLine;
        }


        public static void CopyToClipboard(this string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    TextCopy.ClipboardService.SetText(text);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при копировании в буфер обмена: {ex.Message}");
                }
            }
        }



    }
}
