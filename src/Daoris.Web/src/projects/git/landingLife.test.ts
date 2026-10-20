import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import {
  END_BY, landingFlow, landingOf, PULL_REQUEST_STATES, REMOVED_AS, TRY_CODES,
} from './landingLife';
import {
  OLDER_BRANCH, SHARED_BRANCH, SHARED_DUE, SHARED_UNITS, traceAnswerOf, WAITING_BRANCH, WAITING_DUE, WAITING_UNITS,
} from './landingFixtures';

// FLOWCHART1c (D186; the flowchart design §2.7, §7 traps 10, 11 and 19): a landed branch's landing and clean-up drawn from its
// records. The tries are its due entry's, each by its code and like the others of its code, so three in a row stack; the landing is
// the branch's, with every later done that moved it on and the session each was made for, never one session's; the push is a fact
// with no time and the pull request's answer a snapshot, with no edge between them that a date does not order; the end, the clean-up
// and the recovery are the units' the branch carries. The codes are page constants, held to the driver's source here.

const t = i18n.t.bind(i18n);
const root = join(process.cwd(), '..', '..');
const driver = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
const files = readdirSync(join(root, ...driver.split('/')), { recursive: true, encoding: 'utf8' })
  .filter((path) => !/[\\/](bin|obj)[\\/]/.test(path));
const only = (name: string) => {
  const found = files.filter((path) => path.split(/[\\/]/).at(-1) === name);
  if (found.length !== 1) throw new Error(`${name} is expected once under ${driver}, found ${found.length}`);
  return readFileSync(join(root, ...driver.split('/'), found[0]!), 'utf8');
};

/** A static class's body, from its declaration to the next type's. */
const classBody = (text: string, name: string) => {
  const start = text.search(new RegExp(`static class ${name}\\b`));
  if (start < 0) return '';
  const rest = text.slice(start);
  const next = rest.slice(1).search(/\n(public|internal) (sealed |static |partial )*(class|record|enum)\b/);
  return next < 0 ? rest : rest.slice(0, next + 1);
};

/** Each `const string` a class declares, by its value: a word, or another class's constant it names (`LandedKind.Merged`). */
function constants(text: string, name: string, others: Record<string, Record<string, string>> = {}): Record<string, string> {
  const found: Record<string, string> = {};
  for (const [, member, value] of classBody(text, name).matchAll(/public const string (\w+) = ([^;]+);/g)) {
    const word = /^"([^"]*)"$/.exec(value!.trim())?.[1];
    const named = /^(\w+)\.(\w+)$/.exec(value!.trim());
    found[member!] = word ?? (named ? others[named[1]!]?.[named[2]!] ?? `?${value}` : `?${value}`);
  }
  return found;
}

const autoLanding = only('AutoLanding.cs');
const cleanups = only('LandedCleanups.cs');
const landedKind = constants(cleanups, 'LandedKind');

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('the codes the landing is drawn by, held to the driver (trap 11)', () => {
  it('reads each class the page copies: a scan that matched nothing would pass on a moved file', () => {
    expect(Object.keys(constants(autoLanding, 'AutoLandingCode')).length).toBeGreaterThan(15);
    expect(Object.keys(landedKind).length).toBeGreaterThan(5);
    expect(Object.keys(constants(only('EndJudgement.cs'), 'EndBy', { LandedKind: landedKind })).length).toBeGreaterThan(5);
  });

  it('knows every try code `AutoLandingCode` declares, and no other', () => {
    expect([...TRY_CODES].sort()).toEqual(Object.values(constants(autoLanding, 'AutoLandingCode')).sort());
  });

  it('knows every proof an end rests on (`EndBy`), and the pull request\'s states (`PullRequestStates.All`)', () => {
    expect([...END_BY].sort()).toEqual(Object.values(constants(only('EndJudgement.cs'), 'EndBy', { LandedKind: landedKind })).sort());
    const states = constants(only('PullRequestStates.cs'), 'PullRequestStates');
    const all = /All\s*=\s*\[([^\]]*)\]/.exec(classBody(only('PullRequestStates.cs'), 'PullRequestStates'))?.[1] ?? '';
    expect([...PULL_REQUEST_STATES]).toEqual(all.split(',').map((member) => states[member.trim()]));
  });

  it('words each removal by a proof `LandedKind` declares', () => {
    for (const code of REMOVED_AS) expect(Object.values(landedKind), code).toContain(code);
  });

  it('words every try code, end, pull request state and removal in both languages', () => {
    for (const code of TRY_CODES) {
      expect(en[`work.trace.try.${code}`], code).toBeTruthy();
      expect(zh[`work.trace.try.${code}`], code).toBeTruthy();
    }
    for (const state of PULL_REQUEST_STATES) {
      expect(en[`landing.pr.state.${state}`], state).toBeTruthy();
      expect(zh[`landing.pr.state.${state}`], state).toBeTruthy();
    }
    for (const code of END_BY) expect(en[`work.milestone.by.${code}`], code).toBeTruthy();
    for (const code of REMOVED_AS) {
      expect(en[`landing.removedAs.${code}`], code).toBeTruthy();
      expect(zh[`landing.removedAs.${code}`], code).toBeTruthy();
    }
  });
});

