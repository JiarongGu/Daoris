import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import type { Session, SessionState } from '../../data/api';
import { SESSION_ACTIVE, SESSION_TONE } from '../../design/ui';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { ENDED_STATES, LIVE_STATES, SESSION_MOVES, SESSION_STATES, sessionFlow } from './sessionLife';

// FLOWCHART1b (the flowchart design §2.2, §7 traps 8, 10, 11, 18 and 19): a session's states and the moves between them,
// drawn at the head of its Timeline view. The moves are a page constant, so this holds them to the code's own table,
// `SessionLedger.Allowed`, read from the service's source (`.claude/knowledge/twins.md`): a second copy no test holds drifts.

const root = join(process.cwd(), '..', '..');
const core = 'src/Daoris.Service/Daoris.Service.Core';
const files = readdirSync(join(root, ...core.split('/')), { recursive: true, encoding: 'utf8' });
const only = (name: string) => {
  const found = files.filter((path) => path.split(/[\\/]/).at(-1) === name);
  if (found.length !== 1) throw new Error(`${name} is expected once under ${core}, found ${found.length}`);
  return readFileSync(join(root, ...core.split('/'), found[0]!), 'utf8');
};
const ledger = only('SessionLedger.cs');

/** `AwaitingPerson` as the wire spells it, `awaiting-person`. */
const spelled = (member: string) => member.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();

/**
 * The pairs `SessionLedger.Allowed` allows, read from its switch: each arm names a state and lists what it may become between
 * brackets (`SessionState.Queued => [SessionState.Starting, ...]`); the arm for every ended state is the empty `_ => []`.
 */
function allowed(text: string): [string, string][] {
  const body = /IReadOnlyList<SessionState> Allowed\(SessionState from\) => from switch\s*\{([\s\S]*?)\n\s*\};/.exec(text)?.[1] ?? '';
  return [...body.matchAll(/SessionState\.(\w+)\s*=>\s*\[([^\]]*)\]/g)].flatMap((arm) =>
    [...arm[2]!.matchAll(/SessionState\.(\w+)/g)].map((to) => [spelled(arm[1]!), spelled(to[1]!)] as [string, string]));
}

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('a session\'s moves, held to the code (trap 11)', () => {
  it('reads the arms of SessionLedger.Allowed: a scan that matched nothing would pass on a moved table', () => {
    expect(allowed(ledger).length).toBeGreaterThan(10);
  });

  it('draws exactly the moves the code allows', () => {
    const sort = (pairs: readonly (readonly [string, string])[]) => pairs.map(([from, to]) => `${from}>${to}`).sort();
    expect(sort(SESSION_MOVES)).toEqual(sort(allowed(ledger)));
  });

  it('holds the nine states the code has, in its order, and the live ones apart from the ended', () => {
    const block = /public enum SessionState\s*\{([^}]*)\}/.exec(only('Sessions.cs'))?.[1] ?? '';
    const members = block.split('\n').flatMap((line) => /^\s*(\w+)\s*,?\s*$/.exec(line)?.[1] ?? []).map(spelled);
    expect(members).toEqual([...SESSION_STATES]);
    expect([...LIVE_STATES, ...ENDED_STATES].sort()).toEqual([...SESSION_STATES].sort());
    expect(new Set(LIVE_STATES)).toEqual(new Set(SESSION_ACTIVE));
  });

  it('leaves no move from an ended state, and lets the person stop every live one', () => {
    // `Allowed` has no arm for an ended state: going on is the ledger's other move, with guards of its own (`GoOnAsync`).
    expect(SESSION_MOVES.filter(([from]) => !SESSION_ACTIVE.has(from as SessionState))).toEqual([]);
    for (const live of LIVE_STATES) expect(SESSION_MOVES, live).toContainEqual([live, 'stopped']);
  });

  it('words each move between live states, and each outcome\'s cause, in both languages', () => {
    for (const [from, to] of SESSION_MOVES.filter(([, to]) => SESSION_ACTIVE.has(to as SessionState))) {
      expect(en[`work.life.move.${from}.${to}`], `${from} → ${to}`).toBeTruthy();
      expect(zh[`work.life.move.${from}.${to}`], `${from} → ${to}`).toBeTruthy();
    }
    for (const outcome of ENDED_STATES) {
      expect(en[`work.life.by.${outcome}`], outcome).toBeTruthy();
      expect(zh[`work.life.by.${outcome}`], outcome).toBeTruthy();
    }
  });
});

