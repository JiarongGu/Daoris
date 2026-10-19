/**
 * JSON read with what its text said (FABRIC-GRAPH1c, FABRIC-GRAPH1f; D173's GRAPH1b choice 3): a graph's option value enters its digest
 * as the driver's compact writer writes the value as read, its numbers by their own text and its object's keys in the order written,
 * which `JSON.parse` loses (`1.0` reads as `1`, and a key that is a number moves first). The driver reads its files through
 * `JsonDocument`, which keeps both, and judges a whole number by its text (`1.0` is none). This reader gives the values `JSON.parse`
 * gives, each object and array holding, out of sight, the keys as written and each number's own text, and `stringifyWithSource`
 * writes them back so, so a workflow file this build writes over keeps every value the driver would read as it was.
 *
 * The record rides a symbol that is not enumerable: `JSON.stringify`, a spread and `structuredClone` leave it behind, and a value made
 * since holds none, so it is written as JavaScript writes it.
 */

/** What a parsed object's or array's text said: an object's keys in the order written, a key written twice, and each number's own text. */
export interface JsonSource {
  keys: string[] | null;
  twice: boolean;
  numbers: Map<string, string>;
}

const SOURCE: unique symbol = Symbol('json source');

/** What the text said of `holder`, where it was read by `parseWithSource`; undefined for a value made otherwise. */
export function sourceOf(holder: unknown): JsonSource | undefined {
  return typeof holder === 'object' && holder !== null ? (holder as { [SOURCE]?: JsonSource })[SOURCE] : undefined;
}

/** A number member's own text as written, where `holder` was read from text and the member is still that number. */
export function numberText(holder: unknown, key: string | number): string | undefined {
  const text = sourceOf(holder)?.numbers.get(String(key));
  const value = (holder as Record<string | number, unknown>)[key];
  return text !== undefined && typeof value === 'number' && Object.is(Number(text), value) ? text : undefined;
}

class Reader {
  at = 0;
  readonly text: string;

  constructor(text: string) {
    this.text = text;
  }

  fail(what: string): never {
    throw new SyntaxError(`${what} in JSON at position ${this.at}`);
  }

  space(): void {
    while (this.at < this.text.length && ' \t\n\r'.includes(this.text[this.at]!)) this.at += 1;
  }

  value(): unknown {
    this.space();
    const ch = this.text[this.at];
    if (ch === '{') return this.object();
    if (ch === '[') return this.array();
    if (ch === '"') return this.string();
    for (const [word, value] of [['true', true], ['false', false], ['null', null]] as const) {
      if (this.text.startsWith(word, this.at)) {
        this.at += word.length;
        return value;
      }
    }
    return Number(this.number());
  }

  number(): string {
    const match = /-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/y;
    match.lastIndex = this.at;
    const found = match.exec(this.text);
    if (found === null) this.fail(this.at >= this.text.length ? 'Unexpected end of JSON input' : `Unexpected token '${this.text[this.at]}'`);
    this.at += found[0].length;
    return found[0];
  }

  string(): string {
    this.at += 1;
    let out = '';
    for (;;) {
      if (this.at >= this.text.length) this.fail('Unterminated string');
      const ch = this.text[this.at]!;
      if (ch === '"') {
        this.at += 1;
        return out;
      }
      if (ch < ' ') this.fail('Bad control character in string literal');
      if (ch !== '\\') {
        out += ch;
        this.at += 1;
        continue;
      }
      const escape = this.text[this.at + 1];
      const simple: Record<string, string> = { '"': '"', '\\': '\\', '/': '/', b: '\b', f: '\f', n: '\n', r: '\r', t: '\t' };
      if (escape !== undefined && escape in simple) {
        out += simple[escape];
        this.at += 2;
      } else if (escape === 'u' && /^[0-9A-Fa-f]{4}$/.test(this.text.slice(this.at + 2, this.at + 6))) {
        out += String.fromCharCode(parseInt(this.text.slice(this.at + 2, this.at + 6), 16));
        this.at += 6;
      } else {
        this.fail('Bad escaped character');
      }
    }
  }

