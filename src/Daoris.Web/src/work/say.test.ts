import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Session } from '../api';
import type { AccountsAnswer, AgentAccounts } from '../settings/accounts';
import type { ToolDoor } from '../tools';
import {
  boxOf, coolingFor, NATIVE_WORDS_LIMIT, neverSentence, newSessionSaid, reasonOf, startFromRefusal, takesWords, tooLong,
} from './say';

// The box on a session's page (MSG1f, D137 §5.1), as one pure answer: which box a session is offered, by what its record
// says and what a word said now would do (`SESSION_QUEUE`'s `reaches` and `why`, MSG1d), and the sentences for what
// takes no words and for why words did not go on.

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q1', repository: 'engine', adapter: 'claude-code-acp', state: 'working', kind: 'driven',
  created: '2026-10-03T00:00:00Z', updated: '2026-10-03T00:00:00Z', ...over,
});

const facts = (over: Partial<Parameters<typeof boxOf>[0]> = {}) =>
  ({ session: session(), here: true, intake: false, listening: false, ...over });

describe('boxOf', () => {
  it('offers nothing with nothing attended', () => {
    expect(boxOf(facts({ session: null }))).toEqual({ kind: 'none' });
  });

  /** §2.2: a teammate's record never takes words here, whatever its state; the page knows it without asking. */
  it('draws the line on a teammate’s record, whatever its state', () => {
    for (const state of ['working', 'awaiting-person', 'completed'] as const) {
      expect(boxOf(facts({ session: session({ state }), here: false }))).toEqual({ kind: 'line', why: 'teammate' });
    }
  });

  /** INT4h: a live intake's head already says why it takes no words; an ended one has only the line. */
  it('leaves a live intake to its head, and draws the line under an ended one', () => {
    const intake = { kind: 'chat' as const, quest: null, ask: '0fda18' };
    expect(boxOf(facts({ session: session({ ...intake, state: 'working' }), intake: true }))).toEqual({ kind: 'none' });
    expect(boxOf(facts({ session: session({ ...intake, state: 'completed' }), intake: true })))
      .toEqual({ kind: 'line', why: 'intake' });
  });

  /** XAGENT1g: a second opinion's reviewer takes no words, live or ended, and its line says why. */
  it('offers a second opinion’s reviewer no box, and says why once it ended', () => {
    const reviewer = { kind: 'chat' as const, quest: null, opinion: 'o1a2b3c4' };
    expect(boxOf(facts({ session: session({ ...reviewer, state: 'working' }) }))).toEqual({ kind: 'none' });
    expect(boxOf(facts({ session: session({ ...reviewer, state: 'completed' }) }))).toEqual({ kind: 'line', why: 'opinion' });
  });

  /** §5.3: where nothing takes words the module says why by a code, and the page draws the line instead of the box. */
  it('draws the line for the code the module answered', () => {
    expect(boxOf(facts({ session: session({ state: 'completed' }), reach: { reaches: null, why: 'superseded' } })))
      .toEqual({ kind: 'line', why: 'superseded' });
    expect(boxOf(facts({ session: session({ kind: 'chat', quest: null, state: 'stopped' }), reach: { reaches: null, why: 'help' } })))
      .toEqual({ kind: 'line', why: 'help' });
  });

  /** CONV4: a live chat keeps its own composer, its turns and its endings. */
  it('keeps a live chat’s own composer', () => {
    expect(boxOf(facts({ session: session({ kind: 'chat', quest: null }), reach: { reaches: 'turn-end', why: null } })))
      .toEqual({ kind: 'chat' });
  });

  /** §2.2: an ended chat goes on with words; until the module answers, its composer says it ended, as before. */
  it('offers an ended chat the box where words go on, and its ended composer until the module answers', () => {
    const ended = session({ kind: 'chat', quest: null, state: 'completed' });
    expect(boxOf(facts({ session: ended, reach: { reaches: 'resume', why: null } }))).toEqual({ kind: 'say', mode: 'goOn' });
    expect(boxOf(facts({ session: ended }))).toEqual({ kind: 'chat' });
  });

  /** D136: a working driven session whose door listens takes words at its next step or its turn's end. */
  it('offers a listening driven session the running door’s box', () => {
    expect(boxOf(facts({ listening: true, reach: { reaches: 'next-step', why: null } }))).toEqual({ kind: 'steer' });
    expect(boxOf(facts({ listening: true }))).toEqual({ kind: 'steer' });
  });

  /**
   * §5.1, D126 §3.1: on a parked session the box is the answer, before the module answers too. Answered, the box stays:
   * a second word joins the first (§2.4), and the same session goes on with them.
   */
  it('offers a parked driven session the answer, and once answered the box where the same session goes on', () => {
    const parked = session({ state: 'awaiting-person' });
    expect(boxOf(facts({ session: parked }))).toEqual({ kind: 'say', mode: 'answer' });
    expect(boxOf(facts({ session: { ...parked, answer: 'go ahead' }, reach: { reaches: 'resume', why: null } })))
      .toEqual({ kind: 'say', mode: 'goOn' });
  });

  /**
   * §2.1: words said as a session winds up are held until its record ends, then reopen it, never refused; a working
   * session no inbox here takes, and whose door the module has not answered for, is offered nothing.
   */
  it('offers a winding-up driven session the box that holds words for its end, and nothing while unknown', () => {
    expect(boxOf(facts({ reach: { reaches: 'resume', why: null } }))).toEqual({ kind: 'say', mode: 'ending' });
    expect(boxOf(facts({ reach: { reaches: null, why: null } }))).toEqual({ kind: 'none' });
    expect(boxOf(facts())).toEqual({ kind: 'none' });
  });

  /** §2.2: an ended driven session goes on with words; until the module answers, nothing is claimed. */
  it('offers an ended driven session the box where the same session goes on, once the module says so', () => {
    for (const state of ['completed', 'declined', 'failed', 'stopped'] as const) {
      expect(boxOf(facts({ session: session({ state }), reach: { reaches: 'resume', why: null } })))
        .toEqual({ kind: 'say', mode: 'goOn' });
      expect(boxOf(facts({ session: session({ state }) }))).toEqual({ kind: 'none' });
    }
  });

  it('says which boxes take words, for Send back to open', () => {
    expect([{ kind: 'chat' }, { kind: 'steer' }, { kind: 'say', mode: 'goOn' }].every((box) => takesWords(box as never))).toBe(true);
    expect([{ kind: 'none' }, { kind: 'line', why: 'teammate' }].some((box) => takesWords(box as never))).toBe(false);
  });
});

