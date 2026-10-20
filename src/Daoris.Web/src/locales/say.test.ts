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

/** The `public const string` codes of one static class, one per line as the driver writes them; an alias is resolved through its target. */
function codesOf(file: string, className: string): string[] {
  const text = driverSource(file);
  const block = new RegExp(`static class ${className}\\b\\s*\\{([\\s\\S]*?)\\n\\}`).exec(text)?.[1] ?? '';
  return [...block.matchAll(/public const string \w+ = (?:"(?<code>[^"]+)"|(?<alias>\w+)\.(?<name>\w+));/g)].map((match) => {
    const { code, alias, name } = match.groups as { code?: string; alias?: string; name?: string };
    if (code) return code;
    // An alias (`WordsNever.Teammate = ContinueWhy.Teammate`) names another class's constant: read that one.
    const target = new RegExp(`public const string ${name} = "([^"]+)";`).exec(driverSource('Continuation.cs'));
    expect(target, `\`${alias}.${name}\` is not a literal in Continuation.cs`).toBeTruthy();
    return (target as RegExpExecArray)[1];
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
  it('reads the driver’s declarations', () => {
    // A scan that matched nothing would pass on a moved or renamed class: it has to see them.
    expect(codesOf('GoOnNew.cs', 'GoOnNew').sort()).toEqual(['closed', 'conversation', 'no-words', 'not-cooling', 'running']);
    expect(codesOf('SessionSay.cs', 'WordsNever').sort()).toEqual([
      'continued', 'help', 'intake', 'not-found', 'stood-down', 'superseded', 'teammate',
    ]);
    expect(codesOf('ChatRunner.StartFrom.cs', 'StartFrom').sort()).toEqual(['carried', 'no-words', 'running']);
    expect(codesOf('BriefedMove.cs', 'GoOnBrief')).toEqual(['agent-gone']);
    expect(codesOf('HandOver.cs', 'HandOver').sort()).toEqual([
      'no-door', 'not-conversation', 'not-its-account', 'not-last-run', 'parked', 'quest-closed', 'quest-elsewhere', 'quest-reopened', 'running',
      'same-agent', 'tree-gone', 'unbriefed', 'unknown-agent', 'waiting',
    ]);
  });

  it.each(tables)('holds $name to exactly the driver’s $className codes, bar the page’s own', ({ table, file, className, own }) => {
    expect(Object.keys(table).sort()).toEqual([...codesOf(file, className), ...own].sort());
  });

  it.each([['en', en], ['zh', zh]] as const)('has a sentence in %s for every code in each table', (_, catalogue) => {
    for (const { name, table } of tables) {
      for (const [code, key] of Object.entries(table)) {
        expect(catalogue[key], `${name}'s \`${code}\` has no \`${key}\``).toBeTruthy();
      }
    }
  });
});
