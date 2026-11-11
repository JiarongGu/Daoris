using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A TOML document read into JSON nodes: tables, arrays of tables, dotted and quoted keys, the four string forms with their escapes,
/// integers in every base, floats, booleans, arrays and inline tables. A date or a time has no JSON form and is refused, as is a key
/// given twice, a table declared twice or anything else it cannot read: a reader that guessed would hand on what the file never said.
/// </summary>
public static class TomlReader
{
    /// <summary>The document <paramref name="text"/> holds.</summary>
    /// <exception cref="FormatException">The text is not TOML this reader takes.</exception>
    public static JsonObject Read(string text) => new Parser(text).Document();

    private sealed class Parser(string text)
    {
        private readonly HashSet<JsonNode> _declared = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<JsonNode> _closed = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<JsonNode> _tableArrays = new(ReferenceEqualityComparer.Instance);
        private int _at;

        public JsonObject Document()
        {
            var root = new JsonObject();
            var current = root;
            while (true)
            {
                Skip(newlines: true);
                if (_at >= text.Length) return root;
                if (text[_at] == '[')
                {
                    var many = At("[[");
                    _at += many ? 2 : 1;
                    var path = Key();
                    Expect(many ? "]]" : "]");
                    current = many ? Appended(root, path) : Declared(root, path);
                }
                else
                {
                    var path = Key();
                    Expect("=");
                    Put(current, path, Value());
                }

                EndOfLine();
            }
        }

        private JsonObject Parent(JsonObject table, IReadOnlyList<string> path)
        {
            foreach (var segment in path.Take(path.Count - 1))
            {
                var next = table[segment];
                if (next is null) table[segment] = next = new JsonObject();
                if (next is JsonArray array && _tableArrays.Contains(array)) next = array[^1];
                if (next is not JsonObject inner || _closed.Contains(inner)) throw Fault($"`{segment}` is not a table that can be added to");
                table = inner;
            }

            return table;
        }

        private void Put(JsonObject table, IReadOnlyList<string> path, JsonNode value)
        {
            var parent = Parent(table, path);
            if (parent.ContainsKey(path[^1])) throw Fault($"the key `{string.Join('.', path)}` is given twice");
            parent[path[^1]] = value;
        }

        private JsonObject Declared(JsonObject root, IReadOnlyList<string> path)
        {
            var parent = Parent(root, path);
            var held = parent[path[^1]];
            if (held is null) parent[path[^1]] = held = new JsonObject();
            if (held is not JsonObject table || _closed.Contains(table) || !_declared.Add(table)) throw Fault($"the table `{string.Join('.', path)}` is declared twice");
            return table;
        }

        private JsonObject Appended(JsonObject root, IReadOnlyList<string> path)
        {
            var parent = Parent(root, path);
            var held = parent[path[^1]];
            if (held is null)
            {
                var made = new JsonArray();
                _tableArrays.Add(made);
                parent[path[^1]] = held = made;
            }

            if (held is not JsonArray array || !_tableArrays.Contains(array)) throw Fault($"`{string.Join('.', path)}` is not an array of tables");
            var table = new JsonObject();
            array.Add(table);
            return table;
        }

        private List<string> Key()
        {
            var path = new List<string>();
            while (true)
            {
                Skip(newlines: false);
                path.Add(_at < text.Length && text[_at] is '"' or '\'' ? Text() : Bare());
                Skip(newlines: false);
                if (!At(".")) return path;
                _at++;
            }
        }

        private string Bare()
        {
            var start = _at;
            while (_at < text.Length && (char.IsAsciiLetterOrDigit(text[_at]) || text[_at] is '_' or '-')) _at++;
            return _at > start ? text[start.._at] : throw Fault("a key was expected");
        }

        private JsonNode Value()
        {
            Skip(newlines: false);
            if (_at >= text.Length) throw Fault("a value was expected");
            switch (text[_at])
            {
                case '"' or '\'':
                    return JsonValue.Create(Text());
                case '[':
                    return Array();
                case '{':
                    return Inline();
            }

            var start = _at;
            while (_at < text.Length && text[_at] is not (' ' or '\t' or ',' or ']' or '}' or '#' or '\r' or '\n')) _at++;
            return Scalar(text[start.._at].Replace("_", "", StringComparison.Ordinal));
        }

