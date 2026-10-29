using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>A frame of a failure's stack: the method with its compiler's wrapping removed, the file as the stack gave it, the line.</summary>
internal sealed record FailureFrame(string Method, string? File, int? Line)
{
    /// <summary>The frame as the tool's reader writes it.</summary>
    public JsonObject ToJson() => new() { ["method"] = Method, ["file"] = File, ["line"] = Line };
}

/// <summary>One failure a report holds: its runner, its unit, its test with its case, the exception type, the message's first line and the frames.</summary>
internal sealed record ReadFailure(string Runner, string? Unit, string Test, string Exception, string Message, IReadOnlyList<FailureFrame> Frames)
{
    /// <summary>The failure as the tool's reader writes it.</summary>
    public JsonObject ToJson() => new()
    {
        ["runner"] = Runner, ["unit"] = Unit, ["test"] = Test, ["exception"] = Exception, ["message"] = Message,
        ["frames"] = new JsonArray([.. Frames.Select(frame => (JsonNode?)frame.ToJson())]),
    };
}

/// <summary>Where a failure was thrown in its test's own file: the frame's method, the text of its source line (normalised) and its place among identical lines of that method.</summary>
internal sealed record FailureLocation(string Method, string Text, int Ordinal);

/// <summary>
/// What a failure is, across runs on different trees (D207; the integration design §5.3): a test by its full name, exception type and
/// location (or, where none can be read, its message line), a gate that names no test by its step, a build by the set of its error codes
/// with their files. Two failures are one when their <see cref="Key"/> is one string.
/// </summary>
internal sealed record FailureSignature
{
    public string? Test { get; init; }

    public string? Exception { get; init; }

    public FailureLocation? Location { get; init; }

    public string? Message { get; init; }

    /// <summary>The gate, where this signs a gate that names no test; null for a test's.</summary>
    public string? Gate { get; init; }

    public string? Step { get; init; }

    /// <summary>A build's errors, <c>CODE file</c> each, sorted.</summary>
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>The signature as one string, its fields in a fixed order: the tool's <c>signatureKey</c>, character for character.</summary>
    public string Key => FailureReader.KeyOf(this);

    /// <summary>The signature as the tool's reader writes it, fields it does not hold left out.</summary>
    public JsonObject ToJson()
    {
        var json = new JsonObject();
        if (Gate is not null)
        {
            json["gate"] = Gate;
            json["step"] = Step;
            if (Errors is not null) json["errors"] = new JsonArray([.. Errors.Select(error => (JsonNode?)JsonValue.Create(error))]);
            else json["message"] = Message;
            return json;
        }

        json["test"] = Test;
        json["exception"] = Exception;
        if (Location is { } at) json["location"] = new JsonObject { ["method"] = at.Method, ["text"] = at.Text, ["ordinal"] = at.Ordinal };
        else json["message"] = Message;
        return json;
    }
}

/// <summary>
/// The failure reader and the signature (INTEGRATE1f part f1; D207, the integration design §5.3): every judgement of a failure compares
/// runs made on different trees, so a failure is read as what it is, not as the message a run makes fresh (a GUID, a tree id, a hash, an
/// actual value) or the line number a line inserted above moves.
/// </summary>
/// <remarks>
/// <b>A twin</b> of <c>tools/test-failures.mjs</c> (<c>readTrx</c>, <c>readVitest</c>, <c>readNodeTap</c>, <c>readReport</c>, <c>signatureOf</c>,
/// <c>gateSignature</c>, <c>signatureKey</c>): no code is shared, so both hold one table, the driver tests' <c>fixtures/failure-signatures.json</c>,
/// row for row (<c>FailureReadingTests</c>, <c>integration-failures.test.ts</c>), and a change to one is a change to both. The patterns keep
/// JavaScript's meaning, not .NET's: a word is ASCII, a digit is 0 to 9, and a space is the set JavaScript's <c>\s</c> holds.
/// </remarks>
internal static class FailureReader
{
    // JavaScript's \s and '.' spelled out, since .NET's are wider (Unicode digits and letters) or narrower (its '.' takes \r). The wider
    // spaces (no-break, Ogham, the en to hair spaces, line and paragraph separator, narrow no-break, medium math, ideographic, and the byte
    // order mark) are built from their code points, so that no separator stands in this source as a character.
    private static readonly char[] WideSpaces =
        [.. new[] { 0xA0, 0x1680 }.Concat(Enumerable.Range(0x2000, 11)).Concat([0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF]).Select(code => (char)code)];

