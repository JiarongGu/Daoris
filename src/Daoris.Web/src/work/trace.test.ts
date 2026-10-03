import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { en, zh } from '../locales';
import { traceStory } from './trace';
import { QUEST_CHAIN, SESSION_CHAIN } from './traceFixtures';

/**
 * A trace's chain carries codes the driver declares and this page words (TRACE1b, D143), with no code shared: a twin, as an
 * instruction account's codes are (`.claude/knowledge/twins.md`). This side parses the driver's declarations in
 * `Trace.Chain.cs` and holds both catalogues to them both ways. The driver's `DriverModuleTraceTests` holds the route's side.
 */

const root = join(process.cwd(), '..', '..');
const driver = (file: string) => readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', file), 'utf8');
const source = driver('Trace.Chain.cs');
// LAND2b's codes, which the chain carries as their own declarations name them: a try's, who accepted, a rule's source.
const landing = driver('AutoLanding.cs') + driver('Landing.cs');

/** A class's codes as the driver declares them: each `const string`, by its name and value. */
function declared(holder: string, text = holder.startsWith('Trace') ? source : landing): Map<string, string> {
  const block = new RegExp(`public static class ${holder}\\s*\\{([\\s\\S]*?)\\n\\}`).exec(text)?.[1] ?? '';
  return new Map([...block.matchAll(/public const string (\w+) = "([^"]+)";/g)].map((match) => [match[1]!, match[2]!]));
}

/** Each catalogue family and the class whose codes it words. */
const FAMILIES = [
  ['work.trace.source.', 'TraceStores'],
  ['work.trace.ask.gap.', 'TraceLinkGaps'],
  ['work.trace.quest.gap.', 'TraceLinkGaps'],
  ['work.trace.events.gap.', 'TraceEventGaps'],
  ['work.trace.rules.gap.', 'TraceRuleGaps'],
  ['work.trace.landing.gap.', 'TraceLandingGaps'],
  ['work.trace.stood.gap.', 'TraceStoodGaps'],
  ['work.trace.standing.', 'TraceStandings'],
  ['work.trace.goAhead.stood.', 'TraceGoAheadStands'],
  ['work.trace.answer.', 'TraceAnswers'],
  ['work.trace.tree.', 'TraceTrees'],
  ['work.trace.namedBy.', 'TraceNamers'],
  ['work.trace.try.', 'AutoLandingCode'],
  ['work.trace.acceptedBy.', 'AcceptedBy'],
  ['work.trace.rule.', 'LandingSource'],
] as const;

/** The stores the chain says did not answer, each a `TraceUnread` the driver makes. */
function unreadStores(): string[] {
  const stores = declared('TraceStores');
  return [...source.matchAll(/new TraceUnread\(TraceStores\.(\w+)\)/g)].map((match) => stores.get(match[1]!) ?? `unknown ${match[1]}`).sort();
}

const codesOf = (catalogue: Record<string, string>, prefix: string) =>
  [...new Set(Object.keys(catalogue).filter((key) => key.startsWith(prefix))
    .map((key) => key.slice(prefix.length).replace(/_(zero|one|two|few|many|other)$/, '')))].sort();

describe('a trace’s codes, held to both catalogues', () => {
  it('reads the driver’s declarations', () => {
    // A scan that matched nothing would pass on a moved or renamed file: it has to see them.
    expect([...declared('TraceStores').values()].sort())
      .toEqual(['asks', 'auto-landings', 'config', 'events', 'landings', 'quests', 'rules', 'sessions']);
    expect([...declared('TraceLandingGaps').values()].sort()).toEqual(['merge-accepted', 'none', 'unknown', 'unread']);
    expect([...declared('AcceptedBy').values()].sort()).toEqual(['auto', 'person']);
    expect([...declared('LandingSource').values()].sort()).toEqual(['default', 'repository', 'workspace']);
    expect(declared('AutoLandingCode').size).toBeGreaterThanOrEqual(14);
    expect(unreadStores()).toEqual(['landings', 'quests', 'sessions']);
    for (const [, holder] of FAMILIES) expect(declared(holder).size, holder).toBeGreaterThan(0);
  });

  it.each([['en', en], ['zh', zh]] as const)('words every code in %s, and keeps no entry no code declares', (_, catalogue) => {
    for (const [prefix, holder] of FAMILIES) {
      expect(codesOf(catalogue, prefix), `${prefix} ↔ ${holder}`).toEqual([...declared(holder).values()].sort());
    }
    expect(codesOf(catalogue, 'work.trace.unread.')).toEqual(unreadStores());
  });
});

describe('a chain’s story, for the folded line', () => {
  it('names the ask and the quest it came through, and counts the sessions', () => {
    expect(traceStory(QUEST_CHAIN)).toEqual({ asks: ['a1'], senders: [], quests: ['q1'], sessions: 2 });
    expect(traceStory(SESSION_CHAIN)).toEqual({ asks: ['a1'], senders: [], quests: ['q1'], sessions: 1 });
  });
});
