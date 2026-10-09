import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { OpinionField, type OpinionRule, opinionGate, opinionRowSays, opinionSays, opinionSummary, opinionToast } from './Opinions';

// XAGENT1a (D155 point 3, the second-agent design §2.5–§2.6): which other agent reads a repository's work before it lands,
// set on a repository's Setup and a workspace's Defaults as `daoris driver opinion` sets it from a terminal (D50). The words
// each door says are the twins' (`opinions.ts`, `OpinionRules.cs`), held here in English to rows of their one table (the
// driver suite's `fixtures/opinion-rules.json`, `says` and `gate`), copied with the rule as the bridge carries it, and the
// field sends the twins' edit, which the driver judges.

type SaysRow = [name: string, rule: OpinionRule, sentences: string[]];

const COPY = 'in a copy of its own that nothing is taken back from; its findings go to the session that did the work, and to you.';
const SAME = 'the same agent as the one that does the work here: a fresh conversation of the same agent is not an independent '
  + 'reading, and each opinion says so.';

/** Rows of the twins' `says` table, copied: a rule as the bridge carries it, its `sameAgent` the table's input, and what each door says. */
const SAYS: SaysRow[] = [
  ['none here', { none: true, on: [], reviewers: [] },
    ['No second opinion here, whatever its workspace says: no other agent reads work here before it lands.']],
  ["the design's repository rule", {
    on: ['landing', 'steps'], reviewers: ['codex-acp'], verify: true, minutes: 30, recheck: true, sameAgent: [],
  }, [
    `Before work here lands, \`codex-acp\` reads it, ${COPY}`,
    "It reads each step's work here too, before the chain's next step starts.",
    'It may build and run what this repository declares safe, in that copy.',
    'If no reviewer can read it, you are told so, and nothing waits.',
    'One pass takes at most 30 minutes.',
  ]],
  ["the design's workspace rule", { on: ['landing'], reviewers: ['codex-acp', 'dsh'], required: true, minutes: 20, sameAgent: [] }, [
    `Before work here lands, \`codex-acp\`, else \`dsh\`, reads it, ${COPY}`,
    'If no reviewer can read it, the work waits for you.',
    'One pass takes at most 20 minutes.',
  ]],
  ['steps alone', { on: ['steps'], reviewers: ['codex-acp'], minutes: 20, sameAgent: [] }, [
    `Before a chain's next step starts, \`codex-acp\` reads the work of the step before it here, ${COPY}`,
    'If no reviewer can read it, you are told so, and nothing waits.',
    'One pass takes at most 20 minutes.',
  ]],
  ['three reviewers, no recheck', {
    on: ['landing'], reviewers: ['codex-acp', 'dsh', 'example-reviewer'], minutes: 45, recheck: false, sameAgent: [],
  }, [
    `Before work here lands, \`codex-acp\`, else \`dsh\`, else \`example-reviewer\`, reads it, ${COPY}`,
    'If no reviewer can read it, you are told so, and nothing waits.',
    'One pass takes at most 45 minutes.',
    'Commits made in answer to its findings are not read again.',
  ]],
  ["the working agent's own family, listed after another maker's", {
    on: ['landing'], reviewers: ['codex-acp', 'claude-code-acp'], required: true, minutes: 20, sameAgent: ['claude-code-acp'],
  }, [
    `Before work here lands, \`codex-acp\`, else \`claude-code-acp\`, reads it, ${COPY}`,
    'If no reviewer can read it, the work waits for you.',
    'One pass takes at most 20 minutes.',
    `\`claude-code-acp\` is ${SAME}`,
  ]],
  ["two of the working agent's own family", {
    on: ['landing'], reviewers: ['claude-code', 'claude-code-acp'], minutes: 20, sameAgent: ['claude-code', 'claude-code-acp'],
  }, [
    `Before work here lands, \`claude-code\`, else \`claude-code-acp\`, reads it, ${COPY}`,
    'If no reviewer can read it, you are told so, and nothing waits.',
    'One pass takes at most 20 minutes.',
    `\`claude-code\` and \`claude-code-acp\` are ${SAME}`,
  ]],
];

/**
 * The twins' `gate` rows, copied (XAGENT1f4): what each door says after the rule's sentences, now that the gate reads it
 * (XAGENT1f), at landing, before a chain's next step, and with `verify`. None here says nothing after.
 */
