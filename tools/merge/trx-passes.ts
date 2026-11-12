/**
 * The classes a trx says passed: every test of the class ran and passed. A class with a failure or a skip is not one, so a pass
 * is recorded only for a class the report shows passing.
 */

const unescape = (text: string): string => text.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"');

export function passedClasses(trx: string): Set<string> {
  const classOf = new Map<string, string>();
  for (const [, id, body] of trx.matchAll(/<UnitTest\b[^>]*\bid="([^"]+)"[^>]*>([\s\S]*?)<\/UnitTest>/g)) {
    const name = /<TestMethod\b[^>]*\bclassName="([^"]+)"/.exec(body as string)?.[1];
    if (name) classOf.set(id as string, unescape(name));
  }
  const passed = new Set<string>();
  const spoiled = new Set<string>();
  for (const [tag] of trx.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const className = classOf.get(/\btestId="([^"]+)"/.exec(tag)?.[1] ?? '');
    if (!className) continue;
    if (/\boutcome="Passed"/.test(tag)) passed.add(className);
    else spoiled.add(className);
  }
  return new Set([...passed].filter((name) => !spoiled.has(name)));
}
