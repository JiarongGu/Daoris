using System.Text.Json;
using Daoris.Driver;
using Shenora.Core.Shell;

namespace Daoris.Desktop;

/// <summary>
/// The viewer's colour scheme, kept by the shell across starts (THEMESTART1, D22's THEMESCHEME1 note), so the window's
/// first frame and the engine wear the choice before the page has said it.
/// </summary>
/// <remarks>
/// <para><b>The kit keeps the scheme in memory only</b>: <c>UseWindows</c> registers <see cref="IColorScheme"/> at what
/// its host options say, and asks the app to pass the saved choice there and to save each change on
/// <see cref="IColorScheme.Changed"/>. The page is still the choice's owner (its own store, D66): it sets the scheme on
/// arrival and on every change (THEMESCHEME1), and this keeps what it set, so a copy that went stale is put right a
/// round trip into the next start.</para>
///
/// <para>Beside the window's geometry, in the kit's data area: both are the window's own memory, and the dev loop's
/// <c>--app-root</c> gives a scratch window its own of each.</para>
/// </remarks>
public static class KeptScheme
{
    /// <summary>The file's name in the kit's <c>config</c> data area.</summary>
    public const string FileName = "color-scheme.json";

    /// <summary>
    /// The choice kept at <paramref name="path"/>, or <see cref="ColorScheme.System"/> where none was kept or it cannot
    /// be read: a start that follows the OS, as every start did before, never a window that does not open.
    /// </summary>
    public static ColorScheme Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return ColorScheme.System;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("scheme", out var scheme)
                || scheme.ValueKind != JsonValueKind.String) return ColorScheme.System;
            return scheme.GetString() switch
            {
                "light" => ColorScheme.Light,
                "dark" => ColorScheme.Dark,
                _ => ColorScheme.System,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return ColorScheme.System;
        }
    }

    /// <summary>
    /// Keep each change of <paramref name="scheme"/> at <paramref name="path"/>, until the answer is disposed. A write
    /// that fails is a line in <paramref name="log"/>, never a failure of the page's request that set it.
    /// </summary>
    public static IDisposable Keep(IColorScheme scheme, string path, MachineLog log)
    {
        var gate = new object();
        void Changed(ColorScheme value)
        {
            // The latest, not the value raised: two threads setting it at once each raise their own, in either order.
            lock (gate)
            {
                try
                {
                    AtomicFile.WriteText(path, $"{{\"scheme\":\"{Word(scheme.Scheme)}\"}}\n");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    log.Failed("keeping the colour scheme", error);
                }
            }
        }

        scheme.Changed += Changed;
        return new Unsubscribe(() => scheme.Changed -= Changed);
    }

    /// <summary>
    /// Whether the first frame is dark: a held choice whatever the OS says, and at <see cref="ColorScheme.System"/> what
    /// <paramref name="operatingSystemPrefersDark"/> answers, which is asked only then.
    /// </summary>
    public static bool StartsDark(ColorScheme scheme, Func<bool> operatingSystemPrefersDark) => scheme switch
    {
        ColorScheme.Light => false,
        ColorScheme.Dark => true,
        _ => operatingSystemPrefersDark(),
    };

    /// <summary>The page's own word for each choice (<c>ThemeChoice</c> in <c>design/theme.ts</c>).</summary>
    private static string Word(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Light => "light",
        ColorScheme.Dark => "dark",
        _ => "system",
    };

    private sealed class Unsubscribe(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose() => Interlocked.Exchange(ref _undo, null)?.Invoke();
    }
}
