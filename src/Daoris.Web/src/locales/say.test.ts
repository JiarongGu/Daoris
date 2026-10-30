import { describe, expect, it } from 'vitest';
import { driverSource } from '../test/driverSource';
import { GO_ON_BRIEF, HAND_OVER, NEVER, NEW_SESSION_REFUSED, START_FROM } from '../work/say';
import { en, zh } from '.';

/**
 * The codes a refused go-on, start-from or say answers by are a twin (MSG1g3, D142 point 4, LANG1b's pattern;
 * `.claude/knowledge/twins.md`): the driver declares them as `public const string` in `GoOnNew`, `WordsNever` and `StartFrom`,
 * and this page words them in `work/say.ts`'s three tables, with no code shared. This side reads the driver's source and holds
 * the tables and both catalogues to it, so a code the driver adds or renames can never reach the page unworded. The held line
 * itself is coded already (CONVNOTE2g/2g2); what this holds is the page's refusals beside it.
 */

/**
 * The file that declares each class an alias may name, read by name: Agents' `ContinueWhy` forwards Events' `ContinueCodes`
 * (FOUNDATION1 mod-ix), and `HandOverAskWhy` is the Events-owned hand-over vocabulary.
 */
const ALIASED: Record<string, () => string> = {
  ContinueWhy: () => driverSource('ContinueReason.cs'),
  ContinueCodes: () => driverSource('ContinueCodes.cs'),
  HandOverAskWhy: () => driverSource('HandOverAskWhy.cs'),
  // FOUNDATION1d-ii part 1: `WordsNever.Continued` forwards the say door's answer's own word, which a record declares.
  SayAnswer: () => driverSource('ServiceAnswers.cs'),
};

/** Follow a forwarded constant to its literal owner; a missing target or cycle fails the twin instead of dropping a code. */
function literalOf(alias: string, name: string, seen: string[] = []): string {
  const key = `${alias}.${name}`;
  expect(seen, `constant alias cycle at \`${key}\``).not.toContain(key);
  const source = ALIASED[alias];
  expect(source, `\`${alias}\` is no class an alias here is read from: add the file that declares it`).toBeTruthy();
  const block = new RegExp(`(?:static class|record) ${alias}\\b[^{]*\\{([\\s\\S]*?)\\n\\}`).exec(source!())?.[1] ?? '';
  const target = new RegExp(`public const string ${name} = (?:"([^"]+)"|(\\w+)\\.(\\w+));`).exec(block);
  expect(target, `\`${key}\` is no constant in the file that declares ${alias}`).toBeTruthy();
  return target![1] ?? literalOf(target![2]!, target![3]!, [...seen, key]);
}

/** The `public const string` codes of one static class, one per line as the driver writes them; an alias is resolved through its target. */
function codesOf(file: string, className: string): string[] {
  return codesIn(driverSource(file), className);
}

/** The codes one class declares in a source text, which a stand-in source may be (CENSUSRULES1). */
function codesIn(text: string, className: string): string[] {
  const block = new RegExp(`static class ${className}\\b\\s*\\{([\\s\\S]*?)\\n\\}`).exec(text)?.[1] ?? '';
  return [...block.matchAll(/public const string \w+ = (?:"(?<code>[^"]+)"|(?<alias>\w+)\.(?<name>\w+));/g)].map((match) => {
    const { code, alias, name } = match.groups as { code?: string; alias?: string; name?: string };
    if (code) return code;
    // WordsNever.Teammate -> ContinueWhy.Teammate -> ContinueCodes.Teammate, each read where it is declared.
    return literalOf(alias!, name!);
  });
}

/** The codes the page words that the driver does not declare: the page's own (D137 §5.1). */
const PAGE_OWN: Record<string, string[]> = {
  // A second opinion's reviewer is the page's to say; `WordsNever` has no code for it.
  never: ['opinion'],
  startFrom: [],
  newSession: [],
  handOver: [],
  goOnBrief: [],
};

