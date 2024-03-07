using System.Text;
using System.Text.RegularExpressions;

namespace docs_gen;

public static partial class JsDocExtensions
{
    public static string FormatAsJsDoc(this string str) =>
        $"""
         /**
          * {str.ReplaceLineEndings("\n * ")}
          */
         """.ReplaceLineEndings("\n");

    public static string AsDescription(this string str)
    {
        str = JsDocLinkRegex().Replace(str, match =>
        {
            var display = match.Groups["display"].Value;
            if (match.Groups["url"].Success)
            {
                var url = match.Groups["url"].Value;
                return $"[{(string.IsNullOrWhiteSpace(display) ? url : display)}]({url})";
            }

            // parse jsdoc symbol link
            var symbol = match.Groups["symbol"].Value;
            var isStaticSymbol = string.IsNullOrWhiteSpace(match.Groups["instance"].Value);

            return $"[{(string.IsNullOrWhiteSpace(display) ? symbol : display)}]({ToUri(symbol, isStaticSymbol)})";
        });

        return str.Trim();
    }

    public static string ToUri(this string symbol, bool isStatic)
    {
        var symbolParts = symbol.ToLower().Split('.');
        var uriBuilder = new StringBuilder("/classes/").Append(symbolParts[0]);

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

    [GeneratedRegex(@"{@link\s+(?:(?<url>https?:\/\/\S+)|(?:(?<instance>@?)(?<symbol>[\w._]+)))(?:(?:\s+|\|)(?<display>.+?))?}", RegexOptions.Compiled)]
    private static partial Regex JsDocLinkRegex();
}