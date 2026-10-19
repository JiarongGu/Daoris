import hljs from 'highlight.js/lib/common';

// Code highlighted a line at a time (REVIEW2). A diff draws each line on its own row, and a
// highlighter reads text whole — a comment or a string opened on one line is still open on the next.
// So a side's text is highlighted whole and split back into lines, each one's markup balanced. Pure.

/** A file's extension, and the highlighter's name for its language. Only names it ships are used. */
const EXTENSIONS: Record<string, string> = {
  ts: 'typescript', tsx: 'typescript', mts: 'typescript', cts: 'typescript',
  js: 'javascript', jsx: 'javascript', mjs: 'javascript', cjs: 'javascript',
  cs: 'csharp', py: 'python', rs: 'rust', go: 'go', java: 'java', kt: 'kotlin', kts: 'kotlin',
  swift: 'swift', rb: 'ruby', php: 'php', lua: 'lua', c: 'c', h: 'c', cc: 'cpp', cpp: 'cpp', hpp: 'cpp',
  json: 'json', md: 'markdown', css: 'css', scss: 'scss', sql: 'sql', toml: 'ini', ini: 'ini',
  yml: 'yaml', yaml: 'yaml', sh: 'bash', bash: 'bash',
  html: 'xml', xml: 'xml', svg: 'xml', csproj: 'xml', props: 'xml', targets: 'xml', xaml: 'xml',
};

/** The language a file is written in, by its extension — or null, and then it is shown plain, never guessed. */
export function languageOf(path: string): string | null {
  const name = path.slice(path.lastIndexOf('/') + 1);
  const dot = name.lastIndexOf('.');
  if (dot <= 0) return null;
  const language = EXTENSIONS[name.slice(dot + 1).toLowerCase()];
  return language && hljs.getLanguage(language) ? language : null;
}

const escape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/**
 * Lines as HTML, highlighted together so what spans lines is coloured on each, and escaped where the
 * language is unknown. The highlighter escapes what it reads, so no line's text can become markup.
 */
export function highlightLines(lines: readonly string[], language: string | null | undefined): string[] {
  const text = lines.join('\n');
  const html = language && hljs.getLanguage(language)
    ? hljs.highlight(text, { language, ignoreIllegals: true }).value
    : escape(text);
  return splitHighlighted(html);
}

/**
 * The highlighter's markup cut at each newline, closing whatever is open at a line's end and opening
 * it again at the next line's start — so every line is markup a row can hold on its own.
 */
export function splitHighlighted(html: string): string[] {
  const lines: string[] = [];
  const open: string[] = [];
  let line = '';
  // The highlighter writes spans and escaped text, so a `<` is always a tag.
  for (const [token] of html.matchAll(/<span[^>]*>|<\/span>|\n|[^<\n]+/g)) {
    if (token === '\n') {
      lines.push(line + '</span>'.repeat(open.length));
      line = open.join('');
    } else if (token.startsWith('<span')) {
      open.push(token);
      line += token;
    } else if (token === '</span>') {
      open.pop();
      line += token;
    } else {
      line += token;
    }
  }
  lines.push(line + '</span>'.repeat(open.length));
  return lines;
}
