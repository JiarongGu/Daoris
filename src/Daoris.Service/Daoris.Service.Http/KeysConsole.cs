using Daoris.Knowledge;

namespace Daoris.Knowledge.Http;

/// <summary>
/// `keys mint|list|revoke` — the deployment's own console is where machine credentials come from
/// until person-auth exists (D47 §7). A console verb on the serving binary, against the same store,
/// exiting without ever binding: it is not a web concern, which is why it does not live in Program.cs.
/// Exit codes are the contract: 0 clean, 1 the prefix named nothing, 2 bad usage or a store a newer
/// Daoris wrote.
/// </summary>
internal static class KeysConsole
{
    public static async Task<int> RunAsync(string[] args, ServiceOptions options)
    {
        // The KEY STORE alone — never the composed service. Composing it bootstraps a registry from
        // the configured root (D48 §3), which on a server would import whatever sits beside the binary
        // into a deployment that must be fed and never scanned (D47 §4). Minting a credential has no
        // business touching an index.
        KeyAdministration opened;
        try
        {
            opened = await ServiceFactory.OpenKeysAsync(options);
        }
        catch (NewerStoreException refusal)
        {
            // KSCHEMA1: the file is left as it was, and the refusal is its sentence, as a usage error is.
            Console.Error.WriteLine(refusal.Message);
            return 2;
        }

        await using var composed = opened;

        switch (args)
        {
            case ["mint", ..]:
            {
                string? name = null;
                var days = 90;
                for (var i = 1; i < args.Length - 1; i++)
                {
                    if (args[i] == "--name") name = args[i + 1];
                    if (args[i] == "--days" && int.TryParse(args[i + 1], out var parsed)) days = parsed;
                }

                if (string.IsNullOrWhiteSpace(name) || days <= 0)
                {
                    Console.Error.WriteLine("usage: keys mint --name <person@machine> [--days N]");
                    return 2;
                }

                var minted = await composed.Keys.MintAsync(name, TimeSpan.FromDays(days), DateTimeOffset.UtcNow);
                Console.WriteLine(minted.Key);
                Console.WriteLine($"  name {name} · prefix {minted.Record.Prefix} · expires {minted.Record.Expires:yyyy-MM-dd}");
                Console.WriteLine("  Shown once and stored hashed — copy it now. Revoke by the prefix.");
                return 0;
            }

            case ["list"]:
            {
                var keys = await composed.Keys.ListAsync();
                if (keys.Count == 0)
                {
                    Console.WriteLine("No keys minted. `keys mint --name <person@machine>` creates one.");
                    return 0;
                }

                foreach (var key in keys)
                {
                    var state = key.Revoked is not null ? $"revoked {key.Revoked:yyyy-MM-dd}"
                        : key.Expires <= DateTimeOffset.UtcNow ? $"expired {key.Expires:yyyy-MM-dd}"
                        : $"expires {key.Expires:yyyy-MM-dd}";
                    Console.WriteLine($"  {key.Prefix}  {key.Name}  {state}");
                }

                return 0;
            }

            case ["revoke", var prefix]:
            {
                if (await composed.Keys.RevokeAsync(prefix, DateTimeOffset.UtcNow))
                {
                    Console.WriteLine($"Key `{prefix}` is revoked.");
                    return 0;
                }

                Console.Error.WriteLine($"No key with prefix `{prefix}`. `keys list` names them.");
                return 1;
            }

            default:
                Console.Error.WriteLine("usage: keys mint --name <person@machine> [--days N] | keys list | keys revoke <prefix>");
                return 2;
        }
    }
}
