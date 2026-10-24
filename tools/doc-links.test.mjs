import assert from 'node:assert/strict';
import { test } from 'node:test';
import { anchorsOf, auditLinks, headingSlug, shippedReadmeFindings } from './doc-links.mjs';

const GH = 'https://github.com/JiarongGu/Daoris/blob/main';
const RAW = 'https://raw.githubusercontent.com/JiarongGu/Daoris/main';
const RULES = ['Edit the canon, not the copy.', 'Run `daoris check` before committing.'];
const block = (rules) => `<!-- agent-rules:begin -->\n${rules.map((r) => `- ${r}`).join('\n')}\n<!-- agent-rules:end -->\n`;

const base = {
  'README.md': `# ![logo](${RAW}/docs/assets/logo.svg) Product\n\nRead [the guide](${GH}/docs/guide.md#the-manifest).\n\n## Rules\n\n${block(RULES)}`,
  'llms.txt': `# Product\n\n${block(RULES)}\n- [Guide](${RAW}/docs/guide.md)\n`,
  'context7.json': JSON.stringify({ rules: RULES }),
  'docs/guide.md': '# Guide\n\n## The manifest\n\n## `sync --force`\n\nBack to [the router](README.md#guides).\n',
  'docs/README.md': '# Router\n\n## Guides\n\n| [Guide](guide.md) | guide |\n',
  'docs/assets/logo.svg': '<svg/>',
  'src/Daoris.Cli/package.json': '{}',
};

const audit = (extra = {}, removed = []) => {
  const contents = { ...base, ...extra };
  for (const file of removed) delete contents[file];
  return auditLinks(Object.keys(contents), (file) => {
    if (!(file in contents)) throw new Error(`missing ${file}`);
    return contents[file];
  });
};
const fails = (extra, ...words) => {
  const { findings } = audit(extra);
  assert.ok(findings.some((f) => words.every((w) => f.includes(w))), `expected a finding with ${words.join(' + ')}; got:\n${findings.join('\n')}`);
};

test('the fixture is clean, absolute, relative and anchored links alike', () => {
  const { findings, counts } = audit();
  assert.deepEqual(findings, []);
  assert.ok(counts.absolute >= 3 && counts.relative >= 2 && counts.anchors >= 2, JSON.stringify(counts));
});

test('a broken link fails, absolute or relative', () => {
  fails({ 'docs/guide.md': '# Guide\n\nSee [gone](missing.md).\n' }, 'docs/guide.md:3', 'docs/missing.md', 'not in a clone');
  fails({ 'llms.txt': `${base['llms.txt']}- [Gone](${RAW}/docs/gone.md)\n` }, 'llms.txt', 'docs/gone.md');
});

test('a link in the wrong letter case fails, as GitHub would refuse it on a case-blind disk', () => {
  fails({ 'docs/README.md': '# Router\n\n| [Guide](Guide.md) | guide |\n' }, 'docs/Guide.md', 'letter case');
});

test('an anchor is resolved by GitHub\'s slug rule', () => {
  fails({ 'README.md': base['README.md'].replace('#the-manifest', '#the-manifesto') }, 'has no heading "#the-manifesto"');
  fails({ 'docs/guide.md': base['docs/guide.md'].replace('#guides', '#guide') }, 'docs/README.md', '#guide"');
  assert.deepEqual(audit({ 'docs/README.md': `${base['docs/README.md']}| [Force](guide.md#sync---force) | x |\n` }).findings, []);
});

test('an absolute link names main and a raw link names a file; a pinned or line link is history', () => {
  fails({ 'llms.txt': `${base['llms.txt']}- [x](https://github.com/JiarongGu/Daoris/blob/dev/docs/guide.md)\n` }, 'branch "dev"');
  fails({ 'llms.txt': `${base['llms.txt']}- [x](${RAW}/docs)\n` }, 'serves files only');
  assert.deepEqual(audit({ 'llms.txt': `${base['llms.txt']}- [a](https://github.com/JiarongGu/Daoris/blob/8cc3936b0/old.md) [b](${GH}/docs/guide.md#L3) [c](https://github.com/JiarongGu/Daoris/tree/main/docs)\n` }).findings, []);
});

