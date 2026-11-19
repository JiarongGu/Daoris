/**
 * The token codemod (UIDS1, D330): the web sources move from the old tokens' names to the role set's, by whole class.
 *
 *   node --test tools/token-codemod.test.mjs
 *
 * `moveSource` is held on the shapes a class takes (a variant, an opacity, a side, a CSS variable, a word on a solid accent),
 * on what it must leave (a mark's hue, a word that holds a name, the aliases), and on running twice; `moveTree` on the real
 * tree, which is already moved, so a run there changes nothing.
 */
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { aliasSites, moveSource, moveTree } from './token-codemod.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const moved = (text, path = 'src/x.tsx') => moveSource(text, path).text;

test('a status colour moves to its role, with its variant, side and opacity', () => {
  assert.equal(moved('"text-ink-danger hover:text-ink-danger border-st-declined"'), '"text-fail-ink hover:text-fail-ink border-fail"');
  assert.equal(moved("'text-ink-done bg-st-done border-l-st-done'"), "'text-ok-ink bg-ok border-l-ok'");
  assert.equal(moved("'text-ink-taken stroke-st-taken border-st-taken/40'"), "'text-live-ink stroke-live border-live/40'");
  assert.equal(moved("'data-[state=on]:ring-st-declined [&>svg]:fill-st-done'"), "'data-[state=on]:ring-fail [&>svg]:fill-ok'");
});

test("a hue's light field becomes its soft token", () => {
  assert.equal(moved("'bg-st-declined/10 bg-st-done/20 hover:bg-st-taken/10'"), "'bg-fail-soft bg-ok-soft hover:bg-live-soft'");
  assert.equal(moved("'bg-accent/10'"), "'bg-accent-soft'");
  // A heavier wash is a fill at an opacity, and keeps it.
  assert.equal(moved("'bg-st-done/40'"), "'bg-ok/40'");
});

test('a word in the accent wears the accent ink, and a label on the solid accent wears on-accent', () => {
  assert.equal(moved("'text-accent hover:text-accent underline decoration-accent'"), "'text-accent-ink hover:text-accent-ink underline decoration-accent'");
  assert.equal(moved("'border border-accent bg-accent text-accent-ink'"), "'border border-accent bg-accent text-on-accent'");
  assert.equal(moved("'data-[state=checked]:bg-accent data-[state=checked]:text-accent-ink'"),
    "'data-[state=checked]:bg-accent data-[state=checked]:text-on-accent'");
  // A soft or faded accent is no solid fill: its word is a link's.
  assert.equal(moved("'bg-accent-soft text-accent'"), "'bg-accent-soft text-accent-ink'");
  // A mark keeps the accent itself.
  assert.equal(moved("'border-accent outline-accent stroke-accent fill-accent ring-accent/60'"), "'border-accent outline-accent stroke-accent fill-accent ring-accent/60'");
});

test('a type step moves to its new name, and the serif goes', () => {
  assert.equal(moved('"text-meta text-title text-view text-wordmark text-value text-small text-body"'),
    '"text-caption text-heading text-display text-display text-display text-small text-body"');
  assert.equal(moved("container.querySelector('.text-meta')"), "container.querySelector('.text-caption')");
  assert.equal(moved('"font-serif text-wordmark"'), '"font-sans text-display"');
  // A selector asks whether the serif is drawn, and keeps asking it.
  assert.equal(moved("container.querySelectorAll('.font-serif')"), "container.querySelectorAll('.font-serif')");
});

test('a variable moves too, in a stylesheet and in a class', () => {
  assert.equal(moved('  color: var(--ink-danger);\n  color: var(--accent);\n  background: var(--st-done);', 'src/a.css'),
    '  color: var(--fail-ink);\n  color: var(--accent-ink);\n  background: var(--ok);');
  assert.equal(moved("'duration-(--speed) duration-[var(--speed)] animate-[drawer-in_var(--speed)_ease-out]'"),
    "'duration-(--duration-quick) duration-[var(--duration-quick)] animate-[drawer-in_var(--duration-quick)_ease-out]'");
  // A border in the accent is a mark, in a stylesheet as in a class.
  assert.equal(moved('  border-color: var(--accent);', 'src/a.css'), '  border-color: var(--accent);');
});

test("a token read by its bare name moves, as the terminal's theme reads them", () => {
  assert.equal(moved("const red = token('st-declined'); between(\"st-taken\", `st-done`); { 'ink-danger': '#9e2f24' }"),
    "const red = token('fail'); between(\"live\", `ok`); { 'fail-ink': '#9e2f24' }");
  assert.equal(moved("style.getPropertyValue('--st-done')"), "style.getPropertyValue('--ok')");
  // An alias keeps its name, and a longer string is no name.
  assert.equal(moved("token('st-open') 'warn' 'st-done later'"), "token('st-open') 'warn' 'st-done later'");
});

test('what holds a name is left: a longer class, a word, a key, and the aliases', () => {
  const kept = [
    "'context-text-meta text-meta-x data-text-title text-st-donex bg-st-done-y'",
    "'border-l-st-open text-ink-open text-warn bg-warn/10 fill-st-open var(--warn)'",
    "'text-ink text-ink-soft text-ink-faint bg-raised'",
    "t('quest.declined') kind: 'request-declined'",
  ];
  for (const text of kept) assert.equal(moved(text), text, text);
});

test('the declaration home is left whole', () => {
  const css = ':root {\n  --ink-danger: #9e2f24;\n}\n--color-st-done: var(--st-done);';
  assert.equal(moved(css, 'src/tokens.css'), css);
});

test('a second run changes nothing', () => {
  const samples = [
    "'border border-accent bg-accent text-accent-ink hover:text-accent text-ink-danger bg-st-done/10 text-meta'",
    "'bg-accent text-accent'",
    '  color: var(--accent);\n  color: var(--ink-taken);',
    "'font-serif text-value duration-(--speed)'",
  ];
  for (const text of samples) {
    const once = moved(text, text.includes('color:') ? 'src/a.css' : 'src/x.tsx');
    assert.equal(moved(once, text.includes('color:') ? 'src/a.css' : 'src/x.tsx'), once, text);
  }
});

test("the aliases' sites are listed, not moved", () => {
  assert.deepEqual(aliasSites("'border-l-st-open text-ink-open hover:text-warn bg-warn/10' var(--st-open) text-warning"),
    ['border-l-st-open', 'text-ink-open', 'text-warn', 'bg-warn/10', 'var(--st-open)']);
});

test('the tree is moved: a run over the web sources changes nothing', () => {
  const run = moveTree(root, { write: false });
  assert.ok(run.read > 500, `read ${run.read} files`);
  assert.deepEqual(run.changed, []);
});