describe('neverSentence', () => {
  /** MSG1d: the codes nothing takes words for, each in the page's own words, in both catalogues. */
  it.each([
    ['teammate', 'This session ran on another machine, where its conversation is.'],
    ['stood-down', 'It stood down: #q1 is someone else\'s, so it has nothing to go on with.'],
    ['intake', 'An intake takes no words: it is answered through its ask.'],
    ['help', 'Ask Daoris\'s conversations take words in its own panel, which starts a new one.'],
    ['superseded', '#q1 went on in a later session here, so write to that one.'],
    ['not-found', 'Daoris no longer has this session\'s record, so nothing can take words for it.'],
  ])('says %s in its own sentence', (code, said) => {
    expect(neverSentence(t, code, { quest: 'q1' })).toBe(said);
    expect(neverSentence(zh, code, { quest: 'q1' })).not.toBe(said);
  });

  it('names a code it has no sentence for, rather than saying nothing', () => {
    expect(neverSentence(t, 'no-words', {})).toBe('It takes no words now (no-words).');
  });
});

describe('startFromRefusal', () => {
  /** MSG1f2: what *Start a conversation with these words* is refused by, each in the page's own words, in both catalogues. */
  it.each([
    ['running', 'It still runs, so your words reach it there; it needs no new conversation.'],
    ['no-words', 'No words wait on this session now, so there is nothing to start a conversation with.'],
    ['carried', 'Its quest is still open, so the driver carries these words on with it by itself.'],
    ['stood-down', 'It stood down: #q1 is someone else\'s, so it has nothing to go on with.'],
  ])('says %s in its own sentence', (code, said) => {
    expect(startFromRefusal(t, code, { quest: 'q1' })).toBe(said);
    expect(startFromRefusal(zh, code, { quest: 'q1' })).not.toBe(said);
  });

  it('names a code it has no sentence for, rather than saying nothing', () => {
    expect(startFromRefusal(t, 'something-newer', {})).toBe('It takes no words now (something-newer).');
  });
});

