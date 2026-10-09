import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { NotePart } from '../api';
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
