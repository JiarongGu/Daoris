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
    const cooling = coded('account.cooling', 'The account it ran on is cooling until …', { until: '2026-10-03T16:00:00Z', why: 'default' });
    const [line] = noteLines(t, { parts: [cooling] }, 'en');
    expect(line).toMatchObject({ kind: 'said' });
    expect(line.text).toMatch(/^The account it ran on is cooling until .+ \(Daoris's default: the agent named no time\); nothing starts on it until then\.$/);
  });

  /** A program's words pass through as written, as `DRIVER_REFUSED` passes the driver's sentence. */
  it('passes a program’s words through as written', () => {
    expect(noteLines(zh, { parts: [words('System.IO.IOException: The process cannot access the file.', 'program')] }, 'zh'))
      .toEqual([{ kind: 'words', text: 'System.IO.IOException: The process cannot access the file.', by: 'program' }]);
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
