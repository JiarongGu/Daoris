namespace Daoris.Knowledge.Http;

/// <summary>
/// A request whose route threw (HOSTLOG1): what was thrown goes into the machine log, and the caller is
/// answered a sentence in the house's <see cref="ErrorResponse"/> shape instead of an empty 500.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> On an install, two routes answered 500 for a stretch while others answered, and the
/// log held only the server's own line that something was thrown, so the cause could not be read. The
/// host now catches the throw itself, before the server does, and writes it as the <c>error</c> event
/// (machine-log design §4).</para>
///
/// <para><b>What is written</b>: <c>where</c> as the method and the route's pattern, each also on its own;
/// the exception's <c>type</c> and <c>message</c>; its first <see cref="Frames"/> frames as the runtime
/// gives them, in <c>stack</c>; and, when it wraps another, the innermost one's type and message in
/// <c>inner</c>, since a wrapper alone can hide the cause. <b>Never the request's body, its query or a
/// header</b>: the route is its pattern, so an id in the path stays out, and a value of the query or the
/// path that the exception's message repeats (a search a parser refused, an id it could not find) is cut
/// out of it (design §5).</para>
///
/// <para><b>What is answered</b>: <see cref="Logged"/>, or <see cref="Unlogged"/> on a host with no home,
/// which keeps no log. Neither names anything of the exception: its message and its frames can carry a
/// machine path, and a browser, or a shared host's keyed caller, is never told one (D46, D47 §4).</para>
/// </remarks>
public static class UnhandledRequests
{
    /// <summary>The answer when the exception was written to the log.</summary>
    public const string Logged = "The service hit an error it did not expect, and wrote what it was to its log.";

    /// <summary>The answer on a host with no home, which writes no log (machine-log design §2).</summary>
    public const string Unlogged =
        "The service hit an error it did not expect, and keeps no log to say what it was: it runs with no DAORIS_HOME.";

    /// <summary>How many of the stack's frames are written: enough to place the throw, not the whole pipeline.</summary>
    public const int Frames = 5;

    /// <summary>What a request value must be at least to be cut from a message: shorter ones are digits and flags.</summary>
    private const int CutFrom = 3;

    /// <summary>What a cut value is replaced with.</summary>
    private const string Cut = "…";

    /// <summary>The route's pattern, never its values; <c>(no route)</c> when none matched.</summary>
    public static string RouteOf(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(no route)";

    /// <summary>
    /// Whether <paramref name="error"/> is the caller hanging up rather than the service failing: it is
    /// left to the server as before, and is no error of the service's.
    /// </summary>
    public static bool CallerLeft(HttpContext context, Exception error) =>
        error is OperationCanceledException && context.RequestAborted.IsCancellationRequested;

    /// <summary>Write <paramref name="error"/> as the <c>error</c> event; true when the log keeps it.</summary>
    public static bool Write(MachineLog log, HttpContext context, Exception error)
    {
        var method = context.Request.Method;
        var route = RouteOf(context);
        var values = RequestValues(context);
        var innermost = error.GetBaseException();
        log.Error("error",
            ("where", $"{method} {route}"),
            ("method", method),
            ("route", route),
            ("type", error.GetType().FullName),
            ("message", Without(error.Message, values)),
            ("stack", FirstFrames(error.StackTrace)),
            ("inner", ReferenceEquals(innermost, error)
                ? null
                : $"{innermost.GetType().FullName}: {Without(innermost.Message, values)}"));
        return log.Writing;
    }

    /// <summary>
    /// Answer the caller with the sentence, once nothing of the route's own answer has been sent; false
    /// when it has, and then the server ends the response as it would have.
    /// </summary>
    public static async Task<bool> AnswerAsync(HttpContext context, bool logged)
    {
        if (context.Response.HasStarted) return false;

        // Whatever the route set before it threw goes; a callback registered on the response's start (the
        // CORS policy's) is the response's, not the route's, and still runs.
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(logged ? Logged : Unlogged));
        return true;
    }

    /// <summary>The first <paramref name="count"/> lines of a stack trace, as written; null for none.</summary>
    public static string? FirstFrames(string? stack, int count = Frames)
    {
        if (string.IsNullOrWhiteSpace(stack)) return null;
        var frames = stack
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Trim().Length > 0)
            .Take(count);
        return string.Join('\n', frames);
    }

    /// <summary><paramref name="message"/> with every one of <paramref name="values"/> it contains cut out.</summary>
    public static string Without(string message, IReadOnlyList<string> values)
    {
        foreach (var value in values)
        {
            message = message.Replace(value, Cut, StringComparison.OrdinalIgnoreCase);
        }

        return message;
    }

    /// <summary>The query's and the path's values worth cutting, the longest first, so one inside another goes whole.</summary>
    private static IReadOnlyList<string> RequestValues(HttpContext context) =>
        context.Request.Query.SelectMany(pair => pair.Value.Select(value => value ?? ""))
            .Concat(context.Request.RouteValues.Values.Select(value => value as string ?? ""))
            .Where(value => value.Length >= CutFrom)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => value.Length)
            .ToList();
}
