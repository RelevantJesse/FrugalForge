using System.Text;
using System.Text.RegularExpressions;

namespace WowAhPlanner.WinForms.Services;

internal static class SavedVariablesReader
{
    public static async Task<string> ReadLuaStringValueAsync(string path, string variableName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("SavedVariables path is required.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("SavedVariables file not found.", path);
        }

        var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        var pattern =
            $"(?:\\[\\s*\\\"{Regex.Escape(variableName)}\\\"\\s*\\]|{Regex.Escape(variableName)})\\s*=\\s*\\\"(?<val>(?:\\\\.|[^\\\\\\\"])*?)\\\"";

        var match = Regex.Match(content, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find {variableName} in SavedVariables. Run the addon export and then /reload (or logout/exit) so WoW writes SavedVariables.");
        }

        var escaped = match.Groups["val"].Value;
        return UnescapeLuaString(escaped);
    }

    private static string UnescapeLuaString(string value)
    {
        var sb = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }

            if (i == value.Length - 1)
            {
                sb.Append('\\');
                break;
            }

            var n = value[++i];
            switch (n)
            {
                case '\\':
                    sb.Append('\\');
                    break;
                case '"':
                    sb.Append('"');
                    break;
                case 'n':
                    sb.Append('\n');
                    break;
                case 'r':
                    sb.Append('\r');
                    break;
                case 't':
                    sb.Append('\t');
                    break;
                default:
                    if (n >= '0' && n <= '9')
                    {
                        var num = n - '0';
                        var digits = 1;
                        while (digits < 3 && i + 1 < value.Length)
                        {
                            var d = value[i + 1];
                            if (d < '0' || d > '9') break;
                            num = (num * 10) + (d - '0');
                            i++;
                            digits++;
                        }

                        sb.Append((char)num);
                    }
                    else
                    {
                        sb.Append(n);
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}

