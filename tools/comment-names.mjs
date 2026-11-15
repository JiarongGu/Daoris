/**
 * The row ids the comment gate refuses, in the two places a test names itself: a C# test method's name (a `[Fact]` or `[Theory]`
 * method, or its `DisplayName`) and the string of a `test`, `it` or `describe` call. A name is added where it stands on the new
 * side more often than on the old, so a test that already carries an id keeps it until it is renamed or removed.
 */

/** A backlog row id: capitals then digits, and a trailing letter, alone among letters and digits (an underscore ends it, as a test method's name has them). */
export const ROW = /(?<![A-Za-z0-9])[A-Z]{2,}\d+[a-z]?(?![A-Za-z0-9])/g;
/** A capitals-and-digits tool or standard name, which is no id. */
export const KNOWN = /^(?:UTF|SHA|MD|HTTP|HS|RS|ES|PS|AES|X|ISO|RFC|TLS|SSL|CRC|BASE|GB|PBKDF|SSH|IPV|WIN|MP|H|OS|TRACE)\d+$/;

const JS_CALL = /\b(?:test|it|describe)(?:\.(?:skip|only|todo|concurrent|sequential|fails))*\(\s*(['"`])((?:\\.|(?!\1)[^\\\n])*)\1/g;
const CS_METHOD = /\[\s*(?:\w+\.)?(?:Fact|Theory)\b[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+(?:async\s+)?(?:Task|void)\s+(\w+)/g;
const CS_DISPLAY = /\bDisplayName\s*=\s*"((?:\\.|[^"\\\n])*)"/g;

/** The names of the tests `src` declares, as the text each carries. */
export function testNames(src, path) {
  if (path.replace(/\.template$/, '').endsWith('.cs')) return [...src.matchAll(CS_METHOD)].map((m) => m[1]).concat([...src.matchAll(CS_DISPLAY)].map((m) => m[1]));
  return [...src.matchAll(JS_CALL)].map((m) => m[2]);
}

/** The row ids a test name holds. */
export const rowIdsIn = (name) => [...name.matchAll(ROW)].map((m) => m[0]).filter((id) => !KNOWN.test(id));


/** The `[kind, name]` pairs of the tests `after` names with a row id that `before` (null for a new file) did not already carry as often. */
export function addedRowNames(path, before, after) {
  const seen = new Map();
  for (const name of before === null ? [] : testNames(before, path)) seen.set(name, (seen.get(name) ?? 0) + 1);
  const fresh = [];
  for (const name of testNames(after, path)) {
    const left = seen.get(name) ?? 0;
    if (left > 0) seen.set(name, left - 1);
    else if (rowIdsIn(name).length) fresh.push(name);
  }
  return fresh;
}