const WAITING = "Where work here waits for another agent's reading, it lands only once that reading is settled, or once you go "
  + 'on without it, `daoris-driver opinion anyway <session>`, or say you looked yourself, `daoris-driver opinion myself <session>`.';
const STEP_WAITING = "Where a chain's next step here waits for another agent's reading of the step before it, it starts only "
  + 'once that reading is settled.';
const SAFE_NOT_HANDED = 'What this repository declares safe is not handed to a reviewer yet.';

type GateRow = [name: string, rule: OpinionRule, sentences: string[]];

/** Rows of the twins' `gate` table, copied: a rule as the bridge carries it, and what each door says after its sentences. */
const GATE: GateRow[] = [
  ['none here says nothing after', { none: true, on: [], reviewers: [] }, []],
  ['one reviewer before landing says what its landing waits for', { on: ['landing'], reviewers: ['codex-acp'] }, [WAITING]],
  ['a required rule says the same', { on: ['landing'], reviewers: ['codex-acp', 'dsh'], required: true }, [WAITING]],
  ["steps alone says what a chain's next step waits for", { on: ['steps'], reviewers: ['codex-acp'] }, [STEP_WAITING]],
  ["the design's repository rule says both, then what verify is not handed yet",
    { on: ['landing', 'steps'], reviewers: ['codex-acp'], verify: true, minutes: 30 }, [WAITING, STEP_WAITING, SAFE_NOT_HANDED]],
];

const t = i18n.t.bind(i18n);

const SET: OpinionRule = {
  on: ['landing', 'steps'], reviewers: ['codex-acp'], required: true, verify: true, minutes: 30, recheck: true, sameAgent: [],
};

const draw = (set: OpinionRule | undefined, owner: 'repository' | 'workspace' = 'repository', onChange = vi.fn()) => {
  render(
    <Tooltip.Provider>
      <OpinionField name="web-app" owner={owner} set={set} onChange={onChange} />
    </Tooltip.Provider>,
  );
  return onChange;
};

describe('what a second-opinion rule says', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it("says, in English, the twins' sentences for each rule, word for word", () => {
    for (const [name, rule, sentences] of SAYS) expect(opinionSays(t, rule), name).toEqual(sentences);
  });

  it("says, in English, the twins' gate sentences for each rule, word for word, and never that nothing reads it", () => {
    for (const [name, rule, sentences] of GATE) {
      expect(opinionGate(t, rule), name).toEqual(sentences);
      expect(opinionRowSays(t, rule), name).not.toMatch(/Declared only|nothing reads it yet/);
    }
  });

  it("says nothing set as today's behaviour, and a rule with what the gate does with it and where it was set", () => {
    expect(opinionRowSays(t, null)).toBe('None: no other agent reads work here.');
    expect(opinionRowSays(t, { ...SET, verify: false, on: ['landing'] }, { source: 'workspace', workspace: 'work' })).toBe(
      `Before work here lands, \`codex-acp\` reads it, ${COPY} If no reviewer can read it, the work waits for you. One pass `
      + `takes at most 30 minutes. ${WAITING} From the workspace work.`);
    // The twins' `gate` rows: nothing waits where a repository has none, so nothing is said after it.
    expect(opinionRowSays(t, { none: true, on: [], reviewers: [] }, { source: 'repository' })).toBe(
      'No second opinion here, whatever its workspace says: no other agent reads work here before it lands. Set for this '
      + 'repository.');
    expect(opinionSummary(t, null)).toBe('no second opinion');
    expect(opinionSummary(t, { none: true, on: [], reviewers: [] })).toBe('no second opinion here');
    expect(opinionSummary(t, SET)).toBe('a second opinion from `codex-acp`, required');
    expect(opinionSummary(t, { ...SET, required: false, reviewers: ['codex-acp', 'dsh'] })).toBe('a second opinion from `codex-acp`, else `dsh`');
  });

  it('says it in Chinese, each name kept as written', async () => {
    await i18n.changeLanguage('zh');

    const said = opinionSays(t, { ...SET, reviewers: ['codex-acp', 'dsh'], sameAgent: [] });
    expect(said[0]).toBe('这里的工作落地之前，由 `codex-acp`，其次 `dsh` 阅读它，用它自己的一份副本，副本里的任何东西都不会被取回；它的发现交给做这项工作的会话，也交给你。');
    expect(said).toContain('一次阅读最多 30 分钟。');
    expect(opinionSummary(t, SET)).toBe('来自 `codex-acp` 的第二意见，必需');

    // The Setup row's sentences after the rule's (XAGENT1f4): what the gate does, its doors as written, nothing after none.
    const gate = opinionGate(t, SET);
    expect(gate).toHaveLength(3);
    expect(gate[0]).toContain('`daoris-driver opinion anyway <session>`');
    expect(gate[0]).toContain('`daoris-driver opinion myself <session>`');
    expect(opinionRowSays(t, SET, { source: 'repository' })).toBe([
      ...opinionSays(t, SET),
      '这里等待另一个智能体阅读的工作，要等那次阅读有了定论，或者你不等它继续（`daoris-driver opinion anyway <session>`），'
        + '或者说明你自己看过了（`daoris-driver opinion myself <session>`），才会落地。',
      '委托链在这里的下一步若在等另一个智能体阅读上一步的工作，要等那次阅读有了定论才会开始。',
      '这个仓库声明为安全的内容目前还不会交给阅读者。',
      '为这个仓库设定。',
    ].join(t('projects.setup.sentenceJoin')));
    expect(opinionRowSays(t, { none: true, on: [], reviewers: [] })).not.toContain('daoris-driver opinion');
    expect(opinionToast(t, 'web-app', { set: { reviewers: ['codex-acp'] } }))
      .toBe('web-app 有了来自 codex-acp 的第二意见。那里等它阅读的工作，要等阅读有了定论或你不等它继续，才会往下走。');
  });

  it('toasts what a change did', () => {
    expect(opinionToast(t, 'web-app', { set: { reviewers: ['codex-acp', 'dsh'] } })).toBe(
      'web-app has a second opinion from codex-acp, dsh. Work there that waits for its reading goes on only once that reading '
      + 'is settled, or you go on without it.');
    expect(opinionToast(t, 'notes-site', { none: true })).toBe('notes-site has no second opinion now, whatever its workspace says.');
    expect(opinionToast(t, 'web-app', { clear: true })).toBe('web-app takes its second-opinion rule from what stands above it again.');
  });
});

