import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// A file that mounts the whole window (or reads the whole app afresh per case) takes 1 to 5 s a case alone (zoomed 0.7 to 1.3, pushedState up to 5, pageRoot's cold import more), which the config's ten seconds ends on a
// machine running other gates. Each says six times the slowest measured alone, as the config asks of a test that needs longer, on the
// `describe` that holds them all (pageMeasure's 650 stories too); pageRoot's cold import says its own.

const FILES = ['pageRoot.test.tsx', 'zoomed.test.tsx', 'pushedState.test.tsx', '../design/pageMeasure.test.tsx'];
const WHOLE_APP_MS = 30_000;

describe('the files that mount the whole app', () => {
  it.each(FILES)('%s declares its cases a budget of at least 30 s on its top describe', (file) => {
    const text = readFileSync(join(__dirname, file), 'utf8');
    const declared = /^describe\([^\n]*\{\s*timeout:\s*([\d_]+)\s*\}/m.exec(text);
    expect(declared, `${file} has a top describe with { timeout }`).not.toBeNull();
    expect(Number(declared![1]!.replaceAll('_', ''))).toBeGreaterThanOrEqual(WHOLE_APP_MS);
  });
});
