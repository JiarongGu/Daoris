namespace Daoris.Driver;

/// <summary>
/// The one rule for a web page's address in Daoris's browser: what a favorite keeps, what the page's link opener passes on, and what
/// Ask Daoris judges a favorite by. A host with no scheme is a site, over HTTPS unless it is this machine. A file, a script, data, or a
/// credential written into the address goes nowhere: an agent drives the page over CDP without ever passing through it.
/// </summary>
public static class BrowserAddress
{
    /// <summary>The page a typed address names, in the form it is kept in, or null when it is no web page. `about:blank` is none.</summary>
    public static string? Page(string typed)
    {
        var text = typed.Trim();
        if (text.Length == 0 || text.Contains(' ', StringComparison.Ordinal)) return null;
        if (string.Equals(text, "about:blank", StringComparison.OrdinalIgnoreCase)) return null;

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            // No scheme: a web address, if anything. `javascript:`, `data:` and `file:` all carry one.
            if (text.Contains(':', StringComparison.Ordinal) && !LooksLikeHostAndPort(text)) return null;
            var local = text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                        || text.StartsWith("127.0.0.1", StringComparison.Ordinal)
                        || text.StartsWith("[::1]", StringComparison.Ordinal);
            text = (local ? "http://" : "https://") + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
               && string.IsNullOrEmpty(uri.UserInfo)
               && uri.Host.Length > 0
            ? uri.AbsoluteUri
            : null;
    }

    /// <summary>`host:port`, `host:port/path`: a colon that is a port, not a scheme.</summary>
    private static bool LooksLikeHostAndPort(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var rest = text[(colon + 1)..];
        var digits = rest.TakeWhile(char.IsAsciiDigit).Count();
        return digits > 0 && (digits == rest.Length || rest[digits] == '/');
    }
}
