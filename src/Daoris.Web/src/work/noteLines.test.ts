import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { NotePart } from '../api';
import { moment } from '../format';
import { linesTaken, NOTE_CODES, noteBlocks, noteLines, noteText } from './noteLines';

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
      expect(noteLines(zh, { parts }, 'zh')[0].text).toBe('它的后台工作 `Run the gates` 已结束（completed）。');
      expect(noteLines(zh, { parts: [coded('background.work-ended', '…', { work: 'x' })] }, 'zh')[0].kind).toBe('recorded');
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
      const own = [coded('held.cooling-own', '…', { owner: 'codex', until, why: 'default' })];
      expect(noteLines(t, { parts: own }, 'en')[0].text).toMatch(/^`codex`'s own sign-in is cooling until .+\. Daoris starts nothing on it until then\.$/);
      const missing = [coded('held.not-installed', '…', { agent: 'codex' }), coded('held.pin-not-installed', '…', { agent: 'codex', version: '1.2.3' })];
      expect(noteLines(t, { parts: missing }, 'en').map((line) => line.text)).toEqual([
        '`codex` is not installed on this machine, so there is nothing to spawn — `daoris agent install codex` installs it. Daoris never installs an agent unasked: a tool that changed under a running loop is a moving target.',
        '`codex` is pinned to 1.2.3 on this machine, and nothing is installed at that version — `daoris agent pin codex 1.2.3` installs it, and `daoris agent unpin codex` goes back to PATH. Daoris will not quietly run a different version than the one you asked for.',
      ]);
      for (const parts of [cooling, signedOut, own, missing]) {
        expect(noteLines(zh, { parts }, 'zh', names).every((line) => line.kind === 'said')).toBe(true);
      }
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