describe('the tries, each by its code (§2.7 node 1)', () => {
  const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS });
  const tries = life.nodes.filter((node) => node.id.startsWith('try:'));

  it('draws the due entry, then each try in its order from it, the last into the landing it made', () => {
    expect(life.entries).toEqual(['due']);
    expect(tries.map((node) => node.title)).toEqual([
      'its tree holds uncommitted work', 'its quest is held for your yes', 'its quest is held for your yes', 'its quest is held for your yes', 'landed',
    ]);
    const chain = ['due', ...tries.map((node) => node.id), 'landed'];
    for (let index = 1; index < chain.length; index += 1) {
      expect(life.edges.some((edge) => edge.from === chain[index - 1] && edge.to === chain[index]), `${chain[index - 1]} → ${chain[index]}`).toBe(true);
    }
  });

  it('marks each try like the others of its code, so the three holds in a row stack and the two lone ones stay apart', () => {
    expect(tries.map((node) => node.stack?.key)).toEqual(['uncommitted', 'held', 'held', 'held', 'landed']);
    expect(new Set(tries.filter((node) => node.stack?.key === 'held').map((node) => node.stack?.word))).toEqual(new Set(['its quest is held for your yes']));
    expect(tries.every((node) => node.stack?.at)).toBe(true);
  });

  it('says the facts each try was made at', () => {
    expect(life.lines['try:0']).toContain('2 uncommitted paths');
    expect(life.lines['try:4']).toEqual(expect.arrayContaining(['branch `review/q6-asset-cache`', '3 commits']));
  });

  it('draws a try code this page does not know as recorded, never dropped (trap 10)', () => {
    const due = { ...SHARED_DUE, tries: [{ at: SHARED_DUE.since, code: 'parked-by-moon' }] };
    const drawn = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due });
    expect(drawn.nodes.find((node) => node.id === 'try:0')?.title).toBe('parked-by-moon, shown as recorded');
  });

  it('draws no due entry for a landing never due, and enters at the landing', () => {
    const drawn = landingFlow(t, { name: WAITING_BRANCH.branch, branch: WAITING_BRANCH });
    expect(drawn.entries).toEqual(['landed']);
    expect(drawn.nodes.some((node) => node.id === 'due')).toBe(false);
  });
});

