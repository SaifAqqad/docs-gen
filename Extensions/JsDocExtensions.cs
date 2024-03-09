using System.Text.RegularExpressions;

namespace docs_gen.Extensions;

public static partial class JsDocExtensions
{
    public static string FormatAsJsDoc(this string str)
    {
        return $"""
                /**
                 * {str.ReplaceLineEndings("\n * ")}
                 */
                """.NormalizeLineEndings();
    }

    public static string ParseDescription(this string str)
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

            return $"[{(string.IsNullOrWhiteSpace(display) ? symbol : display)}]({symbol.ToDocUri(isStaticSymbol)})";
        });

        return str.Trim('-', '_', ' ', '\n', '\t');
    }

    public static (string? Uri, string? DisplayName) ParseLink(this string str)
    {
        var match = JsDocLinkRegex().Match(str);
        if (!match.Success)
        {
            return (null, null);
        }

        var display = string.IsNullOrWhiteSpace(match.Groups["display"].Value) ? null : match.Groups["display"].Value;
        if (match.Groups["url"].Success)
        {
            return (match.Groups["url"].Value, display);
        }

        var symbol = match.Groups["symbol"].Value;
        var isStaticSymbol = string.IsNullOrWhiteSpace(match.Groups["instance"].Value);
        return (symbol.ToDocUri(isStaticSymbol), display);
    }

    [GeneratedRegex(@"{@link\s+(?:(?<url>https?:\/\/\S+)|(?:(?<instance>@?)(?<symbol>[\w._]+)))(?:(?:\s+|\|)(?<display>.+?))?}", RegexOptions.Compiled)]
    private static partial Regex JsDocLinkRegex();
}