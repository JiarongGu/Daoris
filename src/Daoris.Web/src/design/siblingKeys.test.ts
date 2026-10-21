import ts from 'typescript';
import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';

/**
 * **Two children of one parent never share a key** (RELSPAM1). React tells a parent's children apart by their keys across every
 * slot of that parent, not only within a list. Two slots keyed alike are matched to each other's fibers, and the one left over is
 * never unmounted: it stays in the page, one more at every draw. The record's *Who it worked with* and its milestone were both keyed
 * by the session's id, so a running driven session's relations line stood eleven times over on an installed window, a copy per
 * change of its record.
 *
 * Held here for the reason `composingKeys.test.ts` is: it breaks one parent at a time and silently, React's own warning is in the
 * development console alone, and the copies only pile up on a page drawn again and again, which a test seldom draws.
 *
 * Read with the TypeScript parser, because the unit is a parent's slots. A slot is a child element, or an expression whose element
 * is drawn under a condition (`a && <X/>`, `a ? <X/> : <Y/>`, parenthesised); the branches of one condition are one slot, since
 * only one is drawn. A list (`rows.map(…)`) is a scope of its own, and is not read here.
 *
 * @remarks
 * Keys are compared as written, whitespace aside, so two slots keyed `session.id` are caught. Two slots keyed by the same record
 * say what each is beside the id (`relations:${session.id}`, `milestone:${session.id}`), which also keeps them apart where two
 * different expressions hold one value.
 */

const LOGICAL = new Set([ts.SyntaxKind.AmpersandAmpersandToken, ts.SyntaxKind.BarBarToken, ts.SyntaxKind.QuestionQuestionToken]);

/** An element's key as written, whitespace aside, or null where it has none. */
function keyOf(element: ts.JsxElement | ts.JsxSelfClosingElement): string | null {
  const { attributes } = ts.isJsxElement(element) ? element.openingElement : element;
  for (const attribute of attributes.properties) {
    if (!ts.isJsxAttribute(attribute) || attribute.name.getText() !== 'key' || !attribute.initializer) continue;
    const value = attribute.initializer;
    const text = ts.isJsxExpression(value) ? value.expression?.getText() : value.getText();
    return text ? text.replace(/\s+/g, '') : null;
  }
  return null;
}

/** The keys a slot may be drawn with: an element's own, or each branch's of the condition it is drawn under. */
function keysOf(node: ts.Node): string[] {
  if (ts.isJsxElement(node) || ts.isJsxSelfClosingElement(node)) {
    const key = keyOf(node);
    return key ? [key] : [];
  }
  if (ts.isJsxExpression(node)) return node.expression ? keysOf(node.expression) : [];
  if (ts.isParenthesizedExpression(node)) return keysOf(node.expression);
  if (ts.isBinaryExpression(node) && LOGICAL.has(node.operatorToken.kind)) {
    return node.operatorToken.kind === ts.SyntaxKind.AmpersandAmpersandToken
      ? keysOf(node.right)
      : [...keysOf(node.left), ...keysOf(node.right)];
  }
  if (ts.isConditionalExpression(node)) return [...keysOf(node.whenTrue), ...keysOf(node.whenFalse)];
  return [];
}

/** Each slot keyed as an earlier slot of its parent is, as `path:line key`. */
export function sharedKeys(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) => {
    const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
    const found: string[] = [];
    const visit = (node: ts.Node) => {
      if (ts.isJsxElement(node) || ts.isJsxFragment(node)) {
        const seen = new Set<string>();
        for (const child of node.children) {
          for (const key of new Set(keysOf(child))) {
            if (seen.has(key)) found.push(`${path}:${file.getLineAndCharacterOfPosition(child.getStart()).line + 1} ${key}`);
            seen.add(key);
          }
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(file);
    return found;
  });
}

/** Counted too, so a scan that stopped finding keyed slots fails rather than passing on nothing. */
export function keyedSlots(files: [path: string, source: string][]): number {
  let count = 0;
  for (const [path, source] of files) {
    const file = ts.createSourceFile(path, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
    const visit = (node: ts.Node) => {
      if (ts.isJsxElement(node) || ts.isJsxFragment(node)) count += node.children.filter((child) => keysOf(child).length > 0).length;
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return count;
}

const sources = rooted(import.meta.glob('/src/**/*.tsx', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>);

/** Tests and stories draw a component to prove it; what they draw beside it is not the page. */
const HARNESS = /\.(test|stories)\.tsx$|^\.\/test\//;

const shipped = Object.entries(sources).filter(([path]) => !HARNESS.test(path));

describe('the children of one parent', () => {
  it('catches two slots keyed alike — the check itself, sabotaged', () => {
    // The record's shape before RELSPAM1: two folds keyed by the session, each under its own condition.
    expect(sharedKeys([['./work/A.tsx',
      'const a = <article>\n  {relations && <Relations key={session.id} />}\n  {milestone && <Milestone key={session.id} />}\n</article>;\n',
    ]])).toEqual(['./work/A.tsx:3 session.id']);
    // A fragment's children, an element drawn always beside one drawn under a condition, whitespace aside.
    expect(sharedKeys([['./a.tsx', 'const a = <><Strip key={attended.id} />{talking && (<Composer key={ attended.id } />)}</>;\n']]))
      .toEqual(['./a.tsx:1 attended.id']);
    // Either branch of a condition, a fallback, and a key written as a string.
    expect(sharedKeys([['./a.tsx', 'const a = <div><A key={id} />{x ? <B /> : <C key={id} />}</div>;\n']])).toEqual(['./a.tsx:1 id']);
    expect(sharedKeys([['./a.tsx', 'const a = <div><A key={id} />{x ?? <C key={id} />}</div>;\n']])).toEqual(['./a.tsx:1 id']);
    expect(sharedKeys([['./a.tsx', 'const a = <div><A key="one" /><B key="one" /></div>;\n']])).toEqual(['./a.tsx:1 "one"']);
  });

  it('takes keys that differ, a condition whose branches share one, lists, and other parents', () => {
    expect(sharedKeys([['./a.tsx',
      'const a = <div>{r && <A key={`relations:${id}`} />}{m && <B key={`milestone:${id}`} />}</div>;\n',
    ]])).toEqual([]);
    // Only one branch is drawn.
    expect(sharedKeys([['./a.tsx', 'const a = <div>{x ? <A key={id} /> : <B key={id} />}</div>;\n']])).toEqual([]);
    // A list is a scope of its own.
    expect(sharedKeys([['./a.tsx', 'const a = <ul>{rows.map((row) => <li key={row.id} />)}{rows.map((row) => <li key={row.id} />)}</ul>;\n']]))
      .toEqual([]);
    // Another parent.
    expect(sharedKeys([['./a.tsx', 'const a = <div><A key={id} /><section><B key={id} /></section></div>;\n']])).toEqual([]);
  });

  it('is looking at files at all — a vacuous check is a check that has stopped working', () => {
    expect(shipped.length).toBeGreaterThan(100);
    expect(shipped.some(([path]) => path === './work/AttendedSession.tsx')).toBe(true);
    // The tree's own keyed slots, seen: eighteen when the gate came, the record's three and the frame's foot's four among them.
    expect(keyedSlots(shipped)).toBeGreaterThan(10);
  });

  it('holds: no two children of one parent share a key', () => {
    expect(sharedKeys(shipped)).toEqual([]);
  });
});
