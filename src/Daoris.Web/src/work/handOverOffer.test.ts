import { describe, expect, it } from 'vitest';
import type { AccountScope, AccountsAnswer, AgentAccounts, NextStart } from '../settings/accounts/accounts';
import type { ToolDoor } from '../agents/tools';
import { handOverOffer } from './handOverOffer';

// MIXAGENT1b-ii (D163 point 3, the mixed-agents design §5): when Daoris offers to hand a conversation over, as one pure
// answer. Cut by the account's cool-off, or near its limit by the scope's own `near` (a limit reached, usage credits or its
// number, USAGEREAD3a), and only where another present agent has an account its next start would take; an account that says
// nothing is never offered as near, and nor is one only ahead of its pace by its agent's warning (USAGEREAD3b, D165 point 2).

const now = new Date('2026-10-04T10:00:00Z');
const until = '2026-10-04T13:10:00Z';
const cooling = { until, stated: true, window: 'session', seen: '2026-10-04T09:50:00Z', assumedZone: false, notBelieved: false };

const scope = (over: Partial<AccountScope> = {}): AccountScope => ({
  workspace: null, default: null, list: [], begins: null, unknown: [], problem: null, near: [],
  use: { use: 'goal', keep: null, early: true, near: 90 }, ...over,
});
const agent = (name: string, over: Partial<AgentAccounts> = {}): AgentAccounts => ({
  agent: name, speaks: true, own: {}, accounts: [{ name: 'personal' }, { name: 'work' }], scopes: [scope()], ...over,
});
const next = (account: string | null, reason: NextStart['reason'] = 'onlyReady'): NextStart => ({ account, reason, others: [] });

const doors: ToolDoor[] = [
  { harness: 'claude-code', present: true },
  { harness: 'codex', present: true },
  { harness: 'gemini', present: true },
];
const nearPersonal = { account: 'personal', window: 'session', by: 'number' as const };
const claude = (over: Partial<AgentAccounts> = {}) => agent('claude-code', over);
const codex = (over: Partial<AgentAccounts> = {}) => agent('codex', { scopes: [scope({ next: next('team') })], ...over });
const answer = (...agents: AgentAccounts[]): AccountsAnswer => ({ agents });
const offer = (answered: AccountsAnswer, over: Partial<Parameters<typeof handOverOffer>[0]> = {}) =>
  handOverOffer({ session: { adapter: 'claude-code', profile: 'personal' }, workspace: 'default', doors, answer: answered, now, ...over });