        private JsonNode Scalar(string token)
        {
            if (token is "true" or "false") return JsonValue.Create(token == "true");
            var sign = token.StartsWith('-') ? -1 : 1;
            var digits = token.TrimStart('+', '-');
            if (digits is "inf") return JsonValue.Create(sign * double.PositiveInfinity);
            if (digits is "nan") return JsonValue.Create(double.NaN);
            try
            {
                if (token.Length > 2 && token[0] == '0' && token[1] is 'x' or 'o' or 'b') return JsonValue.Create(Convert.ToInt64(token[2..], token[1] switch { 'x' => 16, 'o' => 8, _ => 2 }));
                if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)) return JsonValue.Create(whole);
                if (token.Any(char.IsAsciiDigit) && token.All(c => char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')
                    && double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return JsonValue.Create(number);
                }
            }
            catch (Exception problem) when (problem is ArgumentException or OverflowException)
            {
                throw Fault($"`{token}` is not a number");
            }

            throw Fault($"`{token}` is not a value this reader takes");
        }

        private JsonArray Array()
        {
            _at++;
            var array = new JsonArray();
            while (true)
            {
                Skip(newlines: true);
                if (At("]")) break;
                array.Add(Value());
                Skip(newlines: true);
                if (At(",")) _at++;
                else if (!At("]")) throw Fault("an array's items are separated by commas");
            }

            _at++;
            _closed.Add(array);
            return array;
        }

        private JsonObject Inline()
        {
            _at++;
            var table = new JsonObject();
            while (true)
            {
                Skip(newlines: true);
                if (At("}")) break;
                var path = Key();
                Expect("=");
                Put(table, path, Value());
                Skip(newlines: true);
                if (At(",")) _at++;
                else if (!At("}")) throw Fault("an inline table's entries are separated by commas");
            }

            _at++;
            _closed.Add(table);
            return table;
        }

        private string Text()
        {
            var quote = text[_at];
            var many = At(new string(quote, 3));
            _at += many ? 3 : 1;
            if (many && At("\r\n")) _at += 2;
            else if (many && At("\n")) _at++;
            var built = new StringBuilder();
            while (true)
            {
                if (_at >= text.Length) throw Fault("a string is not closed");
                var c = text[_at];
                if (c == quote && (!many || At(new string(quote, 3))))
                {
                    if (!many)
                    {
                        _at++;
                        return built.ToString();
                    }

                    var run = 0;
                    while (_at + run < text.Length && text[_at + run] == quote && run < 5) run++;
                    built.Append(quote, run - 3);
                    _at += run;
                    return built.ToString();
                }

                if (c is '\r' or '\n' && !many) throw Fault("a string ends at its line");
                if (c == '\\' && quote == '"')
                {
                    Escape(built, many);
                    continue;
                }

                built.Append(c);
                _at++;
            }
        }

        private void Escape(StringBuilder built, bool many)
        {
            _at++;
            if (_at >= text.Length) throw Fault("an escape is not finished");
            var c = text[_at++];
            switch (c)
            {
                case 'b': built.Append('\b'); return;
                case 't': built.Append('\t'); return;
                case 'n': built.Append('\n'); return;
                case 'f': built.Append('\f'); return;
                case 'r': built.Append('\r'); return;
                case 'e': built.Append('\u001b'); return;
                case '"': built.Append('"'); return;
                case '\\': built.Append('\\'); return;
                case 'u' or 'U':
                    var length = c == 'u' ? 4 : 8;
                    if (_at + length > text.Length || !int.TryParse(text.AsSpan(_at, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                    {
                        throw Fault("a unicode escape is not finished");
                    }

                    built.Append(char.ConvertFromUtf32(code));
                    _at += length;
                    return;
            }

            if (!many || c is not (' ' or '\t' or '\r' or '\n')) throw Fault($"`\\{c}` is not an escape");
            // A line-ending backslash drops the whitespace and the line ends after it.
            _at--;
            while (_at < text.Length && text[_at] is ' ' or '\t' or '\r' or '\n') _at++;
        }

        private void Skip(bool newlines)
        {
            while (_at < text.Length)
            {
                var c = text[_at];
                if (c is ' ' or '\t' || newlines && c is '\r' or '\n') _at++;
                else if (c == '#' && newlines) while (_at < text.Length && text[_at] != '\n') _at++;
                else return;
            }
        }

        private void EndOfLine()
        {
            Skip(newlines: false);
            if (At("#")) while (_at < text.Length && text[_at] != '\n') _at++;
            if (At("\r\n")) _at += 2;
            else if (At("\n")) _at++;
            else if (_at < text.Length) throw Fault("a line holds more than one entry");
        }

        private void Expect(string what)
        {
            Skip(newlines: false);
            if (!At(what)) throw Fault($"`{what}` was expected");
            _at += what.Length;
        }

        private bool At(string what) => string.CompareOrdinal(text, _at, what, 0, what.Length) == 0;

        private FormatException Fault(string why) =>
            new($"{why} (line {text.AsSpan(0, Math.Min(_at, text.Length)).Count('\n') + 1}).");
    }
}
