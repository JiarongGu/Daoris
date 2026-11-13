/**
 * The test files a run's report says passed, for the runners whose unit is a file (node's and the page's): every test of the file
 * ran and passed, so a pass is recorded for each file seen to pass even where another file of the same command failed.
 */

const unescape = (text: string): string => text.replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');

/** Node's junit report: the files with a test case, none failed, errored or skipped. Paths as the report writes them. */
export function junitPasses(xml: string): Set<string> {
  const seen = new Set<string>();
  const spoiled = new Set<string>();
  for (const [, attributes, body] of xml.matchAll(/<testcase\b([^>]*?)(?:\/>|>([\s\S]*?)<\/testcase>)/g)) {
    const file = /\bfile="([^"]*)"/.exec(attributes as string)?.[1];
    if (!file) continue;
    seen.add(unescape(file));
    if (/<(failure|error|skipped)\b/.test(body ?? '')) spoiled.add(unescape(file));
  }
  return new Set([...seen].filter((file) => !spoiled.has(file)));
}

/** Vitest's json report: each failed test with its file, or the file alone (title empty) where it failed before any test ran. */
export function vitestFailures(json: string): { file: string; test: string }[] {
  try {
    const parsed = JSON.parse(json) as { testResults?: Array<{ name?: string; status?: string; message?: string; assertionResults?: Array<{ status?: string; fullName?: string }> }> };
    return (parsed.testResults ?? []).flatMap((each) => {
      if (each.status !== 'failed' || typeof each.name !== 'string') return [];
      const tests = (each.assertionResults ?? []).filter((one) => one.status === 'failed' && one.fullName);
      return tests.length ? tests.map((one) => ({ file: each.name as string, test: one.fullName as string })) : [{ file: each.name, test: each.message?.split('\n')[0] ?? 'the file did not run' }];
    });
  } catch {
    return [];
  }
}

/** Vitest's json report: the files whose status is passed. Paths as the report writes them. */
export function vitestPasses(json: string): Set<string> {
  try {
    const parsed = JSON.parse(json) as { testResults?: Array<{ name?: string; status?: string }> };
    return new Set((parsed.testResults ?? []).filter((each) => each.status === 'passed' && typeof each.name === 'string').map((each) => each.name as string));
  } catch {
    return new Set();
  }
}