describe('reasonOf', () => {
  /** §5.1: a reason is chrome, so the page words the code, never the note's English. */
  it.each([
    ['account', 'its conversation stays with the account it ran on, and this start runs on another'],
    ['unkept', 'Daoris kept no id for its conversation'],
    ['tree', 'its tree is gone'],
    ['offered', 'the agent offers no way to continue a conversation'],
    ['gone', 'the agent no longer has its conversation'],
    ['refused', 'its conversation could not be continued'],
    ['ended', 'its record had already ended'],
    ['teammate', 'it ran on another machine, where its conversation is'],
    ['intake', 'an intake is answered through its ask'],
    ['stood-down', 'it stood down, so it has nothing to go on with'],
    ['started', 'you started a conversation with them'],
  ])('words %s', (code, said) => {
    expect(reasonOf(t, code, {})).toBe(said);
  });

  it('words a reason that names an agent only where the page knows which', () => {
    expect(reasonOf(t, 'adapter', { from: 'claude-code', to: 'claude-code-acp' }))
      .toBe('it ran on claude-code, and starts here now run on claude-code-acp');
    expect(reasonOf(t, 'adapter', { from: 'claude-code' })).toBeNull();
    expect(reasonOf(t, 'unable', { adapter: 'stub' })).toBe('stub cannot continue a conversation');
    expect(reasonOf(t, 'unable', {})).toBeNull();
    expect(reasonOf(t, 'elsewhere', { agent: 'Codex' })).toBe('its conversation is open in another client of Codex');
    expect(reasonOf(t, 'elsewhere', {})).toBeNull();
  });

  it('has no words for a code it does not know, so the driver’s own line stands', () => {
    expect(reasonOf(t, 'something-newer', {})).toBeNull();
  });
});

describe('tooLong', () => {
  /** §2.4: the native door takes the words as one argument, so its box refuses words past the bound before sending. */
  it('holds a driven session on the native door to the bound, and nothing else', () => {
    const long = 'x'.repeat(NATIVE_WORDS_LIMIT + 1);
    expect(tooLong(long, { door: 'pipe', kind: 'driven' })).toBe(true);
    expect(tooLong('x'.repeat(NATIVE_WORDS_LIMIT), { door: 'pipe', kind: 'driven' })).toBe(false);
    expect(tooLong(long, { door: 'acp', kind: 'driven' })).toBe(false);
    expect(tooLong(long, { door: 'pipe', kind: 'chat' })).toBe(false);
    expect(tooLong(long, { door: null, kind: 'driven' })).toBe(false);
  });
});

/**
 * MSG1g2 (D137 §2.2, MSG1g's note): a resume asks for the account its record ran on, so the page reads that account's
 * cool-off from the facts `ACCOUNTS` answers: by the agent that owns the door's accounts (AGT7), the tool's own sign-in
 * where the record names no account, and only while it has not passed.
 */
