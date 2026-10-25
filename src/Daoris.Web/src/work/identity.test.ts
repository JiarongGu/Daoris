import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { HELP_REPOSITORY, type Ask, type Quest, type Session } from '../data/api';
import {
  askName, inTree, isHelp, isHelper, isPartner, linkedSessions, ownTree, questName, sessionOrigin, sessionTitle, shortened, treeName,
} from './identity';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T09:04:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  created: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
} as Quest);

/**
 * SESSUX1j (D126 §9, the session management design §6.2): a quest's name is its short title, the service's answer, else
 * its title, from a host older than the field. One helper, so no two places name one quest differently.
 */
describe("an ask's name (HEAD2e)", () => {
  const ask = (over: Partial<Ask> = {}) => ({ sentence: 'Cap the hydration.\n\nSeen on the rig.', ...over }) as Ask;

  it("is its short title where the service answers one, as a quest's is", () => {
    expect(askName(ask({ short: ' Cold-cache stall ' }))).toBe('Cold-cache stall');
  });

  it('is the first line of its sentence from an ask with none, or from a host older than the field', () => {
    expect(askName(ask())).toBe('Cap the hydration.');
    expect(askName(ask({ short: '  ' }))).toBe('Cap the hydration.');
    expect(askName(ask({ short: null }))).toBe('Cap the hydration.');
  });
});

describe("a quest's name", () => {
  it('is its short title where the service answers one', () => {
    expect(questName(quest({ short: 'Streaming budget' }))).toBe('Streaming budget');
  });

  it('is its title from a host older than the short title, or where the short title is blank', () => {
    expect(questName(quest())).toBe('Expose a streaming budget on the chunk API');
    expect(questName(quest({ short: '  ' }))).toBe('Expose a streaming budget on the chunk API');
  });

  it("names a session by it, so a session's row and its quest's row say the same words", () => {
    expect(sessionTitle(session({ quest: '7a82cc' }), quest({ short: 'Streaming budget' }))).toBe('Streaming budget');
  });

  it('says whether the name is all of the title, so a page shows the whole title once where it is not', () => {
    expect(shortened(quest({ short: 'Streaming budget' }))).toBe(true);
    expect(shortened(quest({ short: 'Expose a streaming budget on the chunk API' }))).toBe(false);
    expect(shortened(quest())).toBe(false);
    expect(shortened(null)).toBe(false);
  });
});

describe('a session title', () => {
  it('is the quest it serves, when the quest is in hand', () => {
    expect(sessionTitle(session({ quest: '7a82cc' }), quest()))
      .toBe('Expose a streaming budget on the chunk API');
  });

  it('falls back to the quest reference rather than inventing a name for it', () => {
    expect(sessionTitle(session({ quest: '7a82cc' }), null)).toBe('#7a82cc');
  });

  it('names a conversation by its kind, because a chat serves no quest by design', () => {
    expect(sessionTitle(session({ kind: 'chat' }))).toBe('Chat');
  });

  /**
   * RAIL1: a conversation's identity is its first line (design §3), from this machine's record — so
   * eight rows reading "conversation" become eight things a person can tell apart. Where the machine
   * holds no record, the kind still stands in; a quest's title still outranks it.
   */
  it('names a conversation by its first line, where this machine holds one', () => {
    expect(sessionTitle(session({ kind: 'chat' }), null, 'Read README.md and tell me its first heading.'))
      .toBe('Read README.md and tell me its first heading.');
    expect(sessionTitle(session({ kind: 'chat' }), null, null)).toBe('Chat');
    expect(sessionTitle(session({ kind: 'chat', quest: '7a82cc' }), quest(), 'anything said'))
      .toBe('Expose a streaming budget on the chunk API');
  });

  it('names a driven session with no quest by its kind too — nothing is fabricated', () => {
    expect(sessionTitle(session({ kind: 'driven' }))).toBe('Session');
  });

  it('serves the derived name in the active language', async () => {
    await i18n.changeLanguage('zh');
    expect(sessionTitle(session({ kind: 'chat' }))).toBe('聊天');
    await i18n.changeLanguage('en');
  });

  /**
   * INT4g: an intake is opened as a chat (INT4b, so every build reads it as a session nothing plans
   * from), and it is not a conversation — it serves an ask. It says so, by the ask's reference, in
   * the way a quest reference stands in for a quest not in hand.
   */
  it('names an intake by the ask it serves, never as a conversation', async () => {
    const intake = session({ kind: 'chat', repository: 'ask #0fda18', ask: '0fda18' });
    expect(sessionTitle(intake)).toBe('Intake for ask #0fda18');

    await i18n.changeLanguage('zh');
    expect(sessionTitle(intake)).toBe('需求 #0fda18 的受理会话');
    await i18n.changeLanguage('en');
  });

  /** XAGENT1g: a second opinion's reviewer is a chat on no quest only by the way it was opened; it says what it is. */
  it('names a second opinion’s reviewer as one, never as a conversation', async () => {
    const reviewer = session({ kind: 'chat', repository: 'engine', opinion: 'o1a2b3c4' });
    expect(sessionTitle(reviewer, null, 'You are reading another session\'s work.')).toBe('Second opinion');

    await i18n.changeLanguage('zh');
    expect(sessionTitle(reviewer)).toBe('第二意见');
    await i18n.changeLanguage('en');
  });

  /** MIXAGENT3a-i (the mixed-agents design §4): a helper is a chat on no quest only by the way it was opened; it says what it is. */
  it('names a helper as one, never as a conversation, and its record says it is one', async () => {
    const helper = session({ kind: 'chat', repository: 'engine', serves: 's1a2b3c4' });
    expect(isHelper(helper)).toBe(true);
    expect(isHelper(session({ kind: 'chat' }))).toBe(false);
    expect(sessionTitle(helper, null, 'You are helping another session.')).toBe('Helper');

    await i18n.changeLanguage('zh');
    expect(sessionTitle(helper)).toBe('助手');
    await i18n.changeLanguage('en');
  });

  it('prefers the quest over the kind — a chat that took one is still about that quest', () => {
    expect(sessionTitle(session({ kind: 'chat', quest: '7a82cc' }), quest()))
      .toBe('Expose a streaming budget on the chunk API');
  });
});