test('a link to private context or inside code is not a link a clone must hold', () => {
  assert.deepEqual(audit({ 'docs/guide.md': '# Guide\n\n## The manifest\n\n[notes](../local/notes.md) `[x](nope.md)`\n\n```md\n[y](nope.md)\n```\n' }).findings, []);
});

test('the shipped README fails on a relative link, raw HTML and a commented sample', () => {
  fails({ 'README.md': `${base['README.md']}\nSee [the router](docs/README.md).\n` }, 'README.md:', 'relatively');
  fails({ 'README.md': `<p align="center">x</p>\n\n${base['README.md']}` }, 'README.md:1', 'raw HTML');
  fails({ 'README.md': `${base['README.md']}\n\`\`\`json\n<!-- a note -->\n{}\n\`\`\`\n` }, 'comment inside a code block');
  // The agent-rules markers are comments outside a fence, hidden by GitHub and npm alike; an in-page anchor follows.
  assert.deepEqual(shippedReadmeFindings(`${base['README.md']}\n[Rules](#rules)\n`), []);
});

test('an unequal copy of the agent rules fails, in either agent file', () => {
  fails({ 'llms.txt': base['llms.txt'].replace('the copy', 'a copy') }, 'llms.txt', 'differ', 'rule 1');
  fails({ 'context7.json': JSON.stringify({ rules: RULES.slice(0, 1) }) }, 'context7.json', 'rule 2');
  fails({ 'context7.json': '{"rules": ' }, 'context7.json', 'not valid JSON');
});

test('a missing copy, a wrapped rule and a rule over Context7\'s limit fail', () => {
  assert.ok(audit({}, ['llms.txt']).findings.some((f) => f.includes('llms.txt: missing')));
  fails({ 'README.md': base['README.md'].replace('the copy.', 'the\n  copy.') }, 'not a "- " rule');
  const long = `A rule ${'x'.repeat(250)}`;
  const rules = [...RULES, long];
  fails({ 'README.md': base['README.md'].replace(block(RULES), block(rules)), 'llms.txt': block(rules), 'context7.json': JSON.stringify({ rules }) },
    'agent rule 3', `${long.length} characters`);
});

test('the slug rule matches GitHub on code spans, punctuation, emphasis, CJK and repeats', () => {
  assert.equal(headingSlug('`sync --force`'), 'sync---force');
  assert.equal(headingSlug('Daoris (道衍)'), 'daoris-道衍');
  assert.equal(headingSlug('The dev loop — `tools/desktop.mjs` (2026-09-21)'), 'the-dev-loop--toolsdesktopmjs-2026-09-21');
  assert.equal(headingSlug('**What** is _in_ the box?'), 'what-is-in-the-box');
  assert.equal(headingSlug(`![logo](${RAW}/x.svg) Daoris`), 'daoris');
  assert.deepEqual([...anchorsOf('# A\n\n## A\n\n```\n# not\n```\nB\n---\n')], ['a', 'a-1', 'b']);
});

test('the README\'s logo and the package\'s icon draw the page\'s seal, as the file says (a twin)', async () => {
  const { readFileSync } = await import('node:fs');
  const at = (rel) => new URL(`../${rel}`, import.meta.url);
  const drawing = (rel) => readFileSync(at(rel), 'utf8').replace(/<!--[\s\S]*?-->/g, '').match(/<rect[\s\S]*<\/mask>/)?.[0].replace(/\s+/g, ' ');
  const seal = drawing('src/Daoris.Web/public/icon.svg');
  assert.ok(seal, 'the page\'s icon no longer has the seal\'s shape: read it again');
  for (const rel of ['docs/assets/daoris-logo.svg', 'docs/assets/daoris-icon.svg']) assert.equal(drawing(rel), seal, rel);
  assert.match(readFileSync(at('docs/assets/daoris-logo.svg'), 'utf8'), /width="36" height="36"/);
});