describe('coolingFor', () => {
  const now = new Date('2026-10-04T10:00:00Z');
  const until = '2026-10-04T13:10:00Z';
  const cooling = { until, stated: true, window: 'session', seen: '2026-10-04T09:50:00Z', assumedZone: false, notBelieved: false };
  const agent = (over: Partial<AgentAccounts> = {}): AgentAccounts => ({
    agent: 'claude-code', speaks: true, own: {}, scopes: [],
    accounts: [{ name: 'personal', cooling }, { name: 'work' }], ...over,
  });
  const answer = (...agents: AgentAccounts[]): AccountsAnswer => ({ agents });
  const doors: ToolDoor[] = [
    { harness: 'claude-code', present: true },
    { harness: 'claude-code-acp', present: true, accountOf: 'claude-code' },
  ];

  it('reads the cool-off of the account the record ran on, by its own door or a door onto the same tool', () => {
    expect(coolingFor({ adapter: 'claude-code', profile: 'personal' }, doors, answer(agent()), now)).toBe(until);
    expect(coolingFor({ adapter: 'claude-code-acp', profile: 'personal' }, doors, answer(agent()), now)).toBe(until);
  });

  it('reads the tool’s own sign-in where the record names no account', () => {
    const own = agent({ own: { cooling } });
    expect(coolingFor({ adapter: 'claude-code', profile: null }, doors, answer(own), now)).toBe(until);
    expect(coolingFor({ adapter: 'claude-code', profile: null }, doors, answer(agent()), now)).toBeNull();
  });

  it('says nothing cools for a ready account, a passed cool-off, another agent, or an answer an older shell gave', () => {
    expect(coolingFor({ adapter: 'claude-code', profile: 'work' }, doors, answer(agent()), now)).toBeNull();
    expect(coolingFor({ adapter: 'claude-code', profile: 'personal' }, doors, answer(agent()), new Date('2026-10-04T14:00:00Z')))
      .toBeNull();
    expect(coolingFor({ adapter: 'codex', profile: 'personal' }, doors, answer(agent()), now)).toBeNull();
    expect(coolingFor({ adapter: 'claude-code', profile: 'personal' }, doors, undefined, now)).toBeNull();
    expect(coolingFor({ adapter: 'claude-code', profile: 'personal' }, doors, {} as AccountsAnswer, now)).toBeNull();
  });
});

/**
 * MSG1g2 (D137 §2.2, MSG1g's note): what *Go on in a new session* came to, in the page's own words: kept, or each code
 * `SESSION_GO_ON_NEW` refuses by, in both catalogues; and *Start a conversation with these words* the door where nothing
 * carries the words on by itself.
 */
describe('newSessionSaid', () => {
  const said = (why: string | null, sent = false, language = t) =>
    newSessionSaid(language, { sent, why, message: 'the driver’s own sentence' }, { quest: 'q1' });

  it('says the choice is kept, and that the new session starts without this conversation', () => {
    expect(said(null, true)).toEqual({
      sentence: 'A new session takes your words at the driver\'s next look, without this conversation\'s context.',
      startChat: false,
    });
  });

  it.each([
    ['running', 'It is still running, so your words reach it there; it needs no new session.', false],
    ['no-words', 'No words of yours wait on it to go on with.', false],
    ['conversation', 'A conversation does not go on in a new session by itself; start a conversation with these words instead.', true],
    ['closed', '#q1 has closed, so nothing carries its words on by itself; start a conversation with them instead.', true],
    ['not-cooling', 'Its account is not cooling any more, so the same session goes on with your words at the driver\'s next look.', false],
    ['teammate', 'This session ran on another machine, where its conversation is.', false],
    ['stood-down', 'It stood down: #q1 is someone else\'s, so it has nothing to go on with.', false],
  ])('words %s, and offers a conversation where nothing carries the words on', (why, sentence, startChat) => {
    expect(said(why)).toEqual({ sentence, startChat });
  });

  /** Every code the route refuses by has a sentence of the page's own, in each catalogue. */
  it('has a sentence of its own for every code the route refuses by, in both languages', () => {
    const codes = [
      'not-found', 'teammate', 'help', 'intake', 'stood-down', 'superseded',
      'running', 'no-words', 'conversation', 'closed', 'not-cooling',
    ];
    for (const language of [t, zh]) {
      const sentences = codes.map((why) => said(why, false, language).sentence);
      expect(sentences.filter((sentence) => sentence === 'the driver’s own sentence' || /[a-z]+\.[a-z]+\./.test(sentence))).toEqual([]);
      expect(new Set(sentences).size).toBe(codes.length);
    }
    expect(said('closed', false, zh).sentence).toBe('#q1 已关闭，没有什么会自行接着处理它的话；请改为用这些话开始对话。');
  });

  it('passes the driver’s own sentence through for a choice it could not keep, or a code this page does not know', () => {
    expect(said(null)).toEqual({ sentence: 'the driver’s own sentence', startChat: false });
    expect(said('something-newer')).toEqual({ sentence: 'the driver’s own sentence', startChat: false });
  });
});
