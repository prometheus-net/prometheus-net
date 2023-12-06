using System.Text;
using System.Text.RegularExpressions;

namespace Prometheus;

/// <summary>
/// Transforms external names in different character sets into Prometheus (metric or label) names.
/// </summary>
internal static class PrometheusNameHelpers
{
    private static readonly Regex NameRegex = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private const string FirstCharacterCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";
    private const string NonFirstCharacterCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789";

    public static string TranslateNameToPrometheusName(string inputName)
    {
        // Transformations done:
        // * all lowercase
        // * special characters to underscore
        // * must match: [a-zA-Z_][a-zA-Z0-9_]*
        //   * colon is "permitted" by spec but reserved for recording rules

        var sb = new StringBuilder();

        foreach (char inputCharacter in inputName)
        {
            // All lowercase.
            var c = Char.ToLowerInvariant(inputCharacter);

            if (sb.Length == 0)
            {
                // If first character is not from allowed charset, prefix it with underscore to minimize first character data loss.
                if (!FirstCharacterCharset.Contains(c))
                    sb.Append('_');

                sb.Append(c);
            }
            else
            {
                // Standard rules.
                // If character is not permitted, replace with underscore. Simple as that!
                if (!NonFirstCharacterCharset.Contains(c))
                    sb.Append('_');
                else
                    sb.Append(c);
            }
        }

        var name = sb.ToString();

        // Sanity check.
        if (!NameRegex.IsMatch(name))
            throw new Exception("Self-check failed: generated name did not match our own naming rules.");

        return name;
    }
}