/**
 * FOUNDATION1c (D212; the foundation design §2.2, §2.3): the page draws a record by the work a host sends it, and by today's predicates
 * where the host sends none. The table is the shared one, `fixtures/participant-work.json` among the service's tests, which the service's
 * and the driver's suites hold row for row: what each reader answered before the work (`today`), what the page draws by the work
 * (`byWork`), and the decision for every difference. Only a partner's drawing changes, D203 point 1's reading, in a string of its own.
 */
describe('a record drawn by its work (FOUNDATION1c)', () => {
  type Marks = { mirror: boolean; kind: string | null; repository: string | null; ask: string | null; serves: string | null; piece: string | null; opinion: string | null };
  type Answers = { holdsSlot: boolean; drawn: string; connectorPartner: boolean; help: boolean };
  type Row = { name: string; marks: Marks; work: string | null; today: Answers; byWork: Answers; disagrees: Record<string, string> };
  const TABLE = JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Service', 'Daoris.Service.Tests', 'fixtures', 'participant-work.json'), 'utf8')) as {
    rows: Row[]; decisions: Record<string, string>;
  };

  /** The words each drawing says, as the page's title says them for a record on no quest with no first line. */
  const WORDS: Record<string, (ask: string | null) => string> = {
    intake: (ask) => i18n.t('work.intake.title', { ask }),
    opinion: () => i18n.t('opinion.session.title'),
    helper: () => i18n.t('work.identity.helper'),
    partner: () => i18n.t('work.identity.partner'),
    help: () => i18n.t('help.title'),
    conversation: () => i18n.t('work.identity.conversation'),
    session: () => i18n.t('work.identity.session'),
  };

  const recordOf = (marks: Marks, work?: string | null): Session => session({
    kind: (marks.kind ?? undefined) as Session['kind'], repository: marks.repository ?? 'engine',
    ask: marks.ask, serves: marks.serves, piece: marks.piece, opinion: marks.opinion,
    ...(work === undefined ? {} : { work }),
  });

  const titled = (row: Row, drawn: string) => WORDS[drawn](row.marks.ask);

  it.each(TABLE.rows.map((row) => [row.name, row] as const))(
    'draws %s by today’s predicates from a host that sends no work, as before the work', (_name, row) => {
      expect(sessionTitle(recordOf(row.marks))).toBe(titled(row, row.today.drawn));
      expect(isHelp(recordOf(row.marks))).toBe(row.today.help);
    });

  it.each(TABLE.rows.map((row) => [row.name, row] as const))(
    'draws %s by the work a host sends, the one reading the table’s byWork column names', (_name, row) => {
      expect(sessionTitle(recordOf(row.marks, row.work))).toBe(titled(row, row.byWork.drawn));
    });

  it('names every difference between the two drawings and decides it: only a partner’s drawing changes', () => {
    for (const row of TABLE.rows) {
      const differs = row.today.drawn !== row.byWork.drawn;
      expect(Object.keys(row.disagrees).includes('drawn'), row.name).toBe(differs);
      if (differs) {
        expect(row.disagrees.drawn).toBe('partner-drawn');
        expect(row.work).toBe('partner');
      }
    }
    expect(Object.keys(TABLE.decisions)).toContain('partner-drawn');
  });

  /**
   * X4-1.3: the two cases kept apart. A record from before the work, whose marks say a partner, answered by a host that has the reading
   * (so it sends `partner`), is drawn as a partner; the same record from a host before the field sends no `work`, and is drawn by today's
   * predicates, as a helper.
   */
  it('draws a partner as a partner on a host that sends work, and as a helper on one that sends none', async () => {
    const partner = { kind: 'chat' as const, serves: 's1a2b3c4', piece: 'p1a2b3c4' };

    expect(sessionTitle(session({ ...partner, work: 'partner' }))).toBe('Partner');
    expect(sessionTitle(session(partner))).toBe('Helper');
    expect(sessionTitle(session({ ...partner, work: null }))).toBe('Helper');
    expect(sessionTitle(session({ kind: 'chat', serves: 's1a2b3c4', work: 'help' }))).toBe('Helper');

    await i18n.changeLanguage('zh');
    expect(sessionTitle(session({ ...partner, work: 'partner' }))).toBe('搭档');
    expect(sessionTitle(session(partner))).toBe('助手');
    await i18n.changeLanguage('en');
  });

  /** X4-1.3, X4-2.1: the drawing changes and nothing else: a partner stays a record that serves, which the readers of that keep refusing. */
  it('keeps a partner a record that serves, whichever host drew it', () => {
    for (const work of [undefined, 'partner'] as const) {
      const partner = session({ kind: 'chat', serves: 's1a2b3c4', piece: 'p1a2b3c4', ...(work ? { work } : {}) });
      expect(isHelper(partner)).toBe(true);
      expect(isPartner(partner)).toBe(work === 'partner');
    }
  });
});