describe('the states a session\'s Timeline view draws (§2.2)', () => {
  const t = i18n.t.bind(i18n);
  const session = (state: SessionState, over: Partial<Session> = {}): Session => ({
    id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', adapter: 'claude-code', state, kind: 'driven',
    created: '2026-10-20T09:00:00Z', updated: '2026-10-20T11:00:00Z', ...over,
  });
  const lit = (state: SessionState, over: Partial<Session> = {}, shown?: Parameters<typeof sessionFlow>[2]) => {
    const flow = sessionFlow(t, session(state, over), shown);
    return flow.nodes.filter((node) => node.current);
  };

  it('is entered at queued, and draws the four live states and one node for every way it ends', () => {
    const flow = sessionFlow(t, session('working'));
    expect(flow.entries).toEqual(['queued']);
    expect(flow.nodes.map((node) => node.id)).toEqual(['queued', 'starting', 'working', 'awaiting-person', 'ended']);
    expect(flow.nodes.map((node) => node.title)).toEqual(['queued', 'starting', 'working', 'waiting on you', 'ended']);
    expect(flow.label).toBe('How session s1a2b3c4 moves');
  });

  it('lights the state the record is in, and every ended state lights the one node of outcomes', () => {
    for (const state of LIVE_STATES) expect(lit(state).map((node) => node.id), state).toEqual([state]);
    for (const state of ENDED_STATES) expect(lit(state).map((node) => node.id), state).toEqual(['ended']);
  });

  it('says its own state on the lit node in its own tone, and "here now" where the node already names it (trap 8)', () => {
    expect(lit('working')[0]!.state).toEqual({ word: 'here now', tone: SESSION_TONE.working });
    // Waiting on the person is open's hue, and nobody else's.
    expect(lit('awaiting-person')[0]!.state).toEqual({ word: 'here now', tone: 'open' });
    // The group names no outcome of its own, so its lit pill says which.
    expect(lit('completed')[0]!.state).toEqual({ word: 'completed', tone: SESSION_TONE.completed });
    expect(lit('failed')[0]!.state).toEqual({ word: 'failed', tone: SESSION_TONE.failed });
    expect(lit('stood-down')[0]!.state).toEqual({ word: 'stood down', tone: SESSION_TONE['stood-down'] });
    for (const node of sessionFlow(t, session('working')).nodes.filter((each) => !each.current)) expect(node.state).toBeUndefined();
  });

  it('says the shown word the page reads over the record\'s state, in the tone that word wears', () => {
    expect(lit('working', {}, 'idle')[0]!.state).toEqual({ word: 'idle', tone: SESSION_TONE.idle });
    expect(lit('awaiting-person', { answer: 'go ahead' }, 'answered')[0]!.state).toEqual({ word: 'answered', tone: 'neutral' });
    // A quest parked on its failed sessions: the record failed, the reader says parked, in open's hue.
    expect(lit('failed', {}, 'parked')[0]!.state).toEqual({ word: 'parked', tone: 'open' });
    expect(lit('completed', {}, 'awaiting-reply')[0]!.state).toEqual({ word: 'awaiting reply', tone: 'neutral' });
    expect(lit('starting', { answer: 'go ahead' }, 'going-on')[0]!.state).toEqual({ word: 'going on', tone: 'neutral' });
  });

  it('claims no path: nothing before the current state is reached or not, and a live record has not ended (trap 19)', () => {
    const reached = (state: SessionState) => Object.fromEntries(sessionFlow(t, session(state)).nodes.map((node) => [node.id, node.reached]));
    expect(reached('working')).toEqual({ queued: undefined, starting: undefined, working: undefined, 'awaiting-person': undefined, ended: false });
    expect(reached('queued').ended).toBe(false);
    for (const state of ENDED_STATES) {
      expect(Object.values(reached(state)).every((each) => each === undefined), state).toBe(true);
    }
    // No node says who acts or when it was entered: the record's `updated` is its last write, not its entry into a state.
    for (const node of sessionFlow(t, session('working')).nodes) expect(node.actor ?? null).toBeNull();
  });

  it('draws no time: what the record last wrote moves nothing (no entry time, no since)', () => {
    const was = sessionFlow(t, session('working', { updated: '2026-10-20T11:00:00Z' }));
    const now = sessionFlow(t, session('working', { updated: '2026-10-20T11:59:00Z' }));
    expect(now).toEqual(was);
  });

  it('says every move out of a live state with who makes it', () => {
    const says = (from: string, to: string) => sessionFlow(t, session('working')).edges.find((edge) => edge.id === `${from}>${to}`)!.says;
    expect(says('queued', 'starting')).toBe('when the driver starts it');
    expect(says('starting', 'working')).toBe('when its process is running');
    expect(says('working', 'awaiting-person')).toBe('when it parks at a checkpoint only you can clear');
    expect(says('awaiting-person', 'working')).toBe('when you answer it');
    // Each live state's edge into the outcomes names what it may become, the person's stop among them (`Allowed`).
    expect(says('queued', 'ended')).toBe('when the driver stands it down, it fails, or you stop it');
    expect(says('starting', 'ended')).toBe('when the driver stands it down, it fails, or you stop it');
    expect(says('working', 'ended')).toBe('when the agent completes it, the agent declines it, the driver stands it down, it fails, or you stop it');
    expect(says('awaiting-person', 'ended')).toBe('when the agent completes it, the agent declines it, or you stop it');
  });

  it('draws the return on the same pair: awaiting-person back to working, and nothing else leaves an ended state but going on', () => {
    const edges = sessionFlow(t, session('working')).edges;
    expect(edges.filter((edge) => edge.from === 'ended').map((edge) => edge.id)).toEqual(['ended>working']);
    expect(edges.map((edge) => edge.id)).toEqual([
      'queued>starting', 'starting>working', 'working>awaiting-person', 'awaiting-person>working',
      'queued>ended', 'starting>ended', 'working>ended', 'awaiting-person>ended', 'ended>working',
    ]);
    // Every edge into the outcomes comes from a state `Allowed` lets reach one.
    const reaching = new Set(SESSION_MOVES.filter(([, to]) => !SESSION_ACTIVE.has(to as SessionState)).map(([from]) => from));
    expect(new Set(edges.filter((edge) => edge.to === 'ended').map((edge) => edge.from))).toEqual(reaching);
  });

  it('says the stop once on the outcomes, and which of them go on and which never do', () => {
    const flow = sessionFlow(t, session('completed'));
    expect(flow.members.map((member) => [member.state, member.word, member.current, member.goesOn])).toEqual([
      ['completed', 'completed', true, true],
      ['declined', 'declined', false, true],
      ['stood-down', 'stood down', false, false],
      ['failed', 'failed', false, true],
      ['stopped', 'stopped', false, true],
    ]);
    expect(flow.stop).toBe('stopped: by you, from any live state');
    expect(flow.neverGoesOn).toBe('a stand-down never goes on');
  });

  it('draws going on as a guarded edge from the outcomes, saying both guards and which may not', () => {
    const edge = sessionFlow(t, session('completed')).edges.find((each) => each.id === 'ended>working')!;
    expect(edge.style).toBe('conditional');
    expect(edge.says).toBe('when your words wait for it and its working tree is free; a stand-down never goes on');
  });

  it('draws going on faint and refused, with its reason, where the record can never make it (§2.2)', () => {
    const says = (state: SessionState, over: Partial<Session> = {}) => {
      const edge = sessionFlow(t, session(state, over)).edges.find((each) => each.id === 'ended>working')!;
      return [edge.style, edge.says];
    };
    expect(says('stood-down')).toEqual(['refused', 'it cannot go on: a stand-down has nothing to go on with']);
    expect(says('completed', { ask: 'a1' })).toEqual(['refused', 'it cannot go on: an intake is answered through its ask']);
    expect(says('completed', { opinion: 'o1' })).toEqual(['refused', 'it cannot go on: a reviewer\'s pass is read afresh each time']);
    expect(says('completed', { serves: 's9' })).toEqual(['refused', 'it cannot go on: a helper takes one turn']);
    expect(says('completed', { id: 'laptop/s1a2b3c4' })).toEqual(['refused', 'it cannot go on: a record of another machine\'s does not move here']);
    // The ledger's order: another machine's record before a stand-down.
    expect(says('stood-down', { id: 'laptop/s1a2b3c4' })[1]).toBe('it cannot go on: a record of another machine\'s does not move here');
    expect(says('failed')[0]).toBe('conditional');
  });

  it('shows a state it does not know as recorded, lit, with no move drawn to it or from it (trap 10)', () => {
    const flow = sessionFlow(t, session('paused' as SessionState));
    const unknown = flow.nodes.filter((node) => node.current);
    expect(unknown.map((node) => [node.title, node.state])).toEqual([['`paused`, shown as recorded', { word: 'paused', tone: 'neutral' }]]);
    expect(flow.edges.filter((edge) => edge.from === unknown[0]!.id || edge.to === unknown[0]!.id)).toEqual([]);
    expect(flow.nodes.find((node) => node.id === 'ended')!.reached).toBeUndefined();
  });

  it('says it all in 中文: the label, the states, the pill and the moves', async () => {
    await i18n.changeLanguage('zh');
    const flow = sessionFlow(t, session('awaiting-person'));
    expect(flow.label).toBe('会话 s1a2b3c4 如何流转');
    expect(flow.nodes.map((node) => node.title)).toEqual(['排队中', '启动中', '工作中', '等你处理', '已结束']);
    expect(flow.nodes.find((node) => node.current)!.state!.word).toBe('当前所在');
    expect(flow.edges.find((edge) => edge.id === 'awaiting-person>working')!.says).toBe('当你回答它时');
    expect(flow.edges.find((edge) => edge.id === 'working>ended')!.says).toBe('当智能体完成它、智能体谢绝它、驱动让它让位、它失败或你停止它时');
    expect(flow.stop).toBe('已停止：由你，在任何进行中的状态');
    expect(flow.neverGoesOn).toBe('让位的会话不会继续');
  });
});
