import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The page reads each pushed slice from its snapshot alone, so no source speaks of an older shell, host, build or page: such a
 * sentence describes a split that no longer exists. This census pins zero matches across the web's sources.
 */
const PATTERN = /older (shell|host|build|page)|(shell|host) older/;
const root = join(process.cwd(), 'src');

function sources(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === 'generated' ? [] : sources(path);
    return /\.(ts|tsx|json|css)$/.test(entry.name) ? [path] : [];
  });
}

describe('the older-shell census', () => {
  it('finds no mention of an older shell, host, build or page in the web sources', () => {
    const hits = sources(root)
      .filter((path) => !path.endsWith('olderShellCensus.test.ts'))
      .flatMap((path) => readFileSync(path, 'utf8').split('\n')
        .map((line, index) => (PATTERN.test(line) ? `${path}:${index + 1}` : ''))
        .filter(Boolean));
    expect(hits).toEqual([]);
  });
});