  array(): unknown[] {
    this.at += 1;
    const list: unknown[] = [];
    const source: JsonSource = { keys: null, twice: false, numbers: new Map() };
    this.space();
    if (this.text[this.at] === ']') {
      this.at += 1;
      return hold(list, source);
    }
    for (;;) {
      this.space();
      const start = this.at;
      const value = this.value();
      if (typeof value === 'number') source.numbers.set(String(list.length), this.text.slice(start, this.at));
      list.push(value);
      this.space();
      const next = this.text[this.at];
      this.at += 1;
      if (next === ']') return hold(list, source);
      if (next !== ',') this.fail(next === undefined ? 'Unexpected end of JSON input' : `Unexpected token '${next}'`);
    }
  }

  object(): Record<string, unknown> {
    this.at += 1;
    const object: Record<string, unknown> = {};
    const source: JsonSource = { keys: [], twice: false, numbers: new Map() };
    this.space();
    if (this.text[this.at] === '}') {
      this.at += 1;
      return hold(object, source);
    }
    for (;;) {
      this.space();
      if (this.text[this.at] !== '"') this.fail('Expected property name');
      const key = this.string();
      this.space();
      if (this.text[this.at] !== ':') this.fail("Expected ':' after property name");
      this.at += 1;
      this.space();
      const start = this.at;
      const value = this.value();
      if (Object.hasOwn(object, key)) source.twice = true;
      else source.keys!.push(key);
      if (typeof value === 'number') source.numbers.set(key, this.text.slice(start, this.at));
      else source.numbers.delete(key);
      // As `JSON.parse` makes it: an own property whatever its name, `__proto__` included, the last written its value.
      Object.defineProperty(object, key, { value, writable: true, enumerable: true, configurable: true });
      this.space();
      const next = this.text[this.at];
      this.at += 1;
      if (next === '}') return hold(object, source);
      if (next !== ',') this.fail(next === undefined ? 'Unexpected end of JSON input' : `Unexpected token '${next}'`);
    }
  }
}

function hold<T extends object>(value: T, source: JsonSource): T {
  Object.defineProperty(value, SOURCE, { value: source, enumerable: false });
  return value;
}

/** `text` parsed to the values `JSON.parse` gives, each object and array holding what its text said; throws `SyntaxError` as it does. */
export function parseWithSource(text: string): unknown {
  const reader = new Reader(text);
  const value = reader.value();
  reader.space();
  if (reader.at < text.length) reader.fail(`Unexpected non-whitespace character after JSON`);
  return value;
}

/**
 * `value` as `JSON.stringify(value, null, 2)` writes it, except where it was read by `parseWithSource`: an object's keys in the order
 * written, then any added since, and a number still the value read by its own text.
 */
export function stringifyWithSource(value: unknown): string {
  const write = (each: unknown, indent: string, holder: unknown, key: string | number): string | undefined => {
    const own = holder === undefined ? undefined : numberText(holder, key);
    if (own !== undefined) return own;
    if (each === null || typeof each !== 'object') return JSON.stringify(each);
    if (typeof (each as { toJSON?: unknown }).toJSON === 'function') return JSON.stringify(each);
    const inner = `${indent}  `;
    if (Array.isArray(each)) {
      if (each.length === 0) return '[]';
      return `[\n${each.map((item, index) => `${inner}${write(item, inner, each, index) ?? 'null'}`).join(',\n')}\n${indent}]`;
    }
    const object = each as Record<string, unknown>;
    const written = sourceOf(object)?.keys?.filter((name) => Object.hasOwn(object, name)) ?? [];
    const keys = [...written, ...Object.keys(object).filter((name) => !written.includes(name))];
    const members = keys.flatMap((name) => {
      const text = write(object[name], inner, object, name);
      return text === undefined ? [] : [`${inner}${JSON.stringify(name)}: ${text}`];
    });
    return members.length === 0 ? '{}' : `{\n${members.join(',\n')}\n${indent}}`;
  };
  return write(value, '', undefined, '') ?? 'undefined';
}