describe("a second-opinion rule's field", () => {
  it('opens empty where nothing of its own is set, and sends the reviewers in order with what it reads at', async () => {
    const onChange = draw(undefined);
    const user = userEvent.setup();

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Before work lands' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Before each next step' })).not.toBeChecked();
    expect(screen.getByRole('spinbutton', { name: 'Minutes one pass may take, for web-app' })).toHaveValue(20);
    await user.type(screen.getByRole('textbox', { name: 'Its reviewers for web-app, in the order they are tried' }), 'codex-acp, dsh');
    await user.click(screen.getByRole('checkbox', { name: 'Wait for me if no reviewer can read it' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({
      set: { reviewers: ['codex-acp', 'dsh'], on: ['landing'], required: true, verify: false, minutes: 20, recheck: true },
    });
  });

  it('opens on the rule set here, and sends what was changed with the rest as it stands', async () => {
    const onChange = draw(SET);
    const user = userEvent.setup();

    expect(screen.getByRole('textbox', { name: 'Its reviewers for web-app, in the order they are tried' })).toHaveValue('codex-acp');
    expect(screen.getByRole('checkbox', { name: 'Before each next step' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Let it run what is declared safe' })).toBeChecked();
    const minutes = screen.getByRole('spinbutton', { name: 'Minutes one pass may take, for web-app' });
    await user.clear(minutes);
    await user.type(minutes, '45');
    await user.click(screen.getByRole('checkbox', { name: 'Read the answers again' }));
    await user.click(screen.getByRole('checkbox', { name: 'Before work lands' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onChange).toHaveBeenLastCalledWith({
      set: { reviewers: ['codex-acp'], on: ['steps'], required: true, verify: true, minutes: 45, recheck: false },
    });
  });

  it('holds Save until a reviewer is named and an occasion is chosen, and a repository may say it has none', async () => {
    const onChange = draw(undefined);
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: 'Its reviewers for web-app, in the order they are tried' }), 'dsh');
    await user.click(screen.getByRole('checkbox', { name: 'Before work lands' }));
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'None here' }));
    expect(onChange).toHaveBeenLastCalledWith({ none: true });
  });

  it('offers a workspace no none: a workspace with no second opinion simply sets none', () => {
    draw(undefined, 'workspace');

    expect(screen.queryByRole('button', { name: 'None here' })).toBeNull();
  });
});
