import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * **A key an input method is composing with is never the page's** (IME1). Every handler that checks Enter or Escape
 * asks `isComposing` (`lib/composing.ts`) first, so accepting a candidate sends nothing, picks no row and closes
 * nothing. The composer sent a half-written message on the Enter that accepted a candidate, and the mention list, the
 * palette and every search box had the same gap: nothing asked.
 *
 * Held here for the reason `presentational.test.ts` holds what a molecule imports: it breaks one handler at a time and
 * silently, and only for a person typing through an input method, which no other test does.
 *
 * Read with the TypeScript parser rather than a pattern, because the unit is a handler: the function nearest a check
 * of the key must itself ask, and ask before the check. A guard in the function that builds a handler does not guard
 * the handler it returns.
 *
 * @remarks
 * What it reads as a check: a key or code (`event.key`, `event.code`, a `key` or `code` of its own) compared with
 * `'Enter'` or `'Escape'`, either side, and a `case` of either in a switch on one. A table of moves looked up by the key
 * is not seen unless its handler also compares; Radix's own Escape on a dialog is not in this tree at all, and the
 * atoms that open one answer it (`ui.tsx`'s `Drawer` and `QuickPanel`, held by `ui.test.tsx`).
 */

/** The keys an input method takes for itself while composing: accepting a candidate, and dropping the composition. */
const KEYS = new Set(['Enter', 'Escape']);

const pressed = (node: ts.Expression): boolean =>
  (ts.isPropertyAccessExpression(node) && (node.name.text === 'key' || node.name.text === 'code'))
  || (ts.isIdentifier(node) && (node.text === 'key' || node.text === 'code'));

const named = (node: ts.Expression): string | null =>
  ts.isStringLiteralLike(node) && KEYS.has(node.text) ? node.text : null;

const EQUALITY = new Set([
  ts.SyntaxKind.EqualsEqualsEqualsToken, ts.SyntaxKind.ExclamationEqualsEqualsToken,
  ts.SyntaxKind.EqualsEqualsToken, ts.SyntaxKind.ExclamationEqualsToken,
]);

/** The function a node is in, nearest first, or null at a module's top. */
function nearestFunction(node: ts.Node): ts.Node | null {
  for (let at = node.parent; at; at = at.parent) {
    if (ts.isFunctionLike(at)) return at;
  }
  return null;
}

/** Every check of Enter or Escape whose own function does not ask `isComposing` before it, as `path:line key`. */
export function unasked(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) => {
    const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true,
      path.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
    const checks: { node: ts.Node; key: string }[] = [];
    const asks: ts.Node[] = [];
    const visit = (node: ts.Node) => {
      if (ts.isBinaryExpression(node) && EQUALITY.has(node.operatorToken.kind)) {
        const key = (pressed(node.left) && named(node.right)) || (pressed(node.right) && named(node.left));
        if (key) checks.push({ node, key });
      } else if (ts.isCaseClause(node) && ts.isSwitchStatement(node.parent.parent) && pressed(node.parent.parent.expression)) {
        const key = named(node.expression);
        if (key) checks.push({ node, key });
      } else if (ts.isCallExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'isComposing') {
        asks.push(node);
      }
      ts.forEachChild(node, visit);
    };
    visit(file);
    return checks
      .filter(({ node }) => {
        const home = nearestFunction(node);
        return !asks.some((ask) => home !== null && nearestFunction(ask) === home && ask.getStart() < node.getStart());
      })
      .map(({ node, key }) => `${path}:${file.getLineAndCharacterOfPosition(node.getStart()).line + 1} ${key}`);
  });
}

/** Counted too, so a scan that stopped finding the checks fails rather than passing on nothing. */
export function checks(files: [path: string, source: string][]): number {
  return unasked(files.map(([path, source]) => [path, source.replaceAll('isComposing(', 'notAsked(')])).length;
}

const sources = import.meta.glob('./**/*.{ts,tsx}', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>;

/** Tests and stories press keys to prove a handler; they handle none. */
const HARNESS = /\.(test|stories)\.tsx?$|^\.\/test\//;

const shipped = Object.entries(sources).filter(([path]) => !HARNESS.test(path));

describe('a key an input method is composing with', () => {
  it('catches a handler it is meant to catch — the check itself, sabotaged', () => {
    // The composer's shape before IME1.
    expect(unasked([['./work/Box.tsx', "const keys = (event) => { if (event.key === 'Enter' && !event.shiftKey) say(); };\n"]]))
      .toEqual(['./work/Box.tsx:1 Enter']);
    // Either side, inequality, a key of its own, a code, and a case in a switch on the key.
    expect(unasked([['./a.ts', "const f = (e) => { if ('Escape' !== e.key) return; close(); };\n"]])).toEqual(['./a.ts:1 Escape']);
    expect(unasked([['./a.ts', "function f(e) { const key = e.key; if (key == 'Enter') go(); }\n"]])).toEqual(['./a.ts:1 Enter']);
    expect(unasked([['./a.ts', "const f = (e) => e.code === 'Enter' && go();\n"]])).toEqual(['./a.ts:1 Enter']);
    expect(unasked([['./a.ts', "function f(e) {\n  switch (e.key) {\n    case 'Escape': close();\n  }\n}\n"]]))
      .toEqual(['./a.ts:3 Escape']);
    // A handler written inline in JSX.
    expect(unasked([['./a.tsx', "const x = <input onKeyDown={(event) => { if (event.key === 'Escape') clear(); }} />;\n"]]))
      .toEqual(['./a.tsx:1 Escape']);
  });

  it('takes a handler that asks first, and only first, and only itself', () => {
    expect(unasked([['./a.ts', "const f = (e) => { if (isComposing(e)) return; if (e.key === 'Enter') go(); };\n"]])).toEqual([]);
    expect(unasked([['./a.ts', "const f = (e) => { if (e.key === 'Enter' && !isComposing(e)) go(); };\n"]]))
      .toEqual(['./a.ts:1 Enter']);
    // A guard in the function that makes the handler does not guard the handler.
    expect(unasked([['./a.ts', "const make = (e) => { isComposing(e); return (p) => { if (p.key === 'Enter') go(); }; };\n"]]))
      .toEqual(['./a.ts:1 Enter']);
    // Other keys are not its business, nor another string that happens to read Enter.
    expect(unasked([['./a.ts', "const f = (e) => { if (e.key === 'ArrowDown') move(); if (label === 'Enter') x(); };\n"]]))
      .toEqual([]);
  });

  it('is looking at files at all — a vacuous check is a check that has stopped working', () => {
    expect(shipped.length).toBeGreaterThan(50);
    expect(shipped.some(([path]) => path === './work/Composer.tsx')).toBe(true);
    // The tree's own checks, seen: the composer's alone are three.
    expect(checks(shipped)).toBeGreaterThan(10);
  });

  it('holds: every handler that checks Enter or Escape asks isComposing first', () => {
    expect(unasked(shipped)).toEqual([]);
  });
});
