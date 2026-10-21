using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The change language <c>daoris driver workflow edit</c> takes (PUSH1f2b; D157, the workflow design §4.7), applied to a version's
/// steps as saved, in order, all making one version: <c>add &lt;kind&gt; --after &lt;step&gt; [--id &lt;id&gt;] [&lt;field&gt;=&lt;value&gt;…]</c>,
/// <c>remove &lt;step&gt;</c>, <c>set &lt;step&gt; &lt;field&gt;=&lt;value&gt;…</c>, <c>move &lt;step&gt; --after &lt;step&gt;</c>, <c>on &lt;step&gt;
/// failed=…</c>. A value is read by its field's type (a switch, a list of names by commas); an empty value clears the field. What the
/// steps then say is the version's reading's to judge (<see cref="WorkflowNamed.PlanAdd"/>). The CLI's <c>applyChanges</c>, word for word.
/// </summary>
public static class WorkflowChanges
{
    /// <summary>The language, as a refusal names it.</summary>
    public const string Said = "a change is add <kind> --after <step> [--id <id>] [<field>=<value>…], remove <step>, set <step> "
        + "<field>=<value>…, move <step> --after <step>, or on <step> failed=wait|send-back|stop — e.g. `daoris driver workflow "
        + "edit docs --base 1 remove look set landing accept=automatic`.";

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) { "add", "remove", "set", "move", "on" };

    /// <summary>
    /// <paramref name="tokens"/> applied to <paramref name="steps"/>, the steps of v<paramref name="version"/> as its file holds them; an
    /// id another kind took in a version of <paramref name="read"/> is never given to a new step.
    /// </summary>
    /// <exception cref="ChoiceWords">The words are no change (exit 2), or name a step there is none of (exit 1).</exception>
    public static JsonArray Apply(JsonArray steps, IReadOnlyList<string> tokens, int version, NamedWorkflowRead read)
    {
        var made = steps.Select(step => step!.DeepClone().AsObject()).ToList();

        int At(string? id)
        {
            var index = id is null ? -1 : made.FindIndex(step => (string?)TextOf(step, "id") == id);
            if (index == -1)
            {
                throw new ChoiceWords($"no step `{id ?? ""}` in v{version} — its steps are {string.Join(", ", made.Select(step => $"`{TextOf(step, "id")}`"))}.", 1);
            }

            return index;
        }

        void Assign(JsonObject step, string token)
        {
            var equals = token.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0) throw new ChoiceWords($"`{token}` is no <field>=<value> — {Said}");
            var name = token[..equals];
            var value = token[(equals + 1)..];
            if (name is "id" or "kind") throw new ChoiceWords($"a step's `{name}` is not changed — a new step takes a new id: {Said}");
            if (value.Length == 0)
            {
                step.Remove(name);
                return;
            }

            var type = WorkflowKindTable.Of(TextOf(step, "kind") ?? "")?.Fields.FirstOrDefault(field => field.Name == name)?.Type;
            if (type == WorkflowFieldTypes.Flag && value is "true" or "false") step[name] = value == "true";
            else if (type == WorkflowFieldTypes.Names || (type == WorkflowFieldTypes.Checks && value != "required"))
            {
                step[name] = new JsonArray([.. value.Split(',').Select(each => (JsonNode?)JsonValue.Create(each.Trim()))]);
            }
            else step[name] = value;
        }

        bool UsedFor(string id, string kind) =>
            made.Any(step => TextOf(step, "id") == id)
            || read.Versions.Any(each => each.Steps?.Any(step => step.Id == id && step.Kind != kind) == true);

        var i = 0;
        while (i < tokens.Count)
        {
            var word = tokens[i++];
            var operands = new List<string>();
            while (i < tokens.Count && !Keywords.Contains(tokens[i]))
            {
                var token = tokens[i++];
                if (token.StartsWith("--", StringComparison.Ordinal) && token is not ("--after" or "--id"))
                {
                    throw new ChoiceWords($"`{token}` is not a flag a change takes — {Said}");
                }

                operands.Add(token);
                if (token is "--after" or "--id" && i < tokens.Count) operands.Add(tokens[i++]);
            }

            string? Option(string flag)
            {
                var index = operands.IndexOf(flag);
                return index == -1 ? null : operands.ElementAtOrDefault(index + 1);
            }

            var plain = operands.Where((token, index) => !token.StartsWith("--", StringComparison.Ordinal)
                && (index == 0 || operands[index - 1] is not ("--after" or "--id"))).ToList();
            switch (word)
            {
                case "add":
                {
                    var after = Option("--after");
                    if (plain.Count == 0 || after is null) throw new ChoiceWords($"`add` names a kind and --after <step> — {Said}");
                    var kind = plain[0];
                    var id = Option("--id") ?? kind;
                    for (var n = 2; Option("--id") is null && UsedFor(id, kind); n++) id = $"{kind}-{n}";
                    var step = new JsonObject { ["id"] = id, ["kind"] = kind };
                    foreach (var token in plain.Skip(1)) Assign(step, token);
                    made.Insert(At(after) + 1, step);
                    break;
                }

                case "remove":
                    if (plain.Count != 1) throw new ChoiceWords($"`remove` names one step — {Said}");
                    made.RemoveAt(At(plain[0]));
                    break;
                case "set":
                case "on":
                {
                    if (plain.Count < 2 || (word == "on" && (plain.Count != 2 || !plain[1].StartsWith("failed=", StringComparison.Ordinal))))
                    {
                        throw new ChoiceWords($"`{word}` names a step and {(word == "on" ? "failed=wait|send-back|stop" : "<field>=<value>…")} — {Said}");
                    }

                    var step = made[At(plain[0])];
                    foreach (var token in plain.Skip(1)) Assign(step, token);
                    break;
                }

                case "move":
                {
                    var after = Option("--after");
                    if (plain.Count != 1 || after is null || after == plain[0]) throw new ChoiceWords($"`move` names a step and --after another — {Said}");
                    var from = At(plain[0]);
                    var step = made[from];
                    made.RemoveAt(from);
                    made.Insert(At(after) + 1, step);
                    break;
                }

                default:
                    throw new ChoiceWords($"`{word}` is not a change — {Said}");
            }
        }

        return new JsonArray([.. made]);
    }

    /// <summary>A step's field as text, or null where it holds none.</summary>
    private static string? TextOf(JsonObject step, string name) =>
        step[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