    private static readonly string Space = @" \t\n\v\f\r" + new string(WideSpaces);

    private static readonly string Dot = @"[^\n\r" + (char)0x2028 + (char)0x2029 + "]";

    private const string NotWord = @"(?![A-Za-z0-9_])";

    private const string NotAfterWord = @"(?<![A-Za-z0-9_])";

    private static Regex Rx(string pattern, bool ignoreCase = false, bool multiline = false) =>
        new(pattern, RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) | (multiline ? RegexOptions.Multiline : RegexOptions.None));

    private static readonly Regex GuidText = Rx(NotAfterWord + "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}" + NotWord, ignoreCase: true);

    private static readonly Regex PathText = Rx(
        @"(?:file:///?)?(?:[A-Za-z]:[\\/]|\\\\|/(?=[A-Za-z0-9_.-]+/))[^" + Space + @"'""`:,;)\]}>]*");

    private static readonly Regex Hex = Rx(NotAfterWord + "(?=[0-9a-f]*[a-f])(?=[0-9a-f]*[0-9])[0-9a-f]{8,}" + NotWord, ignoreCase: true);

    private static readonly Regex Number = Rx(@"[0-9]+(?:\.[0-9]+)?");

    private static readonly Regex Spaces = Rx("[" + Space + "]+");

    private static readonly Regex NewLine = Rx(@"\r?\n");

    // ---------------------------------------------------------------------------------------------------
    // Normalising a line

    /// <summary>
    /// A message or source line with what a run makes fresh replaced: GUIDs, hex runs of eight characters or more, absolute paths, then
    /// numbers; its spaces collapsed. Two runs of one failure read the same line.
    /// </summary>
    public static string Normalise(string? line)
    {
        var text = GuidText.Replace(line ?? "", "<guid>");
        text = PathText.Replace(text, "<path>");
        text = Hex.Replace(text, "<hex>");
        text = Number.Replace(text, "<n>");
        return Trim(Spaces.Replace(text, " "));
    }

    /// <summary>JavaScript's <c>trim</c>: its white space, which is not <see cref="char.IsWhiteSpace(char)"/>'s (U+FEFF is one, U+0085 is not).</summary>
    private static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start])) start += 1;
        while (end > start && IsSpace(text[end - 1])) end -= 1;
        return text[start..end];
    }

    private static bool IsSpace(char c) => c is '\t' or '\n' or '\v' or '\f' or '\r' or ' ' || Array.IndexOf(WideSpaces, c) >= 0;

    // ---------------------------------------------------------------------------------------------------
    // Frames

    private static readonly Regex Entity = Rx(@"&(amp|lt|gt|quot|apos|#([0-9]+)|#x([0-9a-f]+));", ignoreCase: true);

    private static readonly Regex Attribute = Rx(@"([A-Za-z_:][A-Za-z0-9_:.-]*)=""([^""]*)""");

    private static string Unxml(string? text) => Entity.Replace(text ?? "", match =>
    {
        if (match.Groups[2].Success) return CodePoint(match.Groups[2].Value, NumberStyles.None, match.Value);
        if (match.Groups[3].Success) return CodePoint(match.Groups[3].Value, NumberStyles.AllowHexSpecifier, match.Value);
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "amp" => "&", "lt" => "<", "gt" => ">", "quot" => "\"", _ => "'",
        };
    });

    private static string CodePoint(string digits, NumberStyles style, string whole) =>
        int.TryParse(digits, style, CultureInfo.InvariantCulture, out var point) && point is >= 0 and <= 0x10FFFF && (point < 0xD800 || point > 0xDFFF)
            ? char.ConvertFromUtf32(point)
            : whole;

    private static Dictionary<string, string> Attributes(string tag)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Attribute.Matches(tag)) found[match.Groups[1].Value] = Unxml(match.Groups[2].Value);
        return found;
    }

    private static readonly Regex MethodArguments = Rx(@"\(" + Dot + "*$");

    private static readonly Regex DisplayClass = Rx(@"\.<>c(?:__DisplayClass[A-Za-z0-9_]*)?\.");

    private static readonly Regex Wrapped = Rx(@"\.<([^>]+)>[a-z]__[^.]*(?:\.MoveNext)?$");

    private static readonly Regex WrappedMoveNext = Rx(@"\.<([^>]+)>[a-z]__[^.]*\.MoveNext$");

    private static readonly Regex ClosureTail = Rx(@"\+<>c$");

    /// <summary>
    /// A .NET method as a frame names it, its compiler's wrapping removed: <c>N.C.&lt;Run&gt;d__5.MoveNext()</c> is <c>N.C.Run</c>, a lambda's
    /// <c>N.C.&lt;&gt;c__DisplayClass3_0.&lt;Run&gt;b__0()</c> is <c>N.C.Run</c>, a local function's <c>N.C.&lt;Run&gt;g__Local|2_0</c> is <c>N.C.Run</c>.
    /// </summary>
    public static string DotnetMethod(string method)
    {
        var text = Trim(MethodArguments.Replace(method, ""));
        text = DisplayClass.Replace(text, ".");
        text = Wrapped.Replace(text, ".$1");
        text = WrappedMoveNext.Replace(text, ".$1");
        return ClosureTail.Replace(text, "");
    }

    private static readonly Regex DotnetFrame = Rx("^[" + Space + "]*at[" + Space + "]+(" + Dot + "+?)(?:[" + Space + "]+in[" + Space + "]+(" + Dot + "+):line[" + Space + "]+([0-9]+))?[" + Space + "]*$");

    /// <summary>The frames of a .NET stack trace: <c>at N.C.M() in &lt;file&gt;:line &lt;n&gt;</c>; a frame without a file is kept with none.</summary>
    public static IReadOnlyList<FailureFrame> DotnetFrames(string? stack)
    {
        var frames = new List<FailureFrame>();
        foreach (var line in NewLine.Split(stack ?? ""))
        {
            var at = DotnetFrame.Match(line);
            if (!at.Success) continue;
            frames.Add(new FailureFrame(DotnetMethod(at.Groups[1].Value), at.Groups[2].Success ? at.Groups[2].Value : null, at.Groups[3].Success ? int.Parse(at.Groups[3].Value, CultureInfo.InvariantCulture) : null));
        }

        return frames;
    }

    private static readonly Regex LeadingAt = Rx("^at[" + Space + "]+");

    private static readonly Regex V8Named = Rx("^(" + Dot + @"*?)[" + Space + @"]*\((" + Dot + @"+):([0-9]+):([0-9]+)\)$");

    private static readonly Regex V8Bare = Rx("^()([^" + Space + "]" + Dot + @"*):([0-9]+):([0-9]+)$");

    private static readonly Regex FileUrl = Rx("^file://(?:/)?");

    private static readonly Regex InternalFile = Rx(@"^(node:|internal/)");

    /// <summary>
    /// The frames of a V8 stack (vitest's, <c>node --test</c>'s): <c>at M (&lt;file&gt;:&lt;line&gt;:&lt;col&gt;)</c> or <c>at &lt;file&gt;:&lt;line&gt;:&lt;col&gt;</c>,
    /// a <c>file://</c> URL read as its path; a TAP stack's lines carry no <c>at</c>.
    /// </summary>
    public static IReadOnlyList<FailureFrame> V8Frames(string? stack)
    {
        var frames = new List<FailureFrame>();
        foreach (var raw in NewLine.Split(stack ?? ""))
        {
            var line = LeadingAt.Replace(Trim(raw), "");
            var framed = V8Named.Match(line);
            if (!framed.Success) framed = V8Bare.Match(line);
            if (!framed.Success) continue;
            var file = framed.Groups[2].Value;
            if (file.StartsWith("file://", StringComparison.Ordinal)) file = Uri.UnescapeDataString(FileUrl.Replace(file, ""));
            if (InternalFile.IsMatch(file)) continue;
            var method = framed.Groups[1].Value;
            frames.Add(new FailureFrame(method.Length == 0 ? "<anonymous>" : method, file, int.Parse(framed.Groups[3].Value, CultureInfo.InvariantCulture)));
        }

        return frames;
    }

    private static readonly Regex AbsolutePath = Rx("^(?:[A-Za-z]:)?/");

    private static readonly Regex Reached = Rx("/(?:src|tools|examples|canon)/");

    /// <summary>
    /// A path as a frame gives it, made relative to the repository: from its root where it is under it, else from its first <c>src/</c>,
    /// <c>tools/</c>, <c>examples/</c> or <c>canon/</c> part; null for an absolute path that holds none.
    /// </summary>
    public static string? RepositoryPath(string? file, string? root = null)
    {
        if (string.IsNullOrEmpty(file)) return null;
        var path = file.Replace('\\', '/');
        if (!string.IsNullOrEmpty(root))
        {
            var folder = root.Replace('\\', '/');
            if (folder.EndsWith('/')) folder = folder[..^1];
            if (path.ToLowerInvariant().StartsWith(folder.ToLowerInvariant() + "/", StringComparison.Ordinal)) return path[(folder.Length + 1)..];
        }

        if (!AbsolutePath.IsMatch(path)) return path;
        var at = Reached.Match(path);
        return at.Success ? path[(at.Index + 1)..] : null;
    }

    // ---------------------------------------------------------------------------------------------------
    // The exception type

    private static readonly Regex TypedWithDetail = Rx("^([A-Za-z_][A-Za-z0-9_.`]*(?:Exception|Error))(?:[" + Space + @"]*\[[^\]]*\])?[" + Space + "]*:");

    private static readonly Regex TypedAlone = Rx("^([A-Za-z_][A-Za-z0-9_.`]*(?:Exception|Error))$");

    private static readonly Regex Asserted = Rx(@"^(Assert\.[A-Za-z0-9_]+)\(\)");

    /// <summary>A failure's exception type, from its message's first line: <c>System.X : ...</c> or <c>TypeError: ...</c>, an xUnit <c>Assert.Equal()</c> by its assertion.</summary>
    public static string ExceptionOf(string? message, string? named = null)
    {
        if (!string.IsNullOrEmpty(named)) return named;
        var first = Trim(NewLine.Split(message ?? "")[0]);
        var typed = TypedWithDetail.Match(first);
        if (!typed.Success) typed = TypedAlone.Match(first);
        if (typed.Success) return typed.Groups[1].Value;
        var asserted = Asserted.Match(first);
        return asserted.Success ? asserted.Groups[1].Value : "";
    }

    private static string FirstLine(string? message) => NewLine.Split(message ?? "").Select(Trim).FirstOrDefault(line => line.Length > 0) ?? "";

    // ---------------------------------------------------------------------------------------------------
    // The readers

    private static readonly Regex UnitTest = Rx("<UnitTest" + NotWord + "[^>]*?" + NotAfterWord + "id=\"([^\"]+)\"[^>]*>([\\s\\S]*?)</UnitTest>");

    private static readonly Regex TestMethod = Rx("<TestMethod" + NotWord + "[^>]*>");

    private static readonly Regex UnitTestResult = Rx("(<UnitTestResult" + NotWord + "[^>]*?)(?:/>|>[\\s\\S]*?</UnitTestResult>)");

    private static readonly Regex MessageElement = Rx(@"<Message>([\s\S]*?)</Message>");

    private static readonly Regex StackElement = Rx(@"<StackTrace>([\s\S]*?)</StackTrace>");

    /// <summary>The failures of a trx: each failed result with its case, class, exception, message line and frames.</summary>
    public static IReadOnlyList<ReadFailure> ReadTrx(string trx)
    {
        var classOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match definition in UnitTest.Matches(trx))
        {
            var method = TestMethod.Match(definition.Groups[2].Value);
            if (method.Success) classOf[definition.Groups[1].Value] = Attributes(method.Value).GetValueOrDefault("className") ?? "";
        }

        var failures = new List<ReadFailure>();
        foreach (Match found in UnitTestResult.Matches(trx))
        {
            var result = Attributes(found.Groups[1].Value);
            var outcome = result.GetValueOrDefault("outcome");
            if (outcome is not ("Failed" or "Error" or "Timeout" or "Aborted")) continue;
            var test = result.GetValueOrDefault("testName") ?? "";
            var defined = result.TryGetValue("testId", out var id) && classOf.TryGetValue(id, out var named) ? named : "";
            var unit = defined.Length > 0 ? defined : ClassOf(test);
            var message = Unxml(MessageElement.Match(found.Value) is { Success: true } m ? m.Groups[1].Value : "");
            var stack = Unxml(StackElement.Match(found.Value) is { Success: true } s ? s.Groups[1].Value : "");
            failures.Add(new ReadFailure("trx", unit, test, ExceptionOf(message), FirstLine(message), DotnetFrames(stack)));
        }

        return failures;
    }

    /// <summary>A test's class from its name where its definition names none: the name without its arguments, without its last part.</summary>
    private static string ClassOf(string test)
    {
        var parts = MethodArguments.Replace(test, "").Split('.');
        return string.Join('.', parts.Take(parts.Length - 1));
    }

    /// <summary>The failures of vitest's JSON report: each failed assertion result with its file, its titles, exception, message line and frames.</summary>
    public static IReadOnlyList<ReadFailure> ReadVitest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var failures = new List<ReadFailure>();
        if (!document.RootElement.TryGetProperty("testResults", out var files) || files.ValueKind != JsonValueKind.Array) return failures;
        foreach (var file in files.EnumerateArray())
        {
            if (!file.TryGetProperty("assertionResults", out var results) || results.ValueKind != JsonValueKind.Array) continue;
            foreach (var result in results.EnumerateArray())
            {
                if (Str(result, "status") != "failed") continue;
                var text = result.TryGetProperty("failureMessages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0
                    ? messages[0].ValueKind == JsonValueKind.String ? messages[0].GetString()! : messages[0].GetRawText()
                    : "";
                IEnumerable<string> titles = result.TryGetProperty("ancestorTitles", out var ancestors) && ancestors.ValueKind == JsonValueKind.Array
                    ? ancestors.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString()! : "")
                    : Enumerable.Empty<string>();
                var test = Str(result, "fullName") is { Length: > 0 } full ? full : string.Join(" > ", titles.Append(Str(result, "title") ?? ""));
                failures.Add(new ReadFailure("vitest", Str(file, "name"), test, ExceptionOf(text), FirstLine(text), V8Frames(text)));
            }
        }

        return failures;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static readonly Regex Subtest = Rx("^([" + Space + @"]*)# Subtest: (" + Dot + "*)$");

    private static readonly Regex TapResult = Rx("^([" + Space + @"]*)(not ok|ok) [0-9]+ - (" + Dot + "*?)(?:[" + Space + "]+#" + Dot + "*)?$");

    private static readonly Regex Position = Rx(@":[0-9]+:[0-9]+$");

    private static readonly Regex SingleQuoted = Rx("^'(" + Dot + @"*)'$");

    private static readonly Regex DoubleQuoted = Rx("^\"(" + Dot + "*)\"$");

    /// <summary>A TAP block's YAML value: a plain or quoted scalar, or a <c>|-</c> block's lines.</summary>
    private static string? YamlValue(IReadOnlyList<string> lines, string key, string indent)
    {
        var at = -1;
        for (var each = 0; each < lines.Count; each += 1)
        {
            if (lines[each].StartsWith($"{indent}{key}:", StringComparison.Ordinal))
            {
                at = each;
                break;
            }
        }

        if (at < 0) return null;
        var rest = Trim(lines[at][(indent.Length + key.Length + 1)..]);
        if (rest is "|-" or "|" or ">-")
        {
            var block = new List<string>();
            foreach (var line in lines.Skip(at + 1))
            {
                if (!line.StartsWith($"{indent}  ", StringComparison.Ordinal) && Trim(line).Length > 0) break;
                block.Add(line.Length > indent.Length + 2 ? line[(indent.Length + 2)..] : "");
            }

            return string.Join('\n', block);
        }

        var unquoted = SingleQuoted.Replace(rest, match => match.Groups[1].Value.Replace("''", "'", StringComparison.Ordinal));
        return DoubleQuoted.Replace(unquoted, "$1");
    }

    /// <summary>
    /// The failures of a <c>node --test --test-reporter=tap</c> report: each <c>not ok</c> that failed in its own code (<c>failureType:
    /// 'testCodeFailure'</c>, or a hook's), never a suite failing only for its subtests. Its name is its titles joined, its unit the
    /// file its <c>location</c> names, or the <c># Subtest:</c> file at the top.
    /// </summary>
    public static IReadOnlyList<ReadFailure> ReadNodeTap(string tap, string? unit = null)
    {
        var lines = tap.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var failures = new List<ReadFailure>();
        var titles = new List<string?>();
        for (var at = 0; at < lines.Length; at += 1)
        {
            var subtest = Subtest.Match(lines[at]);
            if (subtest.Success)
            {
                var level = subtest.Groups[1].Length / 4;
                Resize(titles, level);
                Resize(titles, level + 1);
                titles[level] = subtest.Groups[2].Value;
                continue;
            }

            var result = TapResult.Match(lines[at]);
            if (!result.Success) continue;
            var indent = result.Groups[1].Value;
            var depth = indent.Length / 4;
            var block = new List<string>();
            if (at + 1 < lines.Length && Trim(lines[at + 1]) == "---")
            {
                for (var next = at + 2; next < lines.Length && Trim(lines[next]) != "..."; next += 1) block.Add(lines[next]);
            }

            if (result.Groups[2].Value == "not ok")
            {
                var inner = $"{indent}  ";
                if (YamlValue(block, "failureType", inner) != "subtestsFailed")
                {
                    var location = YamlValue(block, "location", inner);
                    var file = string.IsNullOrEmpty(location) ? null : Position.Replace(location, "").Replace("\\\\", "\\", StringComparison.Ordinal);
                    var error = YamlValue(block, "error", inner) ?? "";
                    var name = YamlValue(block, "name", inner);
                    var stack = YamlValue(block, "stack", inner) ?? "";
                    var named = titles.Take(depth).Append(result.Groups[3].Value).Where(title => !string.IsNullOrEmpty(title));
                    failures.Add(new ReadFailure("node", unit ?? (string.IsNullOrEmpty(file) ? titles.FirstOrDefault() : file), string.Join(" > ", named), ExceptionOf(error, name), FirstLine(error), V8Frames(stack)));
                }
            }

            Resize(titles, depth);
        }

        return failures;
    }

    /// <summary>A JavaScript array's <c>length = n</c>: shortened, or extended with nothing in the new places.</summary>
    private static void Resize(List<string?> list, int length)
    {
        if (list.Count > length) list.RemoveRange(length, list.Count - length);
        while (list.Count < length) list.Add(null);
    }

    private static readonly Regex TestRun = Rx("<TestRun" + NotWord);

    private static readonly Regex TapStart = Rx(@"^(TAP version|# Subtest:|(not )?ok [0-9])", multiline: true);

    /// <summary>A report's failures, its kind told by its content: a trx, vitest's JSON, or <c>node --test</c>'s TAP.</summary>
    /// <exception cref="FormatException">The report is none of them.</exception>
    public static IReadOnlyList<ReadFailure> ReadReport(string text, string? unit = null)
    {
        var start = 0;
        while (start < text.Length && IsSpace(text[start])) start += 1;
        var trimmed = text[start..];
        if (trimmed.StartsWith('<') && TestRun.IsMatch(trimmed)) return ReadTrx(trimmed);
        if (trimmed.StartsWith('{')) return ReadVitest(trimmed);
        if (TapStart.IsMatch(trimmed)) return ReadNodeTap(trimmed, unit);
        throw new FormatException("not a trx, a vitest JSON report or a node --test TAP report");
    }

    // ---------------------------------------------------------------------------------------------------
    // The signature

    /// <summary>The test's own file in a frame, as the failure names its unit: the class's simple name for a trx, the unit's path otherwise.</summary>
    private static FailureFrame? OwnFrame(ReadFailure failure, string? root)
    {
        if (failure.Runner == "trx")
        {
            var unit = (failure.Unit ?? "").Split('+')[0];
            return failure.Frames.FirstOrDefault(frame => !string.IsNullOrEmpty(frame.File)
                && (frame.Method.StartsWith($"{unit}.", StringComparison.Ordinal) || frame.Method.StartsWith($"{failure.Unit}.", StringComparison.Ordinal)));
        }

        var own = RepositoryPath(failure.Unit, root);
        return failure.Frames.FirstOrDefault(frame => !string.IsNullOrEmpty(frame.File) && own is not null && RepositoryPath(frame.File, root) == own);
    }

    /// <summary>Where a method's body starts above <paramref name="line"/> (1-based): the nearest line above that declares it, or the file's first.</summary>
    private static int MethodStart(string[] lines, int line, string method)
    {
        var name = method.Split('.')[^1].Replace("<", "", StringComparison.Ordinal).Replace(">", "", StringComparison.Ordinal);
        if (name.Length == 0 || name == "anonymous") return 0;
        var boundary = char.IsAsciiLetterOrDigit(name[0]) || name[0] == '_' ? NotAfterWord : "(?<=[A-Za-z0-9_])";
        var declares = Rx(boundary + Regex.Escape(name) + "[" + Space + @"]*[(<]");
        for (var at = line - 1; at >= 0; at -= 1)
        {
            if (at < lines.Length && declares.IsMatch(lines[at])) return at;
        }

        return 0;
    }

    /// <summary>
    /// A failure's signature: its test with its case, its exception type, and its location as the method, the text of the line and its
    /// ordinal, the text read from the tree the run used through <paramref name="read"/> (repository-relative); or, where no location can
    /// be read, its message line normalised.
    /// </summary>
    public static FailureSignature SignatureOf(ReadFailure failure, Func<string, string?>? read = null, string? root = null)
    {
        var frame = OwnFrame(failure, root);
        var path = frame is null ? null : RepositoryPath(frame.File, root);
        var source = !string.IsNullOrEmpty(path) && read is not null ? read(path) : null;
        if (frame is { Line: > 0 } && !string.IsNullOrEmpty(source))
        {
            var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var line = frame.Line.Value;
            var text = Normalise(line - 1 < lines.Length ? lines[line - 1] : "");
            if (text.Length > 0)
            {
                var from = MethodStart(lines, line, frame.Method);
                var ordinal = 0;
                for (var at = from; at < line - 1; at += 1)
                {
                    if (Normalise(lines[at]) == text) ordinal += 1;
                }

                return new FailureSignature { Test = failure.Test, Exception = failure.Exception, Location = new FailureLocation(frame.Method, text, ordinal) };
            }
        }

        return new FailureSignature { Test = failure.Test, Exception = failure.Exception, Message = Normalise(failure.Message) };
    }

    private static readonly Regex LineEnds = Rx(@"\r\n|\r|" + (char)0x2028 + "|" + (char)0x2029);

    private static readonly Regex CompilerError = Rx("^[" + Space + @"]*(" + Dot + @"+?)\([0-9]+,[0-9]+\):[" + Space + "]+error[" + Space + "]+((?:CS|TS|MSB|NU)[0-9]+)" + NotWord, multiline: true);

    private static readonly Regex TypeScriptError = Rx(@"^(" + Dot + @"+?\.tsx?)\([0-9]+,[0-9]+\): error (TS[0-9]+)", multiline: true);

    private static readonly Regex ErrorLine = Rx(NotAfterWord + "error" + NotWord + "|" + NotAfterWord + "failed" + NotWord + "|" + NotAfterWord + "error:", ignoreCase: true);

    /// <summary>
    /// A gate that names no failing test, signed by its step: for a build, the set of its compiler errors' codes with their files
    /// (<c>CS0103 src/.../X.cs</c>), sorted, since MSBuild prints each project's errors as its node finishes; otherwise the step's first
    /// error line, normalised.
    /// </summary>
    public static FailureSignature GateSignature(string gate, string step, string? output, string? root = null)
    {
        var text = output ?? "";
        var scanned = LineEnds.Replace(text, "\n");
        var errors = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in CompilerError.Matches(scanned)) errors.Add(ErrorOf(match.Groups[2].Value, match.Groups[1].Value, root));
        foreach (Match match in TypeScriptError.Matches(scanned)) errors.Add(ErrorOf(match.Groups[2].Value, match.Groups[1].Value, root));
        if (step == "build" && errors.Count > 0)
        {
            var sorted = errors.ToList();
            sorted.Sort(StringComparer.Ordinal);
            return new FailureSignature { Gate = gate, Step = step, Errors = sorted };
        }

        var first = NewLine.Split(text).FirstOrDefault(line => ErrorLine.IsMatch(line)) ?? "";
        return new FailureSignature { Gate = gate, Step = step, Message = Normalise(first) };
    }

    private static string ErrorOf(string code, string file, string? root) => $"{code} {RepositoryPath(Trim(file), root) ?? Trim(file)}";

    // ---------------------------------------------------------------------------------------------------
    // The key

    /// <summary>Whether two signatures are one failure: every field equal, a build's errors as sets.</summary>
    public static bool Same(FailureSignature? a, FailureSignature? b) => KeyOf(a) == KeyOf(b);

    /// <summary>A signature as one string, its fields in a fixed order; none is the empty string. The tool's <c>signatureKey</c>, character for character.</summary>
    public static string KeyOf(FailureSignature? signature)
    {
        if (signature is null) return "";
        var key = new StringBuilder("[");
        if (signature.Gate is not null)
        {
            key.Append(Quote(signature.Gate)).Append(',').Append(Quote(signature.Step)).Append(',');
            if (signature.Errors is { } errors)
            {
                var sorted = errors.ToList();
                sorted.Sort(StringComparer.Ordinal);
                key.Append('[').Append(string.Join(',', sorted.Select(Quote))).Append(']');
            }
            else
            {
                key.Append("null");
            }

            key.Append(',').Append(Quote(signature.Message));
        }
        else
        {
            key.Append(Quote(signature.Test)).Append(',').Append(Quote(signature.Exception)).Append(',');
            if (signature.Location is { } at)
            {
                key.Append('[').Append(Quote(at.Method)).Append(',').Append(Quote(at.Text)).Append(',').Append(at.Ordinal.ToString(CultureInfo.InvariantCulture)).Append("],null");
            }
            else
            {
                key.Append("null,").Append(Quote(signature.Message));
            }
        }

        return key.Append(']').ToString();
    }

    /// <summary>A string as JavaScript's <c>JSON.stringify</c> writes it, or <c>null</c>: only quotes, backslashes and control characters escaped.</summary>
    internal static string Quote(string? text)
    {
        if (text is null) return "null";
        var quoted = new StringBuilder("\"");
        for (var at = 0; at < text.Length; at += 1)
        {
            var c = text[at];
            switch (c)
            {
                case '"': quoted.Append("\\\""); break;
                case '\\': quoted.Append("\\\\"); break;
                case '\b': quoted.Append("\\b"); break;
                case '\f': quoted.Append("\\f"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': quoted.Append("\\r"); break;
                case '\t': quoted.Append("\\t"); break;
                default:
                    var lone = char.IsSurrogate(c)
                        && !(char.IsHighSurrogate(c) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1]))
                        && !(char.IsLowSurrogate(c) && at > 0 && char.IsHighSurrogate(text[at - 1]));
                    if (c < ' ' || lone) quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else quoted.Append(c);
                    break;
            }
        }

        return quoted.Append('"').ToString();
    }
}
