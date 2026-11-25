import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// A file that mounts the whole window (or reads the whole app afresh per case) takes 1 to 5 s a case alone and ten times that on a machine
// running other gates, which the config's ten seconds ends. Each says the budget its cases need, as the config asks of a test
// that needs longer, on the `describe` that holds them all.

const FILES = ['pageRoot.test.tsx', 'zoomed.test.tsx', 'pushedState.test.tsx'];
const WHOLE_APP_MS = 30_000;

describe('the files that mount the whole app', () => {
  it.each(FILES)('%s declares its cases a budget of at least 30 s on its top describe', (file) => {
    const text = readFileSync(join(__dirname, file), 'utf8');
    const declared = /^describe\([^\n]*\{\s*timeout:\s*([\d_]+)\s*\}/m.exec(text);
    expect(declared, `${file} has a top describe with { timeout }`).not.toBeNull();
    expect(Number(declared![1]!.replaceAll('_', ''))).toBeGreaterThanOrEqual(WHOLE_APP_MS);
  });
});
