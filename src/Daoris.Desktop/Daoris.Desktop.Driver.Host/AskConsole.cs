namespace Daoris.Driver.Host;

/// <summary>
/// Asks, from a terminal (D65 §1a, D50): a sentence entered at a WORKSPACE rather than at a repository.
/// </summary>
/// <remarks>
/// <code>
/// daoris-driver ask [--workspace &lt;name&gt;] [--to &lt;repo&gt;] [--file &lt;path&gt;]… [--url &lt;address&gt;]… "…"
/// daoris-driver ask --publish &lt;id&gt; --to &lt;repo&gt;
/// daoris-driver ask --close &lt;id&gt; --reason "…"
/// daoris-driver ask --delete &lt;id&gt;
/// </code>
/// <para><c>--delete</c> removes an ask made by mistake with every quest asked by it, or refuses whole
/// when one of them must stay (D95) — the ask's record's <i>Delete</i> is the other door.</para>
/// <para>The service answers with the tier that answered — by declarations only, with no intake
/// harness, which proposes and publishes nothing; or the receiver named with <c>--to</c>, published
/// at once. The answer is printed verbatim: it is the contract, and a rewording here would be a second
/// one.</para>
///
/// <para>Exit codes keep the family contract: 0 answered · 1 refused (a receiver that cannot be asked,
/// a closed ask) · 2 tool error.</para>
/// </remarks>
internal static class AskConsole
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? workspace = null, to = null, publish = null, close = null, reason = null, delete = null;
        var files = new List<string>();
        var links = new List<string>();
        var words = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new DriverException($"{args[i]} needs a value");

            switch (args[i])
            {
                case "--workspace": workspace = Value(); break;
                case "--to": to = Value(); break;
                case "--file": files.Add(Value()); break;
                case "--url": links.Add(Value()); break;
                case "--publish": publish = Value(); break;
                case "--close": close = Value(); break;
                case "--delete": delete = Value(); break;
                case "--reason": reason = Value(); break;
                default: words.Add(args[i]); break;
            }
        }

        try
        {
            using var service = ServiceClient.FromEnvironment();

            if (publish is not null)
            {
                if (to is null) return Usage("--publish needs --to: the repository the ask becomes a quest for");
                return Report(await service.PublishAskAsync(publish, to).ConfigureAwait(false));
            }

            if (close is not null)
            {
                return Report(await service.CloseAskAsync(close, reason ?? "").ConfigureAwait(false));
            }

            if (delete is not null)
            {
                var (ok, message) = await service.DeleteAskAsync(delete).ConfigureAwait(false);
                Console.WriteLine(message);
                return ok ? 0 : 1;
            }

            var sentence = string.Join(' ', words).Trim();
            if (sentence.Length == 0) return Usage("an ask needs its words");

            // Files are read HERE, on the machine that has them, and travel whole to the local host —
            // which keeps them under this machine's home (D65 §2).
            var read = new List<(string Name, byte[] Content)>();
            foreach (var path in files)
            {
                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) throw new DriverException($"`{path}` is not a file on this machine");
                read.Add((Path.GetFileName(full), await File.ReadAllBytesAsync(full).ConfigureAwait(false)));
            }

            return Report(await service.AskAsync(workspace ?? "default", sentence, links, read, to).ConfigureAwait(false));
        }
        catch (DriverException error)
        {
            Console.Error.WriteLine($"ask: {error.Message}");
            return 2;
        }
        catch (HttpRequestException error)
        {
            Console.Error.WriteLine($"ask: could not reach the service — {error.Message}");
            return 2;
        }
    }

    private static int Report(AskAnswer answer)
    {
        Console.WriteLine(answer.Message);
        if (answer.AskId is not null) Console.WriteLine($"  ask    #{answer.AskId}");
        if (answer.QuestId is not null) Console.WriteLine($"  quest  #{answer.QuestId}");
        return answer.Ok ? 0 : 1;
    }

    private static int Usage(string why)
    {
        Console.Error.WriteLine($"ask: {why}");
        Console.Error.WriteLine("usage: daoris-driver ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… \"…\"");
        Console.Error.WriteLine("       daoris-driver ask --publish <id> --to <repo>");
        Console.Error.WriteLine("       daoris-driver ask --close <id> --reason \"…\"");
        Console.Error.WriteLine("       daoris-driver ask --delete <id>");
        return 2;
    }
}
