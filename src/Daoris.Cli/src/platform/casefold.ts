// A name compared without case, as the driver compares it (CASEFOLD1; AGENTREAD1c, D125's notes). Wherever a CLI twin of a
// driver file compares a name without case (an agent, an account, a workspace, a repository, a quest's or an ask's id, a
// plugin's), the driver compares it with .NET's `OrdinalIgnoreCase`: each code point to its one capital, never a wider one.
// JavaScript's `toUpperCase` and `toLowerCase` map a whole string in full, so they found entries the driver does not:
// `straße` met `STRASSE` and `ışık` met `IŞIK` through `toUpperCase`, and `İ` lowered to two letters, meeting an `i` with a
// dot above, through `toLowerCase`. Every such comparison goes through here, so the two doors find the same entry.
//
//   - A letter whose capital is two or more (`ß`, `ŉ`, `ﬁ`) keeps itself, so `straße` is not `STRASSE`.
//   - The two letters beyond ASCII whose capital is ASCII, `ı` and `ſ`, keep themselves, so a dotless i is not an I.
//   - The 27 Greek small letters with a subscript iota take their one-letter capital (`ᾳ` is `ᾼ`): full mapping makes both
//     `ΑΙ`, and a bare *keeps itself* would part them.
//
// `test/fixtures/name-case.json` holds .NET's answers, measured under ICU and in invariant mode, and `casefold.test.ts` holds
// this module to it. A letter given case in Unicode 16 or 17 compares by each runtime's own data (D125's AGENTREAD1c note).

/**
 * A name folded as the driver's `OrdinalIgnoreCase` compares it: two names are one exactly where their folds are equal, and
 * order as their folds do. A fold keeps the name's length in code points.
 */
export function foldName(name: string): string {
  return Array.from(name, (letter) => {
    const point = letter.codePointAt(0)!;
    const iota = iotaCapital(point);
    if (iota !== null) return String.fromCodePoint(iota);
    const upper = letter.toUpperCase();
    // A capital of two or more letters (ß, ŉ, ﬁ) is none: the letter keeps itself.
    if (Array.from(upper).length !== 1) return letter;
    // .NET keeps the two letters beyond ASCII whose capital is ASCII, ı and ſ, as themselves, as a JavaScript pattern's
    // `i` flag without `u` does, so a dotless i is not an I.
    return point > 0x7f && upper.codePointAt(0)! <= 0x7f ? letter : upper;
  }).join('');
}

/** Whether two names are one, as the driver's `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` finds them. */
export function sameName(a: string, b: string): boolean {
  return a === b || foldName(a) === foldName(b);
}

/** The first of `names` equal to `name` without case, as written, as the driver finds a key; null where there is none. */
export function findName(names: Iterable<string>, name: string): string | null {
  const wanted = foldName(name);
  for (const each of names) if (foldName(each) === wanted) return each;
  return null;
}

/**
 * Entries held as a driver dictionary that ignores case holds what is set into it, one by one (`map[name] = value`): each
 * name once, under its spelling first set, holding the value set last (CASEFOLD1c; `HarnessSettings`' maps are read so).
 */
export function byName<T>(entries: Iterable<readonly [string, T]>): Record<string, T> {
  const held: [string, T][] = [];
  for (const [name, value] of entries) {
    const at = held.findIndex(([each]) => sameName(each, name));
    if (at === -1) held.push([name, value]);
    else held[at] = [held[at]![0], value];
  }
  return Object.fromEntries(held);
}

/** What a map keyed by name holds under `name` in any case, as a driver dictionary that ignores case finds it; undefined for none. */
export function atName<T>(map: Record<string, T>, name: string): T | undefined {
  const key = findName(Object.keys(map), name);
  return key === null ? undefined : map[key];
}

/**
 * How two names order without case, as the driver's `StringComparer.OrdinalIgnoreCase` orders them: by their folds, a UTF-16
 * unit at a time, as .NET compares each capital.
 */
export function compareNames(a: string, b: string): number {
  const x = foldName(a);
  const y = foldName(b);
  return x < y ? -1 : x > y ? 1 : 0;
}

/**
 * A Greek small letter with a subscript iota, whose capital is two letters in full (`ᾳ` is `ΑΙ`) and one in the simple
 * mapping .NET compares by (`ᾳ` is `ᾼ`, the capital with the iota beside it): that one; null for any other.
 */
function iotaCapital(point: number): number | null {
  const row = point & ~0x7;
  if (row === 0x1f80 || row === 0x1f90 || row === 0x1fa0) return point + 8;
  return point === 0x1fb3 || point === 0x1fc3 || point === 0x1ff3 ? point + 9 : null;
}
