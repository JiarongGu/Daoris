/**
 * An edit written over the file it was read from (FABRIC-GRAPH1's review, findings 1 and 5; FIX-LOG 2026-10-17), the driver's
 * `JsonKeeping`. A writer that rebuilt a home file from the fields it knows deleted every setting it did not, and one that parsed through
 * JavaScript's numbers wrote `9007199254740993` back as `9007199254740992`. Three values make the edit instead: the file's text as it
 * stands, the file as its reader understood it, and the edited choices, the last two built by the same writer. What the edit did not change
 * is written as the file has it, every number by its own text; what it changed is written as the edit has it, an object the three hold alike
 * merged key by key; a key the edit adds goes after the one before it in the writer's order. Both twins are held to
 * `test/fixtures/driver-config-keeps.json`.
 *
 * A name JavaScript reads as an array index (`"2048"`) is laid out before the other names of its object when an edit rewrites the file:
 * a plain object keeps such keys in number order, so the driver's twin, which keeps every key in place, is the exact one there.
 */

type Raw = { readonly rawJSON: string };
const rawJSON = (JSON as unknown as { rawJSON?: (text: string) => Raw }).rawJSON;
const isRawJSON = (JSON as unknown as { isRawJSON?: (value: unknown) => boolean }).isRawJSON;

/** `text` parsed with every number kept as its own text, where this Node can (`JSON.rawJSON`); else as JavaScript reads numbers. */
export function parseKeepingNumbers(text: string): unknown {
  if (rawJSON === undefined) return JSON.parse(text);
  return JSON.parse(text, (_key: string, value: unknown, context?: { source?: string }) =>
    typeof value === 'number' && context?.source !== undefined ? rawJSON(context.source) : value);
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value) && !(isRawJSON?.(value) ?? false);
}

/**
 * `edited` written over `original`, where `read` is `original` as its reader understood it, written by the same writer as `edited`: the
 * file's text, two spaces deep, LF, one final newline. No edit is `original`'s own text.
 */
export function keepEdits(original: string, read: unknown, edited: unknown): string {
  const readText = JSON.stringify(read);
  const editedText = JSON.stringify(edited);
  if (readText === editedText) return original;
  const file = parseKeepingNumbers(original);
  const understood: unknown = JSON.parse(readText);
  const now: unknown = JSON.parse(editedText);
  const kept = isObject(file) && isObject(understood) && isObject(now) ? plan(file, understood, now, true) : now;
  return `${JSON.stringify(kept, null, 2)}\n`;
}

const same = (a: unknown, b: unknown): boolean => JSON.stringify(a) === JSON.stringify(b);

/**
 * What one object becomes, key by key in the file's order: a key neither its reader nor the edit holds is the file's, kept; one the edit
 * took goes, except that a map emptied at the top keeps the entries its reader never read; one its reader did not read and the edit set is
 * the edit's; one the edit left as read is the file's, as written; an object the edit changed is merged; else the edit's.
 */
function plan(file: Record<string, unknown>, read: Record<string, unknown>, edited: Record<string, unknown>, top: boolean): Record<string, unknown> {
  const out: [string, unknown][] = [];
  for (const [name, kept] of Object.entries(file)) {
    const wasRead = Object.hasOwn(read, name);
    const isEdited = Object.hasOwn(edited, name);
    const was = read[name];
    const now = edited[name];
    if (!wasRead && !isEdited) out.push([name, kept]);
    else if (!isEdited) {
      if (top && isObject(kept) && isObject(was)) {
        const unread = plan(kept, was, {}, false);
        if (Object.keys(unread).length > 0) out.push([name, unread]);
      }
    } else if (!wasRead) out.push([name, now]);
    else if (same(was, now)) out.push([name, kept]);
    else if (isObject(kept) && isObject(was) && isObject(now)) out.push([name, plan(kept, was, now, false)]);
    else out.push([name, now]);
  }

  // A key the edit adds goes after the edit's key before it, or first; a default the file never wrote stays unwritten.
  let at = 0;
  for (const [name, now] of Object.entries(edited)) {
    const index = out.findIndex(([each]) => each === name);
    if (index >= 0) {
      at = index + 1;
      continue;
    }
    if (Object.hasOwn(file, name)) continue;
    if (Object.hasOwn(read, name) && same(read[name], now)) continue;
    out.splice(at++, 0, [name, now]);
  }

  return Object.fromEntries(out);
}