const tables = [
  { name: 'NEVER', table: NEVER, file: 'SessionSay.cs', className: 'WordsNever', own: PAGE_OWN.never },
  { name: 'START_FROM', table: START_FROM, file: 'ChatRunner.StartFrom.cs', className: 'StartFrom', own: PAGE_OWN.startFrom },
  { name: 'NEW_SESSION_REFUSED', table: NEW_SESSION_REFUSED, file: 'GoOnNew.cs', className: 'GoOnNew', own: PAGE_OWN.newSession },
  // MIXAGENT1b-i: the hand-over's own codes; what never goes on is `NEVER`'s, said through `neverSentence`.
  { name: 'HAND_OVER', table: HAND_OVER, file: 'HandOver.cs', className: 'HandOver', own: PAGE_OWN.handOver },
  // SESSCTX1c part B: *Go on with Daoris's brief*'s own codes; the rest are `START_FROM`'s, the hand-over's tree and `NEVER`'s.
  { name: 'GO_ON_BRIEF', table: GO_ON_BRIEF, file: 'BriefedMove.cs', className: 'GoOnBrief', own: PAGE_OWN.goOnBrief },
];

describe('the refusal codes the driver declares, held to the page’s tables', () => {
  it('reads every account-rule code through its forwarding constant', () => {
    // The scan resolves each forwarded constant to a literal; the codes are the driver's to add (the table test below holds the page to them).
    const codes = codesOf('ContinueReason.cs', 'ContinueWhy');
    expect(codes.length).toBeGreaterThan(0);
    expect(codes.every((code) => /^[a-z-]+$/.test(code))).toBe(true);
    expect(new Set(codes).size).toBe(codes.length);
  });

  it.each(tables)('reads the driver’s $className declarations', ({ file, className }) => {
    // A scan that matched nothing would pass on a moved or renamed class: it has to see some, each once. Which codes are the driver's to say.
    const codes = codesOf(file, className);
    expect(codes.length, `${className} in ${file} declares no code the scan can read`).toBeGreaterThan(0);
    expect(new Set(codes).size).toBe(codes.length);
  });

  /** What differs between the codes a class declares and the page's table: a code with no entry, an entry for no code. */
  function differences(table: Record<string, string>, codes: string[], own: string[]): string[] {
    const wanted = new Set([...codes, ...own]);
    return [
      ...codes.filter((code) => !(code in table)).map((code) => `${code} has no page entry`),
      ...Object.keys(table).filter((code) => !wanted.has(code)).map((code) => `${code} is declared by no code of the driver`),
    ];
  }

  it.each(tables)('holds $name to the driver’s $className codes: every code has a page entry, bar the page’s own', ({ table, file, className, own }) => {
    expect(differences(table, codesOf(file, className), own)).toEqual([]);
  });

  it('fails a code the driver adds without a page entry and passes a declaration edited around the codes', () => {
    const table = { a: 'k.a', b: 'k.b' };
    const declared = 'static class Said {\n    public const string A = "a";\n    public const string B = "b";\n}\n';
    expect(differences(table, codesIn(declared, 'Said'), [])).toEqual([]);
    expect(differences(table, codesIn(declared.replace('    public const string B', '    public const string C = "c";\n    public const string B'), 'Said'), []))
      .toEqual(['c has no page entry']);
    // A comment line, a moved declaration and a reordering are no change of the codes.
    expect(differences(table, codesIn('static class Said {\n    // a note\n    public const string B = "b";\n\n    public const string A = "a";\n}\n', 'Said'), [])).toEqual([]);
  });

  it.each([['en', en], ['zh', zh]] as const)('has a sentence in %s for every code in each table', (_, catalogue) => {
    for (const { name, table } of tables) {
      for (const [code, key] of Object.entries(table)) {
        expect(catalogue[key], `${name}'s \`${code}\` has no \`${key}\``).toBeTruthy();
      }
    }
  });
});