describe('handOverOffer', () => {
  it('offers near by the scope’s own near, naming the next other agent and the account its walk would take', () => {
    const near = claude({ scopes: [scope({ near: [nearPersonal] })] });

    expect(offer(answer(near, codex()))).toEqual({ why: 'near', until: null, agent: 'codex', account: 'team' });
  });

  it('never offers near for the agent’s pace warning alone, and passes no other agent’s account for it', () => {
    const ahead = { seen: 'x', windows: [{ window: 'weekly', used: 0.6, standing: 'warned', credits: false, reset: until, seen: 'x' }] };
    const warned = claude({ accounts: [{ name: 'personal', said: ahead }, { name: 'work' }] });

    expect(offer(answer(warned, codex()))).toBeNull();
    // Another agent's next account, ahead of its pace and not near, is still one with room.
    const near = claude({ scopes: [scope({ near: [nearPersonal] })] });
    expect(offer(answer(near, codex({ accounts: [{ name: 'team', said: ahead }] }))))
      .toEqual({ why: 'near', until: null, agent: 'codex', account: 'team' });
  });

  it('offers limit while the account is cut, with when it is offered again, and the cut goes before near', () => {
    const cut = claude({ accounts: [{ name: 'personal', cooling }], scopes: [scope({ near: [nearPersonal] })] });

    expect(offer(answer(cut, codex()))).toEqual({ why: 'limit', until, agent: 'codex', account: 'team' });
  });

  it('reads a cut on the tool’s own sign-in, which is never near', () => {
    const own = claude({ own: { cooling }, scopes: [scope({ near: [nearPersonal] })] });

    expect(offer(answer(own, codex()), { session: { adapter: 'claude-code', profile: null } }))
      .toEqual({ why: 'limit', until, agent: 'codex', account: 'team' });
    expect(offer(answer(claude({ scopes: [scope({ near: [nearPersonal] })] }), codex()), { session: { adapter: 'claude-code', profile: null } }))
      .toBeNull();
  });

  it('offers nothing near for an account that says nothing, or that its agent does not speak for', () => {
    const quiet = claude({ speaks: false, scopes: [scope({ near: [nearPersonal] })] });

    expect(offer(answer(quiet, codex()))).toBeNull();
    expect(offer(answer(claude(), codex()))).toBeNull();
  });

  it('reads the workspace’s own scope where it has one, else the machine’s', () => {
    const own = claude({ scopes: [scope(), scope({ workspace: 'engine', near: [nearPersonal] })] });

    expect(offer(answer(own, codex()), { workspace: 'engine' })?.why).toBe('near');
    expect(offer(answer(own, codex()), { workspace: 'other' })).toBeNull();
  });

  it('takes the other agents in the roster’s order, the first with an account its walk would take', () => {
    const near = claude({ scopes: [scope({ near: [nearPersonal] })] });
    const waits = codex({ scopes: [scope({ next: next(null, 'waits') })] });
    const gemini = agent('gemini', { scopes: [scope({ next: next('g1') })] });

    expect(offer(answer(near, waits, gemini))).toEqual({ why: 'near', until: null, agent: 'gemini', account: 'g1' });
  });

  it('names no account for another agent’s own sign-in, and offers none when no other agent is ready', () => {
    const near = claude({ scopes: [scope({ near: [nearPersonal] })] });

    expect(offer(answer(near, codex({ scopes: [scope({ next: next(null, 'own') })] })))).toEqual(
      { why: 'near', until: null, agent: 'codex', account: null });
    expect(offer(answer(near, codex({ scopes: [scope()] })))).toBeNull();
    expect(offer(answer(near))).toBeNull();
    expect(offer(answer(near, codex()), { doors: [doors[0], { harness: 'codex', present: false }] })).toBeNull();
  });

  describe('names another tool on an account with room (MIXAGENT1b-iii)', () => {
    // The install's reading: a conversation on the protocol door, its account near at 99%, the same tool's other door next on
    // that very account, and another tool with a ready account.
    const acpDoors: ToolDoor[] = [
      { harness: 'claude-code', present: true },
      { harness: 'claude-code-acp', present: true, accountOf: 'claude-code' },
      { harness: 'codex', present: true },
    ];
    const gmailNear = { account: 'Gmail', window: 'session', by: 'number' as const };
    const claudeGmail = claude({
      accounts: [{ name: 'Gmail' }], scopes: [scope({ near: [gmailNear], next: next('Gmail', 'near') })],
    });
    const onAcp = { session: { adapter: 'claude-code-acp', profile: 'Gmail' }, doors: acpDoors };

    it('never offers the session’s own tool through its other door', () => {
      expect(offer(answer(claudeGmail, codex()), onAcp)).toEqual({ why: 'near', until: null, agent: 'codex', account: 'team' });
      expect(offer(answer(claudeGmail), onAcp)).toBeNull();
      expect(offer(answer(claudeGmail, codex({ scopes: [scope()] })), onAcp)).toBeNull();
    });

    it('offers a tool with several doors once, by its first present door', () => {
      const two: ToolDoor[] = [...acpDoors, { harness: 'codex-acp', present: true, accountOf: 'codex' }];
      expect(offer(answer(claudeGmail, codex()), { ...onAcp, doors: two })?.agent).toBe('codex');
    });

    it('passes over a next account that is near, refused or cooling, for the next ready one of that tool’s order', () => {
      const near = claude({ scopes: [scope({ near: [nearPersonal] })] });
      const held = (over: Partial<AccountScope>, accounts: AgentAccounts['accounts']) => codex({
        accounts, scopes: [scope({ near: [{ account: 'a', window: 'session', by: 'number' as const }], ...over,
          next: { account: 'a', reason: 'near', others: [{ account: 'b', hold: 'ready' }, { account: 'c', hold: 'ready' }] } })],
      });
      const refused = { seen: 'x', windows: [{ window: 'session', standing: 'refused', credits: false, reset: until, seen: 'x' }] };

      expect(offer(answer(near, held({}, [{ name: 'a' }, { name: 'b' }])))).toEqual({ why: 'near', until: null, agent: 'codex', account: 'b' });
      expect(offer(answer(near, held({ near: [] }, [{ name: 'a', said: refused }, { name: 'b' }])))?.account).toBe('b');
      expect(offer(answer(near, held({ near: [] }, [{ name: 'a', cooling }, { name: 'b', cooling }, { name: 'c' }])))?.account).toBe('c');
      expect(offer(answer(near, held({ near: [] }, [{ name: 'a', cooling }, { name: 'b', cooling }, { name: 'c', cooling }])))).toBeNull();
    });

    it('does not offer a tool whose own sign-in is cooling', () => {
      const near = claude({ scopes: [scope({ near: [nearPersonal] })] });
      expect(offer(answer(near, codex({ own: { cooling }, scopes: [scope({ next: next(null, 'own') })] })))).toBeNull();
    });
  });

  it('offers nothing from an answer an older shell gave', () => {
    expect(offer(undefined as unknown as AccountsAnswer)).toBeNull();
    expect(offer({} as AccountsAnswer)).toBeNull();
  });
});