/**
 * MIXAGENT3a-i (the mixed-agents design §4): the sessions a record names as its own, the one it continues (`from`) and the one a
 * helper helps (`serves`), each where the list holds it; a link the list does not hold is null, and nothing is claimed of it.
 */
describe('the sessions a record links to', () => {
  const earlier = session({ id: 'a1b2c3d4', adapter: 'codex', kind: 'chat' });
  const asker = session({ id: 's1a2b3c4', quest: '7a82cc' });
  const byId = new Map([earlier, asker].map((row): [string, Session] => [row.id, row]));

  it('finds the session it continues and the one it helps, by the record’s own ids', () => {
    expect(linkedSessions(session({ from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris' }), byId))
      .toEqual({ continues: earlier, helps: null });
    expect(linkedSessions(session({ kind: 'chat', serves: 's1a2b3c4' }), byId)).toEqual({ continues: null, helps: asker });
  });

  it('holds nothing where the record names none, or the list does not hold the one it names', () => {
    expect(linkedSessions(session(), byId)).toEqual({ continues: null, helps: null });
    expect(linkedSessions(session({ from: 'zzzzzzzz', serves: 'yyyyyyyy' }), byId)).toEqual({ continues: null, helps: null });
  });
});

/**
 * Where a session runs (D55). The feed keys a mirrored record by `origin/id` (D47 §6), and the
 * origin is the key's own identity — whose key, on which machine. So the answer is already in the
 * record, and this is where reading it lives rather than in whichever component asked first.
 */
describe('where a session runs', () => {
  it('names the machine a mirrored record came from', () => {
    expect(sessionOrigin(session({ id: 'person@machine-a/s1a2b3c4' }))).toBe('person@machine-a');
  });

  it('answers null for a record this deployment made itself — silence means here', () => {
    expect(sessionOrigin(session({ id: 's1a2b3c4' }))).toBeNull();
  });

  it('splits on the FIRST separator, so a machine name is never cut short by a second one', () => {
    expect(sessionOrigin(session({ id: 'person@host/lab/s1a2b3c4' }))).toBe('person@host');
  });

  it('answers null rather than an empty name when the prefix is missing', () => {
    expect(sessionOrigin(session({ id: '/s1a2b3c4' }))).toBeNull();
  });
});

/**
 * A session tree's short name. Daoris owns where trees live (D51 §2), so the last segment is the
 * branch the tree was cut for — the part a person recognises, and the only part that fits a rail.
 */
describe('a tree name', () => {
  it('is the last segment of the path Daoris created', () => {
    expect(treeName('/home/p/.daoris/trees/default/engine/streaming-budget')).toBe('streaming-budget');
  });

  it('reads a Windows path too — the same tree, spelled the other way', () => {
    expect(treeName('C:\\somewhere\\.daoris\\trees\\default\\engine\\streaming-budget'))
      .toBe('streaming-budget');
  });

  it('ignores a trailing separator rather than answering with nothing', () => {
    expect(treeName('/home/p/.daoris/trees/default/engine/streaming-budget/')).toBe('streaming-budget');
  });

  /** No tree is the registered root, and a browser is told no path at all (D51 §9). Both are null. */
  it('answers null for no tree at all', () => {
    expect(treeName(null)).toBeNull();
    expect(treeName(undefined)).toBeNull();
    expect(treeName('   ')).toBeNull();
  });
});

/**
 * Found by the first real-window pass (SURF4d): the rail said `in engine` under the heading
 * `engine`, because an ordinary conversation DOES carry a tree — the registered root's own path.
 * The ledger resolves an unstated tree to the root before recording it, so "has a tree" was never
 * the question; "which tree" is.
 */
describe('a tree the session opened for itself', () => {
  const root = 'D:/checkouts/engine';

  it('is null when the session is working in the registered checkout', () => {
    expect(ownTree(session({ tree: root }), root)).toBeNull();
  });

  it('ignores separators and case, because one path can be spelled several ways', () => {
    expect(ownTree(session({ tree: 'D:\\checkouts\\Engine\\' }), root)).toBeNull();
  });

  it('names it when the session opened one for itself (D51)', () => {
    expect(ownTree(session({ tree: '/home/p/.daoris/trees/default/engine/streaming' }), root))
      .toBe('streaming');
  });

  it('claims nothing where the root is not known — a browser is told neither path', () => {
    expect(ownTree(session({ tree: '/home/p/.daoris/trees/default/engine/streaming' }), null))
      .toBeNull();
  });

  it('claims nothing when the record carries no tree at all', () => {
    expect(ownTree(session(), root)).toBeNull();
  });
});

/**
 * A harness names what it touched by absolute path, so a card read `Edit D:/…/trees/…/src/chunk.rs`
 * and truncated before the part that mattered (CONV3's look). Shown relative to the session's tree;
 * the record keeps what the wire said.
 */
describe('a path inside the session\'s tree', () => {
  const tree = 'D:\\daoris\\trees\\default\\engine\\streaming';

  it('reads relative to the tree, wherever it appears in a line', () => {
    expect(inTree('Edit D:\\daoris\\trees\\default\\engine\\streaming\\src\\chunk.rs', tree)).toBe('Edit src\\chunk.rs');
  });

  it('ignores separators and case, because one path can be spelled several ways', () => {
    expect(inTree('d:/daoris/trees/default/Engine/streaming/src/chunk.rs', tree)).toBe('src/chunk.rs');
  });

  it('leaves a path outside the tree, and a sibling that only shares its prefix, as they were', () => {
    expect(inTree('Read D:/daoris/notes.md', tree)).toBe('Read D:/daoris/notes.md');
    expect(inTree('D:/daoris/trees/default/engine/streaming-old/a.rs', tree)).toBe('D:/daoris/trees/default/engine/streaming-old/a.rs');
  });

  it('reads a POSIX tree the same way, and changes nothing with no tree in hand', () => {
    expect(inTree('Read /srv/engine/src/lib.rs', '/srv/engine/')).toBe('Read src/lib.rs');
    expect(inTree('Read /srv/engine/src/lib.rs', null)).toBe('Read /srv/engine/src/lib.rs');
  });
});

describe('the sessions of Ask Daoris (HELP1a)', () => {
  /** A twin (`twins.md`): the service's `SessionLedger.HelpRepository` and the driver's `HelpRoom.Repository`. */
  it('are recorded in a repository no folder can be called', () => {
    expect(HELP_REPOSITORY).toBe('daoris:help');
  });

  it('are told by that repository, and called Ask Daoris until the person has said something', () => {
    const help = session({ repository: HELP_REPOSITORY, kind: 'chat' });
    expect(isHelp(help)).toBe(true);
    expect(isHelp(session({ kind: 'chat' }))).toBe(false);
    expect(sessionTitle(help)).toBe(i18n.t('help.title'));
    expect(sessionTitle(help, null, 'how do I drive a repository?')).toBe('how do I drive a repository?');
  });
});
