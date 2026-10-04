using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ClassicUO.Utility;

public static class RegexHelper
{
    private static ConcurrentDictionary<(string Pattern, RegexOptions Options), Regex> _regexes = new();

    public static Regex GetRegex(string pattern, RegexOptions options = RegexOptions.Compiled)
    {
        if((options & RegexOptions.Compiled) == 0)
            options |= RegexOptions.Compiled;
        
        return _regexes.GetOrAdd((pattern, options), key => new Regex(key.Pattern, key.Options));
    }
}
