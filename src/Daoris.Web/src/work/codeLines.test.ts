import { describe, expect, it } from 'vitest';
import { highlightLines, languageOf, splitHighlighted } from './codeLines';

// Code highlighted a line at a time (REVIEW2): a diff draws each line on its own, and a highlighter
// reads text whole — a comment opened on one line is still open on the next. So the text is
// highlighted whole and split back into lines, each one's markup balanced. Pure.

const balanced = (html: string) => (html.match(/<span/g) ?? []).length === (html.match(/<\/span>/g) ?? []).length;

describe('a file\'s language', () => {
  it('is read from its extension, only where the highlighter knows it', () => {
    expect(languageOf('src/world/chunk.rs')).toBe('rust');
    expect(languageOf('src/Daoris.Web/src/App.tsx')).toBe('typescript');
    expect(languageOf('src/Driver/Acp.cs')).toBe('csharp');
    expect(languageOf('docs/DECISIONS.md')).toBe('markdown');
    expect(languageOf('notes.txt')).toBeNull();
    expect(languageOf('LICENSE')).toBeNull();
  });
});

describe('splitting highlighted markup into lines', () => {
  it('closes what is open at each line\'s end and opens it again on the next', () => {
    expect(splitHighlighted('<span class="hljs-comment">/* one\ntwo */</span> x')).toEqual([
      '<span class="hljs-comment">/* one</span>',
      '<span class="hljs-comment">two */</span> x',
    ]);
  });
});

describe('highlighting lines', () => {
  it('colours a comment that runs across lines on every line it covers, each line balanced', () => {
    const lines = highlightLines(['/* the cap', '   per frame */', 'const cap = 4;'], 'typescript');
    expect(lines).toHaveLength(3);
    expect(lines[0]).toContain('hljs-comment');
    expect(lines[1]).toContain('hljs-comment');
    expect(lines[2]).toContain('hljs-keyword');
    expect(lines.every(balanced)).toBe(true);
  });

  it('escapes a line in no known language, and never lets its text become markup', () => {
    expect(highlightLines(['a < b && <img src=x>'], null)).toEqual(['a &lt; b &amp;&amp; &lt;img src=x&gt;']);
    expect(highlightLines(['<img src=x onerror=alert(1)>'], 'typescript')[0]).not.toContain('<img');
  });
});
