using System.Text;

namespace docs_gen;

public static class DocStringExtensions
{
    public static string BaseUri { get; set; } = ".";

    public static bool IsUpperCase(this string str)
    {
        return str.All(c => char.IsUpper(c) || char.IsWhiteSpace(c) || char.IsSymbol(c));
    }

    public static string Indent(this string str, int indentLevel)
    {
        if (indentLevel == 0 || string.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        return string.Join('\n', str.Split('\n').Select(line => $"{new string(' ', indentLevel * 2)}{line}"));
    }

    public static string NormalizeLineEndings(this string str)
    {
        return str.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static string ToDocUri(this string symbol, bool isStatic)
    {
        var symbolParts = symbol.ToLower().Split('.');
        var uriBuilder = new StringBuilder($"{BaseUri}/{symbolParts[0]}");

        if (symbolParts.Length > 1)
        {
            uriBuilder.Append("?id=");
            if (isStatic)
            {
                uriBuilder.Append("static-");
            }

            uriBuilder.Append(symbolParts[1]);
        }

        return uriBuilder.ToString();
    }
}