describe('the landing, the branch\'s and never one session\'s (§2.7 node 2, §10\'s trap)', () => {
  const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS });

  it('says who accepted it, its rule, its review and the session it was made for', () => {
    expect(life.nodes.find((node) => node.id === 'landed')?.title).toBe('Landed on review/q6-asset-cache');
    expect(life.lines.landed).toEqual(expect.arrayContaining([
      'made for session s2', 'reviewed by you in `local`', 'carries the work of u-s2, u-s3, u-s4',
    ]));
    expect(life.edges.find((edge) => edge.to === 'landed')?.says).toBe('accepted automatically');
  });

  it('keeps every advance, each naming its session, like the others of its session, so a session that moved it on three times stacks', () => {
    const advances = life.nodes.filter((node) => node.id.startsWith('advance:'));
    expect(advances.map((node) => node.title)).toEqual([
      'Moved on by session s3', 'Moved on by session s4', 'Moved on by session s4', 'Moved on by session s4',
    ]);
    expect(advances.map((node) => node.stack?.key)).toEqual(['advance:s3', 'advance:s4', 'advance:s4', 'advance:s4']);
    const chain = ['landed', ...advances.map((node) => node.id)];
    expect(chain.slice(1).map((id, index) => life.edges.find((edge) => edge.from === chain[index] && edge.to === id)?.says)).toEqual([
      'accepted by you', 'accepted automatically', 'accepted automatically', 'accepted automatically',
    ]);
    expect(life.lines['advance:0']).toEqual(expect.arrayContaining(['unit u-s3', '#q7']));
  });

  it('reads the same landing from a session that only moved it on, since the trace answers it for each session it names', () => {
    const answer = traceAnswerOf('s3', SHARED_BRANCH, null);
    expect(landingOf(answer, 's3', SHARED_BRANCH.branch)?.branch?.session).toBe('s2');
  });

  it('says the advances were not read where a driver from before them answers none', () => {
    const drawn = landingFlow(t, { name: OLDER_BRANCH.branch, branch: OLDER_BRANCH });
    expect(drawn.lines.landed).toContain('Its advances were not read: this driver answers none.');
    expect(drawn.nodes.some((node) => node.id.startsWith('advance:'))).toBe(false);
  });

  it('draws a landing into the line from its gap: merged and accepted, with no branch kept', () => {
    const answer = traceAnswerOf('s9', null, null, 'merge-accepted');
    const drawn = landingFlow(t, { name: 'main', ...landingOf(answer, 's9', 'main')! });
    expect(drawn.nodes.find((node) => node.id === 'landed')?.title).toBe('Merged into the line');
    expect(drawn.lines.landed).toContain('Accepted; no branch kept.');
    expect(drawn.nodes.some((node) => node.id === 'pushed' || node.id === 'pull-request')).toBe(false);
  });
});

describe('the push and the pull request, as the record keeps them (trap 19)', () => {
  const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS });

  it('draws the push as a fact with no time, hung from the landing and not after its advances', () => {
    expect(life.nodes.find((node) => node.id === 'pushed')?.title).toBe('Pushed');
    expect(life.lines.pushed).toEqual(['Its record keeps that it was pushed, not when.']);
    expect(life.edges.filter((edge) => edge.to === 'pushed').map((edge) => [edge.from, edge.says])).toEqual([['landed', 'pushed by github']]);
  });

  it('draws the pull request\'s latest answer as one snapshot, and no edge between the push and it', () => {
    const pull = life.nodes.find((node) => node.id === 'pull-request');
    expect(pull?.state?.word).toBe('merged');
    expect(life.nodes.filter((node) => node.id.startsWith('pull-request'))).toHaveLength(1);
    expect(life.edges.filter((edge) => edge.to === 'pull-request').map((edge) => edge.from)).toEqual(['landed']);
    expect(life.edges.some((edge) => [edge.from, edge.to].includes('pushed') && [edge.from, edge.to].includes('pull-request'))).toBe(false);
    expect(life.pullRequest).toBe('https://example.test/pull/42');
  });

  it('says an open answer and the ask that failed since, each with when, and never orders them as moves', () => {
    const drawn = landingFlow(t, { name: WAITING_BRANCH.branch, branch: WAITING_BRANCH, due: WAITING_DUE, units: WAITING_UNITS });
    expect(drawn.nodes.find((node) => node.id === 'pull-request')?.state?.word).toBe('open');
    expect(drawn.lines['pull-request']!.some((line) => line.startsWith('Could not read `github`: late.'))).toBe(true);
    expect(drawn.edges.find((edge) => edge.to === 'landed')?.says).toBe('accepted by you');
  });

  it('draws no pull request where none is kept, and a handed branch as not pushed', () => {
    const drawn = landingFlow(t, { name: OLDER_BRANCH.branch, branch: OLDER_BRANCH });
    expect(drawn.nodes.find((node) => node.id === 'pushed')?.title).toBe('Not pushed');
    expect(drawn.edges.find((edge) => edge.to === 'pushed')?.says).toBe('handed to github, which did not push it');
    expect(drawn.nodes.some((node) => node.id === 'pull-request')).toBe(false);
  });
});

