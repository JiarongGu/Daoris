import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { moment } from '../locales/format';
import { linesTaken, liveParts, NOTE_CODES, noteBlocks, noteLines, noteText } from './noteLines';

// A session's note as the page words it (LANG1b, D142 points 1, 4, 5; the language design §2, §5, §6): a coded part from
// the catalogue in the reader's language with its values, someone's words as written, and what the page cannot word as
// recorded. Pure, so every part a writer could send is an argument.

const t = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

const coded = (code: string, text: string, values: Record<string, unknown> = {}): NotePart => ({ code, values, text });
const words = (said: string, by: string): NotePart => ({ words: said, by });

describe('noteLines', () => {
  /** §2: Daoris's line is chrome, worded from its code and values in the reader's language, never from its English. */
  it('words a coded part in the reader’s language, with its values', () => {
    const parts = [coded('ended.taken-exit', 'exit 2 with the quest still taken.', { exit: 2 })];
    expect(noteLines(t, { note: 'exit 2 with the quest still taken.', parts }, 'en'))
      .toEqual([{ kind: 'said', text: 'Exit 2 with the quest still taken.' }]);
    expect(noteLines(zh, { note: 'exit 2 with the quest still taken.', parts }, 'zh'))
      .toEqual([{ kind: 'said', text: '退出码 2，委托仍已接下。' }]);
  });

  /** §2: a split note — the lead-in is a code, the agent's question beneath it a part of its own, never in a catalogue. */
  it('keeps someone’s words as written, beneath the line that leads into them', () => {
    const question = 'Which branch should the release land on?\n\n- main\n- release/1.0';
    const parts = [
      coded('ended.parked-asked', 'It stopped with its quest still taken, to ask you:'),
      words(question, 'agent'),
      coded('ledger.answered', 'Answered:'),
      words('main', 'person'),
    ];

    expect(noteLines(zh, { note: 'ignored', parts }, 'zh')).toEqual([
      { kind: 'said', text: '它停了下来，委托仍已接下，想问你：' },
      { kind: 'words', text: question, by: 'agent' },
      { kind: 'said', text: '已答复：' },
      { kind: 'words', text: 'main', by: 'person' },
    ]);
  });

  /** §5: a code the page does not know — a newer driver's — shows its own English, marked; nothing is invented. */
  it('shows a part it cannot word as recorded: an unknown code, or a value its sentence needs that is absent', () => {
    expect(noteLines(zh, { parts: [coded('ended.brand-new', 'It did something new.')] }, 'zh'))
      .toEqual([{ kind: 'recorded', text: 'It did something new.' }]);
    expect(noteLines(zh, { parts: [coded('ended.timeout', 'Timed out after 30 minutes and was killed.')] }, 'zh'))
      .toEqual([{ kind: 'recorded', text: 'Timed out after 30 minutes and was killed.' }]);
    // A value of the wrong shape is as absent as a missing one: a moment that is no moment.
    expect(noteLines(t, { parts: [coded('ledger.went-on', 'Went on with your words at noon.', { at: 'noon' })] }, 'en'))
      .toEqual([{ kind: 'recorded', text: 'Went on with your words at noon.' }]);
    // With no text either, there is nothing to say, and nothing is said.
    expect(noteLines(t, { parts: [{ code: 'ended.brand-new', values: {} }] }, 'en')).toEqual([]);
  });

  /** §6: a record from before parts shows its note as kept, marked; no pattern table re-reads its English. */
  it('shows a record from before parts as it was kept', () => {
    const note = 'the quest reached done.';
    expect(noteLines(zh, { note, parts: null }, 'zh')).toEqual([{ kind: 'recorded', text: note }]);
    expect(noteLines(zh, { note, parts: [] }, 'zh')).toEqual([{ kind: 'recorded', text: note }]);
    expect(noteLines(zh, { note: null, parts: null }, 'zh')).toEqual([]);
    // Carried whole into a newer note as one part, it is the same English, and shown the same way.
    expect(noteLines(zh, {
      parts: [words(note, 'before'), coded('working.goes-on', 'It goes on with your words in its own conversation.')],
    }, 'zh')).toEqual([
      { kind: 'recorded', text: note },
      { kind: 'said', text: '它带着你的话在自己的对话中继续，在它原来的工作树中。' },
    ]);
  });

  /**
   * WAITSHOWN1: a session that is working again carries its last end in its note (`ended.went-on`, with the ledger's
   * `ledger.went-on` before it), which says it ended while it works. The page reads past those; the record keeps them.
   */
  it('leaves out what a live session’s past ending and going on said, and keeps the rest', () => {
    const parts = [
      coded('ledger.went-on', 'Went on with your words at noon.', { at: '2026-10-03T09:30:00Z' }),
      coded('ended.went-on', 'It went on with your words and ended; its quest stays as it closed.'),
      coded('working.goes-on', 'It goes on with your words in its own conversation.'),
    ];
    expect(liveParts(parts)?.map((part) => part.code)).toEqual(['working.goes-on']);
    expect(noteLines(t, { parts: liveParts(parts) }, 'en')).toHaveLength(1);
    // The record is not changed, and a note with no parts or no such part passes as it was.
    expect(parts).toHaveLength(3);
    expect(liveParts(null)).toBeNull();
    expect(liveParts([words('hello', 'person')])).toEqual([words('hello', 'person')]);
  });

  /** §4: a list of quests joined in the reader's language, each an id as the page draws one. */
  it('joins a list of quests the reader’s way', () => {
    const parts = [coded('intake.published', 'published `#a1`, `#b2` onto ask `#0f`.', { quests: ['a1', 'b2', 'c3'], ask: '0f' })];
    expect(noteLines(t, { parts }, 'en')).toEqual([{ kind: 'said', text: 'Published #a1, #b2, and #c3 onto ask #0f.' }]);
    expect(noteLines(zh, { parts }, 'zh')).toEqual([{ kind: 'said', text: '已在需求 #0f 下发布 #a1、#b2和#c3。' }]);
  });

  /** §4: a moment in the reader's language and zone, never the record's ISO string. */
  it('says a moment the way the reader’s language writes it', () => {
    const parts = [coded('ledger.went-on', 'Went on with your words at 2026-10-03 09:30 UTC.', { at: '2026-10-03T09:30:00Z' })];
    const [line] = noteLines(t, { parts }, 'en');
    expect(line.kind).toBe('said');
    expect(line.text).toMatch(/^Went on with your words at Oct \d+, \d\d:\d\d .+\.$/);
    expect(line.text).not.toContain('2026-10-03T');
  });

  /** §4: a `why` is a reason's code, worded by MSG1f's one wording per reason, with that reason's own values. */
  it('words a reason by its code and its own values, and falls back where it names what the page was not told', () => {
    const fell = coded('started.fell-back', 'A new session, because its tree is gone.', { why: 'tree' });
    expect(noteLines(zh, { parts: [fell] }, 'zh')).toEqual([{ kind: 'said', text: '改为新会话，因为它的工作树已不在。' }]);

    const adapter = coded('went.new-session', 'Your words went to a new session, because …', { why: 'adapter', from: 'claude-code', to: 'codex-acp' });
    expect(noteLines(t, { parts: [adapter] }, 'en')).toEqual([
      { kind: 'said', text: 'Your words went to a new session, because it ran on claude-code, and starts here now run on codex-acp.' },
    ]);

    // `went.cannot` is the sentence the conversation already says (`work.say.cannot`).
    const cannot = coded('went.cannot', 'It cannot go on in this session, because …', { why: 'ended' });
    expect(noteLines(t, { parts: [cannot] }, 'en'))
      .toEqual([{ kind: 'said', text: 'It cannot go on in this session, because its record had already ended.' }]);

    const unknown = coded('went.carried-on', 'Carried on in a new session, because of a newer reason.', { why: 'newer' });
    expect(noteLines(zh, { parts: [unknown] }, 'zh'))
      .toEqual([{ kind: 'recorded', text: 'Carried on in a new session, because of a newer reason.' }]);
    const unnamed = coded('went.carried-on', 'Carried on in a new session, because …', { why: 'adapter', from: 'claude-code' });
    expect(noteLines(zh, { parts: [unnamed] }, 'zh')[0].kind).toBe('recorded');
  });

  it('words a cool-off’s reason by its own family', () => {
    const cooling = coded('account.cooling-no-window', 'The `claude-code` account it ran on is cooling until …', {
      until: '2026-10-03T16:00:00Z', why: 'default', owner: 'claude-code',
    });
    const [line] = noteLines(t, { parts: [cooling] }, 'en');
    expect(line).toMatchObject({ kind: 'said' });
    expect(line.text).toMatch(/^The claude-code account it ran on is cooling until .+ \(Daoris's default: the agent named no time\); nothing starts on it until then\.$/);
  });

  /**
   * UX7d-1 (D152's UX7d-1 note): a conversation's first line, worded by its codes: the account it opened on with the step
   * that chose it, a reason with its own values (a percent, a window, a moment, another account), the rest's step, a refused
   * turn, and what each account said, a line per account and per thing it said. The record's English is never read.
   */
  describe('a conversation’s first line', () => {
    const seen = '2026-10-02T11:50:00Z';
    const carried = [
      coded('opening.carried-on', 'carried on from session `s0` on `account-2`; …', {
        session: 's0', account: 'account-2', why: 'near-used', over: 'account-1', used: 95, window: 'session', near: 90,
      }),
      coded('opening.rest', 'of the rest, Daoris started on it least recently', { why: 'least-recent' }),
      coded('opening.turn-context', 'its turn 3 was refused with 370,104 tokens of context', { turn: 3, tokens: 370104 }),
      coded('opening.said-at', '`account-1` 10 min ago', { account: 'account-1', seen }),
      coded('opening.said-used', '95% of its session limit', { used: 95, window: 'session' }),
      coded('opening.said-near-at', 'near at 90%', { near: 90 }),
      coded('opening.said-nothing', '`account-2` nothing yet', { account: 'account-2' }),
    ];

    it('words each line by its code in English and 中文', () => {
      expect(noteLines(t, { parts: carried }, 'en')).toEqual([
        'Carried on from session `s0` on `account-2`: `account-1` has used 95% of its five-hour limit, at or over the 90% that counts as near.',
        'Of the rest, Daoris started on it least recently.',
        'Its turn 3 was refused with 370,104 tokens of context.',
        `\`account-1\` last said what it has left at ${moment(seen, 'en')}.`,
        'It had used 95% of its five-hour limit.',
        'That is at or over the 90% that counts as near.',
        '`account-2` has said nothing yet.',
      ].map((text) => ({ kind: 'said', text })));
      expect(noteLines(zh, { parts: carried }, 'zh')).toEqual([
        '在账户 `account-2` 上接续会话 `s0`：`account-1` 的5 小时上限已用 95%，达到或超过视为接近的 90%。',
        '其余账户中，Daoris 最久没有在它上面启动。',
        '它的第 3 轮被拒绝，当时上下文有 370,104 个 token。',
        `\`account-1\` 最近一次说明剩余用量是在 ${moment(seen, 'zh')}。`,
        '它的5 小时上限已用 95%。',
        '这已达到或超过视为接近的 90%。',
        '`account-2` 还没有说明剩余用量。',
      ].map((text) => ({ kind: 'said', text })));
    });

    /** LOCKPASSED1: an account passed for its lock is a reason of its own, said in both languages. */
    it('words an account passed for its lock by its own reason', () => {
      const locked = [
        coded('opening.opened', 'opened on `account-2`: …', { account: 'account-2', why: 'locked', agent: 'claude-code', over: 'account-1' }),
      ];
      expect(noteLines(t, { parts: locked }, 'en')).toEqual([
        { kind: 'said', text: 'Opened on `account-2`: the `claude-code` account `account-1` is being asked about by another question.' },
      ]);
      expect(noteLines(zh, { parts: locked }, 'zh').every((line) => line.kind === 'said')).toBe(true);
    });

    /** USAGEPROBE1: a window's use projected from its reading and Daoris's own turns since is a line of its own, said as projected. */
    it('words a projected use as projected, apart from what the account said', () => {
      const projected = [
        coded('opening.said-projected', 'about 85% of its session limit by now, projected from Daoris’s turns since', { projected: 85, window: 'session' }),
      ];
      expect(noteLines(t, { parts: projected }, 'en')).toEqual([
        { kind: 'said', text: 'Projected from Daoris\'s own turns since, about 85% of its five-hour limit is used by now.' },
      ]);
      expect(noteLines(zh, { parts: projected }, 'zh')).toEqual([
        { kind: 'said', text: '按 Daoris 此后自己的轮次推算，它的5 小时上限此刻约已用 85%。' },
      ]);
    });

    /**
     * USAGEREAD3a: the agent's pace warning is its own line, never near; a line written before, `opening.said-near`, and the
     * step it named, `near-word`, keep being worded as they were.
     */
    it('words the agent’s pace warning as its own line, and keeps a near line written before, in either language', () => {
      const warned = [
        coded('opening.said-at', '`account-1` 10 min ago', { account: 'account-1', seen }),
        coded('opening.said-warned', 'ahead of its pace on its weekly window, by its own word', { window: 'weekly' }),
      ];
      const before = [
        coded('opening.opened', 'opened on `account-2`: …', { account: 'account-2', why: 'near-word', over: 'account-1', window: 'session' }),
        coded('opening.said-near', 'near its session limit, by its own word', { window: 'session' }),
      ];

      expect(noteLines(t, { parts: warned }, 'en')[1]).toEqual({ kind: 'said', text: 'It said it is ahead of its pace on its weekly window.' });
      expect(noteLines(zh, { parts: warned }, 'zh')[1]).toEqual({ kind: 'said', text: '它说每周的用量超前于节奏。' });
      expect(noteLines(t, { parts: before }, 'en').map((line) => line.text)).toEqual([
        'Opened on `account-2`: `account-1` said it is near its five-hour limit.',
        'It said it is near its five-hour limit.',
      ]);
      expect(noteLines(zh, { parts: before }, 'zh')[1]).toEqual({ kind: 'said', text: '它说接近5 小时上限。' });
    });

    /**
     * SWITCH1b-ii (D165 point 2): an account passed over its curve says the window, its use and the curve's value now; the
     * account with the most room under its curve says its room on its tightest window. A line written before by the scope's
     * one number (`near-used`, `opening.said-near-at`) keeps being worded as it was, as do the pace's under `off`.
     */
    it('words over its curve and the most room under it, in English and 中文', () => {
      const over = [
        coded('opening.opened', 'opened on `account-2`: …', {
          account: 'account-2', why: 'over-curve', over: 'account-1', window: 'weekly', used: 70, curve: 63,
        }),
        coded('opening.said-at', '`account-1` 10 min ago', { account: 'account-1', seen }),
        coded('opening.said-used', '70% of its weekly limit', { used: 70, window: 'weekly' }),
        coded('opening.said-over', "over its weekly curve's 63%", { window: 'weekly', curve: 63 }),
      ];
      const room = coded('opening.opened', 'opened on `account-3`: …', { account: 'account-3', why: 'most-room', room: 53, window: 'weekly' });

      expect(noteLines(t, { parts: over }, 'en').map((line) => line.text)).toEqual([
        "Opened on `account-2`: `account-1` has used 70% of its weekly limit, over its curve's 63%.",
        `\`account-1\` last said what it has left at ${moment(seen, 'en')}.`,
        'It had used 70% of its weekly limit.',
        "That is over its weekly curve's 63%.",
      ]);
      expect(noteLines(zh, { parts: over }, 'zh').map((line) => line.text)).toEqual([
        '在账户 `account-2` 上开始：`account-1` 的每周上限已用 70%，超出其曲线此刻的 63%。',
        `\`account-1\` 最近一次说明剩余用量是在 ${moment(seen, 'zh')}。`,
        '它的每周上限已用 70%。',
        '这超出了其每周曲线此刻的 63%。',
      ]);
      expect(noteLines(t, { parts: [room] }, 'en')[0].text)
        .toBe('Opened on `account-3`: it has the most room under its curve, 53 points on its weekly window.');
      expect(noteLines(zh, { parts: [room] }, 'zh')[0].text).toBe('在账户 `account-3` 上开始：它在曲线下的余量最多：每周窗口还有 53 个百分点。');
      // A value the reason needs that is absent: the line is shown as recorded.
      const short = coded('opening.opened', 'opened on `account-3`: it has the most room', { account: 'account-3', why: 'most-room', window: 'weekly' });
      expect(noteLines(t, { parts: [short] }, 'en')[0]).toEqual({ kind: 'recorded', text: 'opened on `account-3`: it has the most room' });
    });

    /**
     * USAGERESET1 (D165's note): step 4 says what would lapse, the window that won, its unused share, its reset and the moment
     * its reading was seen. A line written before by its reset alone (`lapsing`) keeps being worded as it was.
     */
    it('words what would lapse at a week’s reset, in English and 中文, and the reset alone as before', () => {
      const lapses = coded('opening.opened', 'opened on `account-3`: …', {
        account: 'account-3', why: 'lapses', window: 'weekly', unused: 40, at: '2026-10-03T08:00:00Z', seen: '2026-10-02T11:50:00Z',
      });
      expect(noteLines(t, { parts: [lapses] }, 'en')[0].text).toBe(
        `Opened on \`account-3\`: its weekly window resets at ${moment('2026-10-03T08:00:00Z', 'en')} with 40% of it unused, `
        + `as read at ${moment('2026-10-02T11:50:00Z', 'en')}.`,
      );
      expect(noteLines(zh, { parts: [lapses] }, 'zh')[0].text).toBe(
        `在账户 \`account-3\` 上开始：它的每周窗口在 ${moment('2026-10-03T08:00:00Z', 'zh')} 重置，`
        + `按 ${moment('2026-10-02T11:50:00Z', 'zh')} 的读数还有 40% 未用。`,
      );
      const before = coded('opening.opened', 'opened on `account-3`: …', { account: 'account-3', why: 'lapsing', at: '2026-10-03T08:00:00Z' });
      expect(noteLines(t, { parts: [before] }, 'en')[0].text)
        .toBe(`Opened on \`account-3\`: its week resets first, at ${moment('2026-10-03T08:00:00Z', 'en')}.`);
    });

    it('words a passed account’s cool-off with its reset and its own reason, and a workspace’s list by name', () => {
      const cooling = coded('opening.opened', 'opened on `account-2`: …', {
        account: 'account-2', why: 'cooling', agent: 'claude-code', over: 'account-1', until: '2026-10-03T18:00:00Z', cooling: 'stated',
      });
      expect(noteLines(t, { parts: [cooling] }, 'en')).toEqual([{
        kind: 'said',
        text: `Opened on \`account-2\`: the \`claude-code\` account \`account-1\` is cooling until ${moment('2026-10-03T18:00:00Z', 'en')} (the agent said so).`,
      }]);
      const first = coded('opening.opened', 'opened on `account-1`: …', { account: 'account-1', why: 'first', workspace: 'work' });
      expect(noteLines(zh, { parts: [first, coded('opening.unsaid', 'No account has said what it has left yet.')] }, 'zh').map((line) => line.text))
        .toEqual(['在账户 `account-1` 上开始：它在 `work` 的列表中排第一。', '尚无账户说明它还剩多少用量。']);
      const paced = coded('opening.opened', 'opened on `account-2`: …', { account: 'account-2', why: 'behind', used: 10, gone: 43 });
      expect(noteLines(t, { parts: [paced] }, 'en')[0].text)
        .toBe("Opened on `account-2`: it is furthest behind its week's pace, 10% of its weekly limit used with 43% of its week gone.");
    });

    /** NOTENAME1: an account id is worded by the name the person gave it, and an account the roster lacks keeps its id. */
    it('words an account by the name the person gave it, and keeps the id of one the roster does not know', () => {
      const names = new Map([['account-1', 'personal'], ['account-2', 'team']]);
      expect(noteLines(t, { parts: carried }, 'en', names).map((line) => line.text)).toEqual([
        'Carried on from session `s0` on `team`: `personal` has used 95% of its five-hour limit, at or over the 90% that counts as near.',
        'Of the rest, Daoris started on it least recently.',
        'Its turn 3 was refused with 370,104 tokens of context.',
        `\`personal\` last said what it has left at ${moment(seen, 'en')}.`,
        'It had used 95% of its five-hour limit.',
        'That is at or over the 90% that counts as near.',
        '`team` has said nothing yet.',
      ]);
      expect(noteLines(zh, { parts: carried }, 'zh', new Map([['account-2', '团队']])).map((line) => line.text).slice(0, 1))
        .toEqual(['在账户 `团队` 上接续会话 `s0`：`account-1` 的5 小时上限已用 95%，达到或超过视为接近的 90%。']);
      const waits = coded('waits.account', 'the `claude-code` account `account-1` is cooling', {
        owner: 'claude-code', account: 'account-1', until: '2026-10-03T18:00:00Z', why: 'stated',
      });
      expect(noteLines(t, { parts: [waits] }, 'en', names)[0].text).toContain('`claude-code` account `personal`');
      expect(noteText(t, { parts: [waits] }, 'en', names)).toContain('`personal`');
    });

    it('shows a line as recorded where its step is a reason it does not know or lacks a value its reason says', () => {
      const newer = coded('opening.opened', 'opened on `account-2`: a newer step.', { account: 'account-2', why: 'newer' });
      const short = coded('opening.opened', 'opened on `account-2`: `account-1` is ahead.', { account: 'account-2', why: 'ahead', over: 'account-1', used: 80 });
      const shaped = coded('opening.said-used', '95% of its session limit', { used: '95%', window: 'session' });
      expect(noteLines(zh, { parts: [newer, short, shaped] }, 'zh')).toEqual([
        { kind: 'recorded', text: 'opened on `account-2`: a newer step.' },
        { kind: 'recorded', text: 'opened on `account-2`: `account-1` is ahead.' },
        { kind: 'recorded', text: '95% of its session limit' },
      ]);
    });
  });

  /** CONVNOTE2a: a resumed conversation's first line is worded in either language, a version that moved a line beside it. */
  describe('a resumed conversation’s first line', () => {
    const lead = (why: string) => coded('resumed.opening', '— … resumed on `claude-code`', { adapter: 'claude-code', why });
    const moved = coded('resumed.moved', '2.1.300; it opened on 2.1.281', { now: '2.1.300', then: '2.1.281' });

    it('is the English it has always been, and the Chinese beside it', () => {
      expect(noteLines(t, { parts: [lead('answer')] }, 'en')[0].text)
        .toBe('— your answer is the next turn of its own conversation, resumed on `claude-code`.');
      expect(noteLines(t, { parts: [lead('words-and-findings'), moved] }, 'en').map((line) => line.text)).toEqual([
        "— your words and another agent's findings are the next turn of its own conversation, resumed on `claude-code`.",
        'It is on version 2.1.300 now; it opened on 2.1.281.',
      ]);
      expect(noteLines(zh, { parts: [lead('findings'), moved] }, 'zh').map((line) => line.text)).toEqual([
        '—— 另一个智能体的发现是其自身对话的下一轮，已在 `claude-code` 上恢复。',
        '现为版本 2.1.300；开始时为 2.1.281。',
      ]);
      expect(noteLines(zh, { parts: [coded('resumed.native', '…', { adapter: 'claude-code' })] }, 'zh')[0].text)
        .toBe('—— 你在它工作时说的话是其自身对话的下一轮，已在 `claude-code` 上恢复。');
    });

    it('shows a way of saying it does not know as recorded', () => {
      expect(noteLines(zh, { parts: [lead('newer')] }, 'zh')).toEqual([{ kind: 'recorded', text: '— … resumed on `claude-code`' }]);
    });
  });

  /** CONVNOTE2b: a refused permission's lead and close are worded, the call's title between them the agent's words as written. */
  describe('a refused permission', () => {
    const parts = [
      coded('refused.lead', 'permission refused:'),
      words('`git push origin release`', 'agent'),
      coded('refused.governs', '— the rules it runs under govern, and the driver may not widen them'),
    ];

    it('is worded in either language, the title between them as written', () => {
      expect(noteLines(t, { parts }, 'en')).toEqual([
        { kind: 'said', text: 'Permission refused:' },
        { kind: 'words', text: '`git push origin release`', by: 'agent' },
        { kind: 'said', text: '— the rules it runs under govern, and the driver may not widen them.' },
      ]);
      expect(noteLines(zh, { parts }, 'zh')).toEqual([
        { kind: 'said', text: '权限被拒绝：' },
        { kind: 'words', text: '`git push origin release`', by: 'agent' },
        { kind: 'said', text: '—— 它所遵循的规则说了算，驱动不得放宽。' },
      ]);
    });
  });

  /**
   * AUTONOMY1b3c (D184's AUTONOMY1b3a note): a permission held for the person under asking, and how it ended, worded around the
   * call's title as the agent wrote it; the bound's minutes a fact.
   */
  describe('a permission held for the person', () => {
    const title = words('`npm test`', 'agent');

    it('says it waits, and that the person allowed it', () => {
      expect(noteLines(t, { parts: [coded('asking.held', 'permission waiting for the person:'), title] }, 'en')).toEqual([
        { kind: 'said', text: 'Permission waiting for your answer:' },
        { kind: 'words', text: '`npm test`', by: 'agent' },
      ]);
      expect(noteLines(zh, { parts: [coded('asking.allowed', 'permission allowed by the person:'), title] }, 'zh')).toEqual([
        { kind: 'said', text: '你已允许权限：' },
        { kind: 'words', text: '`npm test`', by: 'agent' },
      ]);
    });

    it('says who or what refused it, in either language', () => {
      const refused = (code: string, values?: Record<string, unknown>) =>
        [coded('refused.lead', 'permission refused:'), title, coded(code, '—', values)];
      expect(noteLines(t, { parts: refused('asking.denied') }, 'en').at(-1)).toEqual({ kind: 'said', text: '— you refused it.' });
      expect(noteLines(t, { parts: refused('asking.cancelled') }, 'en').at(-1)?.text).toBe('— the turn was stopped before you answered.');
      expect(noteLines(t, { parts: refused('asking.ended') }, 'en').at(-1)?.text).toBe('— the session ended before you answered.');
      expect(noteLines(t, { parts: refused('asking.bound', { minutes: 15 }) }, 'en').at(-1)?.text)
        .toBe('— nobody answered within 15 minutes, and a turn is never left waiting for ever.');
      expect(noteLines(zh, { parts: refused('asking.denied') }, 'zh').at(-1)?.text).toBe('—— 你拒绝了它。');
      expect(noteLines(zh, { parts: refused('asking.bound', { minutes: 15 }) }, 'zh').at(-1)?.text)
        .toBe('—— 15 分钟内无人答复，而一轮不会无限期等待。');
    });
  });

  /** CONVNOTE2c: a press's note, a chat's setting and where the person is, and a browser withheld, worded; others' words as written. */
  describe('a press, a chat’s settings and a browser withheld', () => {
    it('words a press by its lead and leaves the landing’s sentence as written', () => {
      const parts = [coded('landing.person-accepted', 'the person accepted this work:'), words('merged `daoris/s4` into `main`.', 'program')];
      expect(noteLines(t, { parts }, 'en')).toEqual([
        { kind: 'said', text: 'You accepted this work:' },
        { kind: 'words', text: 'merged `daoris/s4` into `main`.', by: 'program' },
      ]);
      expect(noteLines(zh, { parts: [coded('landing.person-handed', 'the person handed this work on:')] }, 'zh'))
        .toEqual([{ kind: 'said', text: '你已将这项工作移交出去：' }]);
    });

    it('words an option the person set and where they were told they are', () => {
      const set = coded('chat.option-set', 'the person set Model to Haiku for this conversation.', { option: 'Model', chosen: 'Haiku' });
      expect(noteLines(t, { parts: [set] }, 'en')[0].text).toBe('You set Model to Haiku for this conversation.');
      expect(noteLines(zh, { parts: [set] }, 'zh')[0].text).toBe('你已将这次对话的 Model 设为 Haiku。');
      expect(noteLines(zh, { parts: [coded('chat.option-set', 'the person set …', { option: 'Model' })] }, 'zh')[0].kind).toBe('recorded');
      expect(noteLines(zh, { parts: [coded('chat.told-where', 'told where the person is:'), words('Sessions', 'program')] }, 'zh')).toEqual([
        { kind: 'said', text: '已告知智能体你所在的位置：' },
        { kind: 'words', text: 'Sessions', by: 'program' },
      ]);
    });

    it("words a merge the access setup's landing rule leaves waiting for Accept, with the level that set it (ACCESSWORDS1)", () => {
      const merges = (access: string) => coded('landing.merges', 'its quest was done, …', { access });
      expect(noteLines(t, { parts: [merges('machine')] }, 'en')).toEqual([
        { kind: 'said', text: "Its quest was done, and this machine's access setup lands work by its rule, but the rule merges into the line, and a merge never lands by itself: its work waits for your Accept…" },
      ]);
      expect(noteLines(t, { parts: [merges('repository')] }, 'en')[0].text).toContain("its repository's access setup lands");
      expect(noteLines(zh, { parts: [merges('workspace')] }, 'zh')[0].text)
        .toBe('委托已完成，它所在工作区的访问配置按落地规则落地工作，但这条规则是合并进主线，而合并从不自动落地：它的工作等你采纳…');
      // A level this page does not know shows as recorded rather than guessed.
      expect(noteLines(t, { parts: [merges('elsewhere')] }, 'en')[0].kind).toBe('recorded');
    });

    it('words the process a conversation was told by its commit rule and where it was set, under its own lead', () => {
      // PROCESS1d-ii: never the browser's lead; the words after it are Daoris's English to the agent, shown as written.
      const told = (source: string) => coded('chat.told-process', "told the agent this repository's process, …:", { mode: 'ask', source });
      const process = words("This repository's process on this machine, set for its repository: …", 'program');
      expect(noteLines(t, { parts: [told('repository'), process] }, 'en')).toEqual([
        { kind: 'said', text: "The agent was told this repository's process, its commit rule `ask` (set on this machine for its repository):" },
        { kind: 'words', text: process.words, by: 'program' },
      ]);
      expect(noteLines(t, { parts: [told('workspace')] }, 'en')[0].text).toContain('`ask` (set on this machine for its workspace):');
      expect(noteLines(zh, { parts: [told('default')] }, 'zh')[0].text).toBe('已告知智能体这个仓库的流程，提交规则 `ask`（本机没有设定）：');
      // A source this page does not know, or a part missing its mode, shows as recorded rather than guessed.
      expect(noteLines(t, { parts: [told('elsewhere')] }, 'en')[0].kind).toBe('recorded');
      expect(noteLines(t, { parts: [coded('chat.told-process', '…', { source: 'default' })] }, 'en')[0].kind).toBe('recorded');
    });

    it('words a withheld server by name and why, an exception’s words as written', () => {
      const lead = coded('browser.withheld', '— `browser` drives Daoris’s own browser, and was not handed:', { servers: ['browser'] });
      expect(noteLines(t, { parts: [lead, coded('browser.no-host', '…')] }, 'en').map((line) => line.text)).toEqual([
        "— `browser` drives Daoris's own browser, and was not handed:",
        'This host carries no in-app browser — only the desktop shell does.',
      ]);
      expect(noteLines(zh, { parts: [lead, words('the in-app browser was closed.', 'program')] }, 'zh')).toEqual([
        { kind: 'said', text: '—— `browser` 要使用 Daoris 自己的浏览器，但没有交给它：' },
        { kind: 'words', text: 'the in-app browser was closed.', by: 'program' },
      ]);
    });
  });

  /** CONVNOTE2f: a session's own background work, its wait and its end, worded; a program's words as written. */
  describe('background work', () => {
    it('words a wait by the work it names', () => {
      const waits = coded('background.waits', '— its turn ended while its own background work runs: `a`, `b`. …', { works: ['Run the gates', 'Build'] });
      expect(noteLines(t, { parts: [waits] }, 'en')[0].text)
        .toBe('Its turn ended while its own background work runs: `Run the gates` and `Build`. The session waits for that work, and goes on when it ends.');
      expect(noteLines(zh, { parts: [waits] }, 'zh')[0].text)
        .toBe('它的这一轮在它自己的后台工作仍在运行时结束了：`Run the gates`和`Build`。会话会等这些工作，结束后继续。');
      expect(noteLines(zh, { parts: [coded('background.waits', '…', { works: [] })] }, 'zh')[0].kind).toBe('recorded');
    });

    it('words an end work by work, and the going on', () => {
      const parts = [
        coded('background.work-ended', '`Run the gates` (completed)', { work: 'Run the gates', state: 'completed' }),
        coded('background.work-ended', '`Build` (failed)', { work: 'Build', state: 'failed' }),
        coded('background.goes-on', 'The session goes on.'),
      ];
      expect(noteLines(t, { parts }, 'en').map((line) => line.text)).toEqual([
        'Its background work `Run the gates` ended (completed).',
        'Its background work `Build` ended (failed).',
        'The session goes on.',
      ]);
      expect(noteLines(zh, { parts }, 'zh')[0].text).toBe('它的后台工作 `Run the gates` 已结束（已完成）。');
      expect(noteLines(zh, { parts }, 'zh')[1].text).toBe('它的后台工作 `Build` 已结束（失败）。');
      // An unknown state is shown as the harness sent it, in both languages.
      const odd = [coded('background.work-ended', '`Build` (paused)', { work: 'Build', state: 'paused' })];
      expect(noteLines(t, { parts: odd }, 'en')[0].text).toBe('Its background work `Build` ended (paused).');
      expect(noteLines(zh, { parts: odd }, 'zh')[0].text).toBe('它的后台工作 `Build` 已结束（paused）。');
      expect(noteLines(zh, { parts: [coded('background.work-ended', '…', { work: 'x' })] }, 'zh')[0].kind).toBe('recorded');
    });

    /** DONEEND1: the quest no longer waits on the work, so the session ends and the work still running ends with it. */
    it('words a wait its quest no longer needs by the work that ends with the session', () => {
      const over = coded('background.wait-over', '— its quest no longer waits on its background work: …', { works: ['Run the tests'] });
      expect(noteLines(t, { parts: [over] }, 'en')[0].text)
        .toBe('Its quest no longer waits on its background work: the session ends, and the work still running ends with it: `Run the tests`.');
      expect(noteLines(zh, { parts: [over] }, 'zh')[0].text)
        .toBe('它的委托不再等这些后台工作：会话结束，仍在运行的工作随之结束：`Run the tests`。');
    });

    it('words that a wait could not be read and leaves the program’s words as written', () => {
      const parts = [coded('background.unread', '— its turn ended …:'), words('the agent said no', 'program')];
      expect(noteLines(zh, { parts }, 'zh')).toEqual([
        { kind: 'said', text: '它的这一轮在它自己的后台工作仍在运行时结束了，无法确定它的委托是否在等这些后台工作：' },
        { kind: 'words', text: 'the agent said no', by: 'program' },
      ]);
    });
  });

  /** CONVNOTE2g: a conversation's words waiting for its own account's reset, the account by its id, and the door out, worded. */
  describe('a wait for an account', () => {
    const until = '2026-10-03T16:02:00Z';

    it('words the wait and its door, the command as code with its session', () => {
      const parts = [
        coded('waits.account', '— it does not go on yet: the `claude-code` account `work` is cooling until …', {
          owner: 'claude-code', account: 'account-1', until, why: 'stated',
        }),
        coded('waits.door-chat', 'To start a conversation with these words now instead, …', { session: 'c1' }),
      ];
      expect(noteLines(t, { parts }, 'en').map((line) => line.text)).toEqual([
        `It does not go on yet: the \`claude-code\` account \`account-1\` is cooling until ${moment(until, 'en')} (the agent said so), and its conversation is on that account, so your words wait to go on in it then.`,
        'To start a conversation with these words now instead, without that one: `daoris-driver sessions start-from c1`.',
      ]);
      expect(noteLines(zh, { parts }, 'zh').map((line) => line.text)).toEqual([
        `还不能继续：\`claude-code\` 账户 \`account-1\` 冷却至 ${moment(until, 'zh')}（智能体如此说明），而它的对话在那个账户上，所以你的话会等到那时再在其中继续。`,
        '要现在改用这些话开始一段对话、不带那一段：`daoris-driver sessions start-from c1`。',
      ]);
    });

    it('words the tool’s own sign-in, its refresh, and each other door', () => {
      const parts = [
        coded('waits.own', '— it does not go on yet: `codex`’s own sign-in is cooling until …', { owner: 'codex', until, why: 'default' }),
        coded('waits.read-again', 'If you have signed in to another account at your own terminal since, …'),
        coded('waits.door-new-session', 'To go on now in a new session instead, …', { session: 's1' }),
        coded('waits.door-help', 'To go on now instead, …'),
      ];
      const lines = noteLines(t, { parts }, 'en').map((line) => line.text);
      expect(lines[0]).toMatch(/^It does not go on yet: `codex`'s own sign-in is cooling until .+ \(Daoris's default: the agent named no time\), /);
      expect(lines.slice(1)).toEqual([
        "If you have signed in to another account at your own terminal since, press Read again under Agents → the agent's page → Accounts.",
        'To go on now in a new session instead, without that conversation: `daoris-driver sessions go-on-new s1`.',
        "To go on now instead, start a new conversation from this one in Ask Daoris's history: it is handed this one's words.",
      ]);
      expect(noteLines(zh, { parts }, 'zh').every((line) => line.kind === 'said')).toBe(true);
    });

    it('shows a wait as recorded where a value its sentence says is absent', () => {
      const parts = [coded('waits.account', '— it does not go on yet: …', { owner: 'claude-code', until, why: 'stated' })];
      expect(noteLines(zh, { parts }, 'zh')).toEqual([{ kind: 'recorded', text: '— it does not go on yet: …' }]);
    });
  });

  /** CONVNOTE2g2: the runner's other holds, a reason of Daoris's own as one line, a program's message as its words between lead and tail. */
  describe('a conversation’s words held for another reason', () => {
    it('words each reason of Daoris’s own as one line, its repository or conversation a value', () => {
      const checkout = [coded('held.no-checkout', '— it does not go on yet: `engine` has no checkout …', { repository: 'engine' })];
      expect(noteLines(t, { parts: checkout }, 'en').map((line) => line.text)).toEqual([
        'It does not go on yet: `engine` has no checkout on this machine any more. Your words wait on it.',
      ]);
      expect(noteLines(zh, { parts: checkout }, 'zh')[0].kind).toBe('said');

      const replaying = [coded('held.replaying', '— it does not go on yet: …', { repository: 'engine' })];
      expect(noteLines(t, { parts: replaying }, 'en')[0].text).toBe(
        "It does not go on yet: `engine`'s branches are being brought up to date right now, so a session does not start in one of its trees until that is done; it starts at the next look. Your words wait on it.",
      );
      expect(noteLines(zh, { parts: replaying }, 'zh')[0].kind).toBe('said');

      const answering = [coded('held.answering', '— it does not go on yet: …', { session: 'h2' })];
      expect(noteLines(t, { parts: answering }, 'en')[0].text).toBe(
        'It does not go on yet: Ask Daoris is answering in conversation `h2`; once its turn ends, a word said here takes these with it. Your words wait on it.',
      );
      expect(noteLines(zh, { parts: answering }, 'zh')[0].kind).toBe('said');
    });

    it('words the lead and the tail around a program’s message and leaves it as written', () => {
      const parts = [coded('held.lead', '— it does not go on yet:'), words('the ledger will not move it D:/x', 'program'), coded('held.tail', 'Your words wait on it.')];
      expect(noteLines(t, { parts }, 'en')).toEqual([
        { kind: 'said', text: 'It does not go on yet:' },
        { kind: 'words', text: 'the ledger will not move it D:/x', by: 'program' },
        { kind: 'said', text: 'Your words wait on it.' },
      ]);
      expect(noteLines(zh, { parts }, 'zh').map((line) => line.kind)).toEqual(['said', 'words', 'said']);

      const unread = [coded('held.config-unread', "— it does not go on yet: this machine's driver.json could not be read:"), words('bad json', 'program')];
      expect(noteLines(t, { parts: unread }, 'en')[0]).toEqual({ kind: 'said', text: "It does not go on yet: this machine's driver.json could not be read:" });
      expect(noteLines(zh, { parts: unread }, 'zh')[0].kind).toBe('said');
    });

    /** ROSTERREFUSE1: the roster's refusal, by code between the lead and the tail; an account by the name the page knows. */
    it('words the roster’s refusals, an account named as the page knows it', () => {
      const until = '2026-10-03T16:02:00Z';
      const names = new Map([['account-1', 'Work']]);
      const cooling = [coded('held.cooling-account', '…', { owner: 'claude-code', account: 'account-1', until, why: 'stated' })];
      expect(noteLines(t, { parts: cooling }, 'en', names)[0].text).toBe(
        `The \`claude-code\` account \`Work\` is cooling until ${moment(until, 'en')} (the agent said so). Daoris starts nothing on it until then.`,
      );
      const signedOut = [coded('held.signed-out', '…', { owner: 'codex', account: 'a-1', profile: 'a-1' })];
      expect(noteLines(t, { parts: signedOut }, 'en')[0].text).toBe(
        "The `codex` account `a-1` is not signed in, so a session would have nothing to run as — `daoris agent login codex --profile a-1` runs the agent's own sign-in into it. Daoris manages the directory and the name; the credential stays in the agent's own store.",
      );
      // LOCKPASSED1: the accounts a start was held on for their lock, each by the name the page knows.
      const locked = [coded('held.locked', '…', { owner: 'claude-code', accounts: ['account-1'] })];
      expect(noteLines(t, { parts: locked }, 'en', names)[0].text).toBe(
        'Nothing starts now on the `claude-code` account: `Work`. Another question asking about it has held it longer than a start waits, and a start beside that question could sign the account out. The next start tries again.',
      );
      const own = [coded('held.cooling-own', '…', { owner: 'codex', until, why: 'default' })];
      expect(noteLines(t, { parts: own }, 'en')[0].text).toMatch(/^`codex`'s own sign-in is cooling until .+\. Daoris starts nothing on it until then\.$/);
      const missing = [coded('held.not-installed', '…', { agent: 'codex' }), coded('held.pin-not-installed', '…', { agent: 'codex', version: '1.2.3' })];
      expect(noteLines(t, { parts: missing }, 'en').map((line) => line.text)).toEqual([
        '`codex` is not installed on this machine, so there is nothing to spawn — `daoris agent install codex` installs it. Daoris never installs an agent unasked: a tool that changed under a running loop is a moving target.',
        '`codex` is pinned to 1.2.3 on this machine, and nothing is installed at that version — `daoris agent pin codex 1.2.3` installs it, and `daoris agent unpin codex` goes back to PATH. Daoris will not quietly run a different version than the one you asked for.',
      ]);
      for (const parts of [cooling, signedOut, locked, own, missing]) {
        expect(noteLines(zh, { parts }, 'zh', names).every((line) => line.kind === 'said')).toBe(true);
      }
    });

    /** ROSTERREFUSE2: a wait over a list, a pick refused, a provider's refusal, an agent's own tooling, and a wait's added clauses. */
    it('words the roster’s other refusals, each account named as the page knows it', () => {
      const until = '2026-10-03T16:02:00Z';
      const at = moment(until, 'en');
      const names = new Map([['account-1', 'Work'], ['account-3', 'Spare']]);
      const wait = [
        coded('held.wait-some', '…', { agent: 'fake' }),
        coded('held.clause-cooling', '…', { account: 'account-1', until }),
        coded('held.clause-refused', '…', { account: 'account-2' }),
        coded('held.clause-signed-out', '…', { account: 'account-3' }),
        coded('held.clause-ready', '…', { account: 'account-4' }),
        coded('held.wait-first', '…', { account: 'account-1', until, why: 'stated' }),
        coded('held.sign-in-sooner', '…', { logins: ['daoris agent login fake --profile account-3', 'daoris agent login fake --profile "my team"'] }),
      ];
      expect(noteLines(t, { parts: wait }, 'en', names).map((line) => line.text)).toEqual([
        'No `fake` account this start may use is ready.',
        `\`Work\` is cooling until ${at}.`,
        '`account-2` was refused by its provider.',
        '`Spare` is not signed in.',
        '`account-4` is ready.',
        `The first to be ready is \`Work\`, at ${at} (the agent said so). Daoris starts nothing on them until then.`,
        'A sign-in starts it sooner: `daoris agent login fake --profile account-3` and `daoris agent login fake --profile "my team"`, or Agents → the agent\'s page → Accounts.',
      ]);
      const every = [coded('held.wait-every', '…', { agent: 'fake' }), coded('held.sign-in', '…', { logins: ['daoris agent login fake --profile a'] })];
      expect(noteLines(t, { parts: every }, 'en').map((line) => line.text)).toEqual([
        'Every `fake` account this start may use is cooling.',
        'A sign-in starts it: `daoris agent login fake --profile a`, or Agents → the agent\'s page → Accounts.',
      ]);

      const picked = [coded('held.picked-ready', '…', { accounts: ['account-1', 'account-2'] }), coded('held.picked-none', '…', { agent: 'fake' })];
      expect(noteLines(t, { parts: picked }, 'en', names).map((line) => line.text)).toEqual([
        'Ready now: `Work` and `account-2`.',
        'No other `fake` account is ready.',
      ]);

      const refused = [
        coded('held.refused-account', '…', { owner: 'fake', account: 'account-1' }),
        coded('held.refused-own', '…', { owner: 'fake' }),
        coded('held.sign-in-refused-own', '…', { owner: 'fake' }),
      ];
      expect(noteLines(t, { parts: refused }, 'en', names)[0].text).toMatch(/^An earlier session found that its provider refused the `fake` account `Work` \(401\)\./);

      // An agent with its own tooling: its command is words, shown as written and never marked.
      const own = [coded('held.not-installed-own', '…', { agent: 'fake' }), words('C:\\tools\\fake-install --yes', 'program'), coded('held.never-installs', '…')];
      expect(noteLines(t, { parts: own }, 'en')).toEqual([
        { kind: 'said', text: '`fake` is not installed on this machine, so there is nothing to spawn — install it with its own tooling:' },
        { kind: 'words', text: 'C:\\tools\\fake-install --yes', by: 'program' },
        { kind: 'said', text: 'Daoris never installs an agent unasked: a tool that changed under a running loop is a moving target.' },
      ]);

      const clauses = [
        coded('held.kept-aside', '…', { account: 'account-3' }),
        coded('held.outside-machine', '…', { accounts: ['account-3'], first: 'account-3', door: 'daoris agent profile order fake account-1 account-3' }),
        coded('held.outside-workspace', '…', { workspace: 'work', accounts: ['account-3', 'account-4'], first: 'account-3', door: 'daoris agent profile order fake account-1 account-3 --workspace work' }),
      ];
      expect(noteLines(t, { parts: clauses }, 'en', names).map((line) => line.text)).toEqual([
        '`Spare` is kept for conversations.',
        'Not cooling, and not among the accounts this machine\'s starts may use: `Spare` — Daoris starts nothing on them unless a list names them; `daoris agent profile order fake account-1 account-3` adds `Spare`.',
        'Not cooling, and not among the accounts `work` may use: `Spare` and `account-4` — Daoris starts nothing on them unless a list names them; `daoris agent profile order fake account-1 account-3 --workspace work` adds `Spare`.',
      ]);
      for (const parts of [wait, every, picked, refused, own, clauses]) {
        expect(noteLines(zh, { parts }, 'zh', names).every((line) => line.kind !== 'recorded')).toBe(true);
      }
      // A list that is not one, or an account that is absent, is shown as recorded.
      expect(noteLines(t, { parts: [coded('held.picked-ready', 'Ready now: `x`.', { accounts: [] })] }, 'en')[0].kind).toBe('recorded');
    });

    it('shows a hold as recorded where its repository is absent', () => {
      const parts = [coded('held.no-checkout', '— it does not go on yet: …')];
      expect(noteLines(zh, { parts }, 'zh')[0].kind).toBe('recorded');
    });
  });

  /** CONVNOTE2d1: words the agent would not take, ones that never reached it, and ones not taken off the record, worded; others' words as written. */
  describe('words that did not reach an agent or were not taken', () => {
    it('words a refusal by its lead and leaves the agent’s words as written', () => {
      const parts = [coded('words.refused', '— the session would not take what the person added:'), words('Internal error: busy', 'agent')];
      expect(noteLines(t, { parts }, 'en')).toEqual([
        { kind: 'said', text: 'The session would not take what you added:' },
        { kind: 'words', text: 'Internal error: busy', by: 'agent' },
      ]);
      expect(noteLines(zh, { parts }, 'zh')[0]).toEqual({ kind: 'said', text: '会话没有接受你补充的内容：' });
    });

    it('words a count of messages and why they never reached it, by reason', () => {
      const unreached = (why: string, count: unknown = 2) =>
        coded('words.unreached', '— 2 message(s) the person sent never reached the session: …', { count, why });
      expect(noteLines(t, { parts: [unreached('ended-first')] }, 'en')[0].text)
        .toBe('Messages you sent while it worked never reached it (2 in all): it ended first.');
      expect(noteLines(t, { parts: [unreached('unkept', 1200)] }, 'en')[0].text)
        .toBe('Messages you sent while it worked never reached it (1,200 in all): Daoris kept no id for its conversation.');
      expect(noteLines(zh, { parts: [unreached('ended-first')] }, 'zh')[0].text)
        .toBe('你在它工作时发送的 2 条消息没有送达：它先结束了。');
      // A reason this build does not know, or a count that is no number, leaves the line as recorded.
      expect(noteLines(zh, { parts: [unreached('moved-on')] }, 'zh')[0].kind).toBe('recorded');
      expect(noteLines(zh, { parts: [unreached('ended-first', 'two')] }, 'zh')[0].kind).toBe('recorded');
    });

    it('words a conversation handed to another agent at both ends, and why by reason (MIXAGENT1a-i)', () => {
      const went = coded('chat.handed-over', 'The conversation went on with codex-acp, in conversation ab12cd34.', { agent: 'codex-acp', session: 'ab12cd34' });
      expect(noteLines(t, { parts: [went] }, 'en')[0].text).toBe('This conversation went on with codex-acp, in conversation ab12cd34.');
      expect(noteLines(zh, { parts: [went] }, 'zh')[0].text).toBe('这段对话已转交给 codex-acp 继续，新对话为 ab12cd34。');

      const took = (why: string) =>
        coded('chat.taken-over', 'The conversation went on from claude-code conversation ab12cd34, …', { agent: 'claude-code', session: 'ab12cd34', why });
      expect(noteLines(t, { parts: [took('limit')] }, 'en')[0].text)
        .toBe('This conversation went on from claude-code conversation ab12cd34, handed over because its account reached its limit.');
      expect(noteLines(zh, { parts: [took('press')] }, 'zh')[0].text)
        .toBe('这段对话是从 claude-code 的对话 ab12cd34 转交过来继续的，原因：你要求转交。');
      // A reason this build does not know leaves the line as recorded.
      expect(noteLines(t, { parts: [took('moved-on')] }, 'en')[0].kind).toBe('recorded');
      // SWITCH1c-i: Daoris moved it by the usage curve.
      expect(noteLines(t, { parts: [took('curve')] }, 'en')[0].text)
        .toBe('This conversation went on from claude-code conversation ab12cd34, handed over because its account was over its usage curve.');
      expect(noteLines(zh, { parts: [took('curve')] }, 'zh')[0].text)
        .toBe('这段对话是从 claude-code 的对话 ab12cd34 转交过来继续的，原因：它的账户超出了用量曲线。');
    });

    /**
     * SWITCH1c-i (the switch design §4): a conversation moved to another account by the usage curve says so at both ends, each
     * account by the name the person gave it (NOTENAME1), the window over its curve, its use and the curve's value there now.
     */
    it('words a conversation moved by the usage curve at both ends, each account by its name (SWITCH1c-i)', () => {
      const values = { window: 'weekly', used: 62, curve: 45 };
      const went = coded('chat.moved', 'Moved to `account-3` …', { to: 'account-3', from: 'account-1', ...values });
      const came = coded('chat.moved-from', 'Continues from `account-1` …', { from: 'account-1', ...values });

      expect(noteLines(t, { parts: [went, came] }, 'en').map((line) => line.text)).toEqual([
        "Moved to `account-3` at the end of its turn: `account-1` had used 62% of its weekly limit, over its curve's 45%; `account-3` has room.",
        "Continues from `account-1`, which had used 62% of its weekly limit, over its curve's 45%.",
      ]);
      expect(noteLines(zh, { parts: [went, came] }, 'zh').map((line) => line.text)).toEqual([
        '在这一轮结束时转到账户 `account-3`：`account-1` 的每周上限已用 62%，超出其曲线此刻的 45%；`account-3` 还有余量。',
        '接续自账户 `account-1`：它的每周上限已用 62%，超出其曲线此刻的 45%。',
      ]);

      const names = new Map([['account-1', 'Work'], ['account-3', 'Spare']]);
      expect(noteLines(t, { parts: [went, came] }, 'en', names).map((line) => line.text)).toEqual([
        "Moved to `Spare` at the end of its turn: `Work` had used 62% of its weekly limit, over its curve's 45%; `Spare` has room.",
        "Continues from `Work`, which had used 62% of its weekly limit, over its curve's 45%.",
      ]);
      // Elsewhere `to` and `from` are no account: a review's repository keeps its own word.
      const review = coded('review.no-rule', '…', { named: 'codex', to: 'account-3' });
      expect(noteLines(t, { parts: [review] }, 'en', names)[0].text).toContain('for `account-3`');
      // A value its entry says that is absent, or a use that is no whole percent: the line is shown as recorded.
      expect(noteLines(t, { parts: [coded('chat.moved-from', 'Continues from …', { from: 'account-1', window: 'weekly', used: 62 })] }, 'en')[0].kind)
        .toBe('recorded');
      expect(noteLines(t, { parts: [coded('chat.moved', 'Moved …', { to: 'account-3', from: 'account-1', ...values, used: 61.5 })] }, 'en')[0].kind)
        .toBe('recorded');
    });

    it('words a helper asked, answered and refused on the asker, and helping on the helper (MIXAGENT2a-ii)', () => {
      const asked = coded('helper.asked', 'A helper was asked: …', { agent: 'codex-acp', session: 'ab12cd34' });
      expect(noteLines(t, { parts: [asked] }, 'en')[0].text).toBe('A helper was asked: codex-acp reads in session ab12cd34 and answers the question.');
      expect(noteLines(zh, { parts: [asked] }, 'zh')[0].text).toBe('已请助手回答：codex-acp 在会话 ab12cd34 中阅读并回答这个问题。');

      const last = coded('helper.answered-last-words', 'Helper …', { agent: 'codex-acp', session: 'ab12cd34' });
      expect(noteLines(t, { parts: [last] }, 'en')[0].text)
        .toBe('Helper codex-acp (session ab12cd34) did not use its answer tool; its last words stand as the answer.');

      const refused = (why: string) => coded('helper.refused-by', 'Helper …', { agent: 'codex-acp', why });
      expect(noteLines(t, { parts: [refused('no-answer')] }, 'en')[0].text)
        .toBe('Helper codex-acp did not answer this question: the helper ended without an answer.');
      expect(noteLines(zh, { parts: [refused('too-many')] }, 'zh')[0].text).toBe('助手 codex-acp 没有回答这个问题：该会话已有两位助手在工作。');
      expect(noteLines(t, { parts: [coded('helper.refused', 'No helper …', { why: 'off' })] }, 'en')[0].text)
        .toBe('No helper answered this question: the rule for helpers is off for this repository.');
      // A reason this build does not know leaves the line as recorded.
      expect(noteLines(t, { parts: [refused('moved-on')] }, 'en')[0].kind).toBe('recorded');

      const helping = coded('helper.helping', 'This session is helping …', { session: 'ab12cd34' });
      expect(noteLines(zh, { parts: [helping] }, 'zh')[0].text).toBe('这个会话正在为会话 ab12cd34 充当助手：它只阅读、只回答一个问题，不改动任何内容。');
    });

    it('words a take that failed and leaves the service’s message as written', () => {
      const parts = [coded('words.not-taken', '— your words could not be taken off its record:'), words('no record at D:/x', 'program')];
      expect(noteLines(zh, { parts }, 'zh')).toEqual([
        { kind: 'said', text: '你的话无法从它的记录中移除：' },
        { kind: 'words', text: 'no record at D:/x', by: 'program' },
      ]);
    });

    /** CONVNOTE2d2: the chat runner's and the modules' sites. */
    it('words words that never reached a conversation, a send that found the agent gone, and held words not kept', () => {
      const never = [coded('words.not-reached', '— what the person added never reached it:'), words('pipe closed at D:/x', 'program')];
      expect(noteLines(t, { parts: never }, 'en')).toEqual([
        { kind: 'said', text: 'What you added never reached it:' },
        { kind: 'words', text: 'pipe closed at D:/x', by: 'program' },
      ]);
      expect(noteLines(zh, { parts: never }, 'zh')[0].kind).toBe('said');

      expect(noteLines(t, { parts: [coded('words.not-sent', 'the message could not be sent: the agent had gone.')] }, 'en')).toEqual([
        { kind: 'said', text: 'The message could not be sent: the agent had gone.' },
      ]);

      const kept = [coded('words.not-kept', '— what you said as it wound up was not kept for it to go on with:'), words('ended', 'program')];
      expect(noteLines(t, { parts: kept }, 'en')[0]).toEqual({ kind: 'said', text: 'What you said as it wound up was not kept for it to go on with:' });
    });

    it('words a start’s words not taken off their session, the session as a value', () => {
      const parts = [
        coded('words.start-not-taken', '— the words this conversation started with could not be taken off session `s-1`:', { session: 's-1' }),
        words('no record', 'program'),
      ];
      expect(noteLines(t, { parts }, 'en')[0].text).toBe('The words this conversation started with could not be taken off session s-1:');
      expect(noteLines(zh, { parts }, 'zh')[0].kind).toBe('said');
    });
  });

  /** CONVNOTE2h: a review set-up's record and a second opinion's answers, worded; a program's words as written. */
  describe('a review set-up and a second opinion’s answers', () => {
    it('words a set-up that could not be posted, and a post the next end retries', () => {
      const noRule = coded('review.no-rule', 'its set-up was not posted: no review rule …', { named: 'browser', to: 'web' });
      expect(noteLines(t, { parts: [noRule] }, 'en')[0].text)
        .toBe('Its set-up was not posted: no review rule here names `browser` for `web` any more, so its kind is not known. Declare it, and its next end posts it.');
      expect(noteLines(zh, { parts: [noRule] }, 'zh')[0].kind).toBe('said');
      expect(noteLines(zh, { parts: [coded('review.no-rule', '…', { named: 'browser' })] }, 'zh')[0].kind).toBe('recorded');

      expect(noteLines(t, { parts: [coded('review.no-commit', '…')] }, 'en')[0]).toEqual({
        kind: 'said',
        text: "Its set-up was not posted: its tree's commit could not be read.",
      });

      const failed = [coded('review.not-posted', '…'), words('connection lost', 'program'), coded('review.posts-next', 'Its next end posts it.')];
      expect(noteLines(t, { parts: failed }, 'en')).toEqual([
        { kind: 'said', text: 'Its set-up was not posted:' },
        { kind: 'words', text: 'connection lost', by: 'program' },
        { kind: 'said', text: 'Its next end posts it.' },
      ]);
      expect(noteLines(zh, { parts: failed }, 'zh').map((line) => line.kind)).toEqual(['said', 'words', 'said']);
    });

    it('words a set-up posted by its commit and the environment, the service’s words beneath', () => {
      const posted = [coded('review.posted', '…', { commit: '01234567', named: 'browser' }), words('Kept as set-up 1.', 'program')];
      expect(noteLines(t, { parts: posted }, 'en')).toEqual([
        { kind: 'said', text: 'Daoris read `01234567` from its tree and posted what it showed in `browser`:' },
        { kind: 'words', text: 'Kept as set-up 1.', by: 'program' },
      ]);
      expect(noteLines(zh, { parts: posted }, 'zh')[0].kind).toBe('said');
      expect(noteLines(zh, { parts: [coded('review.posted', '…', { commit: '01234567' })] }, 'zh')[0].kind).toBe('recorded');
    });

    it('words the answers to a second opinion and each count, the person as you', () => {
      const parts = [
        coded('opinion.answers', '…', { who: 'Codex (OpenAI)', count: 7 }),
        coded('opinion.fixed', '…', { count: 1 }),
        coded('opinion.unproven', '…', { count: 3 }),
        coded('opinion.rejected', '…', { count: 1 }),
        coded('opinion.unresolved', '…', { count: 1 }),
        coded('opinion.unanswered', '…', { count: 1 }),
        coded('opinion.disputed', '…', { count: 4 }),
      ];
      const en = noteLines(t, { parts }, 'en');
      expect(en.map((line) => line.kind)).toEqual(Array(7).fill('said'));
      expect(en[0].text).toBe("The answers to Codex (OpenAI)'s findings (7 in all), as its turn ended:");
      expect(en[6].text).toBe('Findings marked `must` and not fixed wait for you (4 in all).');
      expect(noteLines(zh, { parts }, 'zh').map((line) => line.kind)).toEqual(Array(7).fill('said'));
      expect(noteLines(zh, { parts: [coded('opinion.fixed', '…', {})] }, 'zh')[0].kind).toBe('recorded');
      expect(noteLines(zh, { parts: [coded('opinion.answers', '…', { who: 'x' })] }, 'zh')[0].kind).toBe('recorded');
    });
  });

  /** XAGENT1g4a: the reviewer's own conclusion; the failure stays the program's words. */
  describe('a second opinion’s reviewer ending', () => {
    it('words each ending, the failure beneath as written', () => {
      const failed = [coded('opinion.reviewer-failed', '…', { opinion: 'op1' }), words('no route to D:/x', 'program')];
      expect(noteLines(t, { parts: failed }, 'en')).toEqual([
        { kind: 'said', text: "The reviewer's turn failed as it read the work for second opinion `op1`:" },
        { kind: 'words', text: 'no route to D:/x', by: 'program' },
      ]);
      expect(noteLines(zh, { parts: failed }, 'zh')[0].kind).toBe('said');
      const read = [coded('opinion.reviewer-read', '…', { opinion: 'op1' })];
      expect(noteLines(t, { parts: read }, 'en')[0].text).toBe("It read the work and ended; what it said is second opinion `op1`'s.");
      expect(noteLines(zh, { parts: read }, 'zh')[0].kind).toBe('said');
      const exited = [coded('opinion.reviewer-exited', '…', { opinion: 'op1', exit: 3 })];
      expect(noteLines(t, { parts: exited }, 'en')[0].text).toBe(
        "It ended with exit 3 as it read the work; what it said, if anything, is second opinion `op1`'s.",
      );
      expect(noteLines(zh, { parts: exited }, 'zh')[0].kind).toBe('said');
      expect(noteLines(zh, { parts: [coded('opinion.reviewer-exited', '…', { opinion: 'op1' })] }, 'zh')[0].kind).toBe('recorded');
    });
  });

  /** CONVNOTE2e: a conversation's door that failed; a program's message and the agent's refusal stay as written. */
  describe('a door or a turn that failed', () => {
    it('words a session that could not open or failed, and a turn not taken, and leaves the program’s words as written', () => {
      const open = [coded('door.not-open', 'the ACP session could not open:'), words('could not read D:/x', 'program')];
      expect(noteLines(t, { parts: open }, 'en')).toEqual([
        { kind: 'said', text: 'The session could not open:' },
        { kind: 'words', text: 'could not read D:/x', by: 'program' },
      ]);
      expect(noteLines(zh, { parts: open }, 'zh')[0]).toEqual({ kind: 'said', text: '会话无法打开：' });

      const turn = [coded('door.turn-not-taken', 'the turn could not be taken:'), words('Authentication required', 'program')];
      expect(noteLines(t, { parts: turn }, 'en')[0]).toEqual({ kind: 'said', text: 'This turn could not be taken:' });
      expect(noteLines(zh, { parts: turn }, 'zh')[0]).toEqual({ kind: 'said', text: '这一轮无法进行：' });

      const failed = [coded('door.failed', '— the ACP session failed:'), words('the stream ended', 'program')];
      expect(noteLines(t, { parts: failed }, 'en')[0]).toEqual({ kind: 'said', text: 'The session failed:' });
      expect(noteLines(zh, { parts: failed }, 'zh')[0]).toEqual({ kind: 'said', text: '会话失败了：' });
    });

    it('words a refused resume by its reason, the agent’s words beneath, and an answer carried on', () => {
      const refused = (why: string, values: Record<string, unknown> = {}) =>
        coded('door.resume-refused', 'the agent no longer has its conversation', { why, ...values });
      expect(noteLines(t, { parts: [refused('gone')] }, 'en')).toEqual([
        { kind: 'said', text: 'It did not go on in its own conversation: the agent no longer has its conversation.' },
      ]);
      expect(noteLines(zh, { parts: [refused('gone')] }, 'zh')[0].text).toBe('它没有在自己的对话中继续：智能体已没有它的对话。');
      expect(noteLines(t, { parts: [refused('elsewhere', { agent: 'codex-acp' })] }, 'en')[0].text)
        .toBe('It did not go on in its own conversation: its conversation is open in another client of codex-acp.');

      const answer = [
        refused('refused'),
        words('Internal error', 'agent'),
        coded('door.answer-carried-on', 'your answer is carried on in a new session'),
      ];
      expect(noteLines(t, { parts: answer }, 'en')).toEqual([
        { kind: 'said', text: 'It did not go on in its own conversation: its conversation could not be continued.' },
        { kind: 'words', text: 'Internal error', by: 'agent' },
        { kind: 'said', text: 'Your answer is carried on in a new session.' },
      ]);
      expect(noteLines(zh, { parts: answer }, 'zh')[2]).toEqual({ kind: 'said', text: '你的回答已在新会话中接续。' });

      // A reason this build does not know, or elsewhere without the agent its line names, shows as recorded.
      expect(noteLines(zh, { parts: [refused('newer')] }, 'zh')[0].kind).toBe('recorded');
      expect(noteLines(zh, { parts: [refused('elsewhere')] }, 'zh')[0].kind).toBe('recorded');
    });
  });

  /**
   * AGT3d (D125's AGT3c note): a limit's line says the kind of limit, its window worded as the agents' screens word it
   * (`harness.window.*`), and whose accounts, in either language; a window this build does not know is said as named.
   */
  it('words a limit’s window and whose account it was, in either language', () => {
    const limit = (window: string) => coded('account.cooling-window', 'The `claude-code` account it ran on hit its … limit and is cooling until …', {
      until: '2026-10-03T16:00:00Z', why: 'stated', owner: 'claude-code', window,
    });

    const [english] = noteLines(t, { parts: [limit('session')] }, 'en');
    expect(english).toMatchObject({ kind: 'said' });
    expect(english.text).toMatch(/^The claude-code account it ran on hit its five-hour limit and is cooling until .+ \(the agent said so\); nothing starts on it until then\.$/);
    const [chinese] = noteLines(zh, { parts: [limit('weekly')] }, 'zh');
    expect(chinese).toMatchObject({ kind: 'said' });
    expect(chinese.text).toMatch(/^它运行时用的 claude-code 账户达到了每周上限，冷却至 .+（智能体如此说明），在此之前不会在它上面启动任何会话。$/);
    expect(noteLines(zh, { parts: [limit('session')] }, 'zh')[0].text).toContain('达到了5 小时上限');
    expect(noteLines(t, { parts: [limit('monthly')] }, 'en')[0].text).toContain('hit its monthly limit');
    expect(noteLines(t, { parts: [limit('90-minute')] }, 'en')[0].text).toContain('hit its 90-minute limit');
    expect(noteLines(zh, { parts: [limit('90-minute')] }, 'zh')[0].text).toContain('达到了90 分钟上限');

    const none = coded('account.cooling-no-window', 'The `codex` account it ran on is cooling until …', {
      until: '2026-10-03T16:00:00Z', why: 'default', owner: 'codex',
    });
    expect(noteLines(zh, { parts: [none] }, 'zh')[0].text).toMatch(/^它运行时用的 codex 账户冷却至 .+（Daoris 的默认值：智能体没有说明时间），在此之前不会在它上面启动任何会话。$/);

  });

  /**
   * AGT3d: a code keeps what it declares, so a note already on a machine keeps being worded. A cooling line written before
   * AGT3d is `account.cooling` with its moment and why alone, and the new shapes have codes of their own.
   */
  it('still words a cooling line written before AGT3d, in either language', () => {
    const before = coded('account.cooling', 'The account it ran on is cooling until …', { until: '2026-10-03T16:00:00Z', why: 'stated' });

    const [english] = noteLines(t, { parts: [before] }, 'en');
    expect(english).toMatchObject({ kind: 'said' });
    expect(english.text).toMatch(/^The account it ran on is cooling until .+ \(the agent said so\); nothing starts on it until then\.$/);
    const [chinese] = noteLines(zh, { parts: [before] }, 'zh');
    expect(chinese).toMatchObject({ kind: 'said' });
    expect(chinese.text).toMatch(/^它运行时用的账户冷却至 .+（智能体如此说明），在此之前不会在它上面启动任何会话。$/);
  });

  /**
   * TREEDEPS1 (D51's note): a tree its checkout's dependencies were copied into says which, each as code, and a project that copied
   * none says why; `started.tree` keeps saying a fresh tree holds no installed dependencies, as the openings written before it did.
   */
  it('words a tree opened with its checkout’s dependencies copied in, and why a project’s were not, in either language', () => {
    const parts = [
      coded('started.tree-copied', 'opened a session tree …', { branch: 'daoris/s1', basedOn: 'main', copied: ['node_modules', 'web/node_modules'] }),
      coded('started.uncopied-workspace', '`tools/node_modules` was not copied …', { modules: 'tools/node_modules', workspace: 'packages/ui' }),
    ];

    const [copied, workspace] = noteLines(t, { parts }, 'en');
    expect(copied.text).toMatch(
      /^Opened a session tree on daoris\/s1, from main, with the dependencies your checkout installed at the same lockfile copied in \(`node_modules`.+`web\/node_modules`\): `npm ci` is not needed there unless a lockfile changes\. It holds no build outputs\.$/,
    );
    expect(workspace.text).toBe("`tools/node_modules` was not copied from your checkout: the lockfile of its npm workspace `packages/ui` differs from the checkout's.");
    const [copiedZh, workspaceZh] = noteLines(zh, { parts }, 'zh');
    expect(copiedZh.text).toMatch(/^已从 main 在 daoris\/s1 上打开会话工作树，并复制了你的检出按同一锁文件安装的依赖（`node_modules`.+`web\/node_modules`）/);
    expect(workspaceZh.text).toBe('`tools/node_modules` 没有从你的检出复制：它的 npm 工作区 `packages/ui` 的锁文件与检出的不同。');
    expect(noteLines(t, { parts: [coded('started.tree', 'opened …', { branch: 'daoris/s1', basedOn: 'main' })] }, 'en')[0].text)
      .toMatch(/no installed dependencies, no build outputs\.$/);
  });

  /** A program's words pass through as written, as `DRIVER_REFUSED` passes the driver's sentence. */
  it('passes a program’s words through as written', () => {
    expect(noteLines(zh, { parts: [words('System.IO.IOException: The process cannot access the file.', 'program')] }, 'zh'))
      .toEqual([{ kind: 'words', text: 'System.IO.IOException: The process cannot access the file.', by: 'program' }]);
  });

  /**
   * REVIEWENV1c2: the look under *Accept automatically* that the review's gate holds says so with a code of its own, the gate's
   * sentence beneath it as the driver wrote it, in either language. A note written before it, under `landing.refused`, keeps
   * being worded as it was.
   */
  it('words a look held for the person’s review as its own line, never as a refused landing, in either language', () => {
    const gate = 'Waits for your review in `dev`: nothing shows it there yet: no set-up step for `storefront` is in its chain.';
    const parts = [
      coded('landing.unreviewed', 'not accepted automatically: its work waits for your review before it lands, as follows. …'),
      words(gate, 'program'),
    ];

    expect(noteLines(t, { parts }, 'en')).toEqual([
      {
        kind: 'said',
        text: 'Not accepted automatically: its work waits for your review before it lands, as follows. It lands at the first look '
          + 'after you review it or skip the review.',
      },
      { kind: 'words', text: gate, by: 'program' },
    ]);
    expect(noteLines(zh, { parts }, 'zh')).toEqual([
      { kind: 'said', text: '未自动采纳：它的工作在落地前等你审阅，详情如下。你审阅或跳过审阅后的下一轮会落地它的工作。' },
      { kind: 'words', text: gate, by: 'program' },
    ]);
    expect(noteLines(zh, { parts: [coded('landing.refused', 'not accepted automatically: the landing was refused, as follows.')] }, 'zh'))
      .toEqual([{ kind: 'said', text: '未自动采纳：落地被拒绝，原因如下。它等待你的审阅。' }]);
  });

  /**
   * XAGENT1g (XAGENT1e2's lines): a record that went on with another agent's findings says so, never *your words*, in either
   * language, its moment in the reader's own.
   */
  it('words a record that went on with another agent’s findings, alone or beside the person’s words', () => {
    const at = { at: '2026-09-19T10:30:00Z' };
    const findings = { code: 'ledger.went-on-findings', values: at, text: 'Went on with another agent\'s findings at 2026-09-19 10:30 UTC.' };
    const both = { code: 'ledger.went-on-both', values: at, text: 'Went on with your words and another agent\'s findings at 2026-09-19 10:30 UTC.' };

    const [english] = noteLines(t, { parts: [findings] }, 'en');
    expect(english).toMatchObject({ kind: 'said' });
    expect(english!.text).toMatch(/^Went on with another agent's findings at .+\.$/);
    const [chinese] = noteLines(zh, { parts: [both] }, 'zh');
    expect(chinese).toMatchObject({ kind: 'said' });
    expect(chinese!.text).toMatch(/^于 .+ 带着你的话和另一个智能体的发现继续。$/);
  });

  /**
   * XAGENT1g: the look held for a second opinion says so with a code of its own, the gate's sentence beneath it as the driver
   * wrote it, in either language; one written before the page worded it says `landing.refused`, and keeps being worded as that.
   */
  it('words a look held for a second opinion as its own line, never as a refused landing, in either language', () => {
    const gate = 'Waits for a second opinion: being read by Codex (OpenAI), another maker\'s agent.';
    const parts = [
      coded('landing.opinion', 'not accepted automatically: its work waits for a second opinion before it lands, as follows. …'),
      words(gate, 'program'),
    ];

    expect(noteLines(t, { parts }, 'en')).toEqual([
      {
        kind: 'said',
        text: 'Not accepted automatically: its work waits for a second opinion before it lands, as follows. It lands at the first '
          + 'look once the opinion is settled, or you go on without one.',
      },
      { kind: 'words', text: gate, by: 'program' },
    ]);
    expect(noteLines(zh, { parts }, 'zh')).toEqual([
      { kind: 'said', text: '未自动采纳：它的工作在落地前等第二意见，详情如下。第二意见了结后，或你决定不等它继续后，下一轮会落地它的工作。' },
      { kind: 'words', text: gate, by: 'program' },
    ]);
  });
});

describe('noteBlocks', () => {
  /** §9: Daoris's lines run together as one paragraph; someone's words, or a recorded line, set themselves apart. */
  it('runs Daoris’s lines together until someone’s words or a recorded line sets itself apart', () => {
    const lines = noteLines(zh, {
      parts: [
        coded('ended.turn-failed-taken', 'The agent’s turn failed with the quest still taken:'),
        words('rate limited', 'agent'),
        coded('account.refused', '…', { owner: 'Claude Code' }),
        coded('started.other-account', 'It runs on another account.'),
        coded('ended.brand-new', 'Something newer.'),
      ],
    }, 'zh');
    expect(noteBlocks(zh, lines)).toEqual([
      { kind: 'said', text: '智能体这一轮失败，委托仍已接下：' },
      { kind: 'words', text: 'rate limited', by: 'agent' },
      {
        kind: 'said',
        text: '服务方拒绝了它运行时用的 Claude Code 账户（401）。请在设置中或用 `daoris agent` 更换密钥或重新登录，Daoris 随后会再在它上面启动会话。它改用另一个账户运行。',
      },
      { kind: 'recorded', text: 'Something newer.' },
    ]);
  });
});

describe('noteText', () => {
  /** One run for a row too narrow for blocks: its lines joined the catalogue's way, English with a space, 中文 without. */
  it('joins the lines the reader’s way', () => {
    const parts = [coded('ended.done', 'the quest reached done.'), coded('started.same-tree', 'In the tree it worked in.')];
    expect(noteText(t, { parts }, 'en')).toBe('The quest reached done. In the tree it worked in.');
    expect(noteText(zh, { parts }, 'zh')).toBe('委托已完成。在它原来的工作树中。');
  });
});

// UX6b (design §2.5): a note longer than four lines folds to them in the timeline. The page lays nothing out before it
// draws, so how many lines a note takes is estimated from its texts, near enough to fold a verify report and leave a
// sentence whole.
describe('linesTaken', () => {
  it('counts each of a text’s own lines once, however short', () => {
    expect(linesTaken(['one'], 50)).toBe(1);
    expect(linesTaken(['one\ntwo\nthree'], 50)).toBe(3);
    expect(linesTaken(['one', 'two'], 50)).toBe(2);
    expect(linesTaken(['a\n\nb'], 50)).toBe(3);
  });

  it('counts a line once more for each measure it runs past', () => {
    expect(linesTaken(['x'.repeat(50)], 50)).toBe(1);
    expect(linesTaken(['x'.repeat(51)], 50)).toBe(2);
    expect(linesTaken(['x'.repeat(120), 'short'], 50)).toBe(4);
  });

  it('counts an ideograph as two, since it sets about twice as wide as a letter', () => {
    expect(linesTaken(['验'.repeat(25)], 50)).toBe(1);
    expect(linesTaken(['验'.repeat(26)], 50)).toBe(2);
    expect(linesTaken(['验证通过，'.repeat(20)], 50)).toBe(4);
  });

  it('takes no lines for nothing', () => {
    expect(linesTaken([], 50)).toBe(0);
  });
});

describe('the page’s map of codes', () => {
  it('words every code it maps, in both catalogues', () => {
    for (const [code, entry] of Object.entries(NOTE_CODES)) {
      const key = entry.key ?? `note.${code}`;
      expect(i18n.exists(key, { lng: 'en' }), key).toBe(true);
      expect(i18n.exists(key, { lng: 'zh' }), key).toBe(true);
    }
  });
});