describe('the end, the clean-up and the recovery, the units\' it carries (§2.7 nodes 4-6)', () => {
  it('says each unit\'s end, reached on the platform, then the clean-up completed and the recovery kept until its date', () => {
    const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS });
    expect(life.nodes.find((node) => node.id === 'end')?.reached).toBe(true);
    expect(life.lines.end).toHaveLength(3);
    expect(life.lines.end![0]).toMatch(/^u-s2: Reached: its pull request completed and its merge commit is on main/);
    expect(life.edges.find((edge) => edge.to === 'end')).toMatchObject({ from: 'pull-request', says: 'merged on the platform' });
    expect(life.nodes.find((node) => node.id === 'cleanup')).toMatchObject({ title: 'Clean-up', state: { word: 'completed', tone: 'done' } });
    expect(life.edges.find((edge) => edge.to === 'cleanup')?.says).toBe('removed by the clean-up');
    expect(life.lines.cleanup).toContain('Removed: tree engine-s2, its work is on the line');
    expect(life.lines.recovery!.some((line) => line.startsWith('recovery ref refs/daoris/recovery/s2, kept until'))).toBe(true);
  });

  it('says an end not reached, from the branch\'s tip and on a guard, and a clean-up that waits, with what waits and why', () => {
    const life = landingFlow(t, { name: WAITING_BRANCH.branch, branch: WAITING_BRANCH, due: WAITING_DUE, units: WAITING_UNITS });
    expect(life.nodes.find((node) => node.id === 'end')?.reached).toBe(false);
    expect(life.edges.find((edge) => edge.to === 'end')).toMatchObject({ from: 'landed', style: 'conditional' });
    expect(life.nodes.find((node) => node.id === 'cleanup')).toMatchObject({ title: 'Clean-up', state: { word: 'pending', tone: 'neutral' } });
    expect(life.lines.cleanup).toContain('Waits: session branch daoris/s5, its pull request is open');
    expect(life.edges.find((edge) => edge.to === 'cleanup')).toMatchObject({ style: 'conditional', says: 'the clean-up waits' });
    expect(life.nodes.some((node) => node.id === 'recovery')).toBe(false);
  });

  it('says a clean-up blocked on the person in open\'s hue, as every wait on them is (trap 8)', () => {
    const blocked = [{ ...WAITING_UNITS[0]!, cleanup: { ...WAITING_UNITS[0]!.cleanup, waits: [{ ...WAITING_UNITS[0]!.cleanup.waits[0]!, disposition: 'blocked' }] } }];
    const life = landingFlow(t, { name: WAITING_BRANCH.branch, branch: WAITING_BRANCH, units: blocked });
    expect(life.nodes.find((node) => node.id === 'cleanup')?.state).toEqual({ word: 'blocked', tone: 'open' });
    expect(life.lines.cleanup).toContain('Blocked: session branch daoris/s5, its pull request is open');
  });

  it('draws none of them where the units were not read', () => {
    const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, units: null });
    expect(life.nodes.some((node) => ['end', 'cleanup', 'recovery'].includes(node.id))).toBe(false);
  });
});

describe('in 中文', () => {
  it('words the nodes and the edges in the reader\'s language', async () => {
    await i18n.changeLanguage('zh');
    const life = landingFlow(t, { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS });
    expect(life.label).toBe(zh['landing.label']!.replace('{{branch}}', SHARED_BRANCH.branch));
    expect(life.nodes.find((node) => node.id === 'pushed')?.title).toBe(zh['landing.pushed.title']);
    expect(life.edges.find((edge) => edge.to === 'pushed')?.says).toBe(zh['landing.edge.pushed']!.replace('{{plugin}}', 'github'));
    expect(life.nodes.find((node) => node.id === 'try:1')?.title).toBe(zh['work.trace.try.held']);
  });
});
