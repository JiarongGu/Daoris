import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { toTurns } from './conversation';
import { ConversationView } from './ConversationView';
import { exchangesOf } from './exchanges';
import { channelHomeOf } from './channelHome';
import { CHANNEL, CLAIM, HELPER_ANSWER, HELPER_QUESTION, nameOf, personsAsk, senderEvents } from './senderChips.fixtures';

/**
 * COMMS1h (the comms design §7): a message is drawn whole once, at its home, and each word's turn is headed by its sender chip, read
 * by the one sender rule. A channel message's home is the channel, so the turn that took it names its poster and opens the channel.
 * MIXAGENT3a-iii: a helper's answer no card folds is that agent's block, with the question it answers.
 */
describe('COMMS1h the sender chips', () => {
  /** `channel: null` draws a page that holds no channel. */
  const draw = ({ asks = [personsAsk()], channel = channelHomeOf(CHANNEL, nameOf, () => {}) }: {
    asks?: ReturnType<typeof personsAsk>[];
    channel?: ReturnType<typeof channelHomeOf> | null;
  } = {}) => {
    const { turns } = toTurns(senderEvents());
    return render(
      <Tooltip.Provider>
        <ConversationView turns={turns} exchanges={exchangesOf(turns, asks, nameOf)} channel={channel ?? undefined} />
      </Tooltip.Provider>,
    );
  };
  const labels = (container: HTMLElement) => [...container.querySelectorAll('[data-sender] > span > span:first-child')]
    .map((label) => label.textContent);

  it('COMMS1h heads every word’s turn with its sender, by the one rule, and the moment it was taken', () => {
    const { container } = draw();
    expect(labels(container)).toEqual([
      'you',
      'Daoris’s words',
      'A claim from billing’s session (Claude Code), on the channel',
      'Codex’s answer · a claim to check, not your words',
      'From the supervisor, on your behalf',
      'Another agent\'s findings',
    ]);
    const times = [...container.querySelectorAll('[data-sender] time')].map((time) => time.getAttribute('dateTime'));
    expect(times).toEqual(['02', '08', '14', '20', '26', '32'].map((minute) => `2026-11-04T16:${minute}:00Z`));
    // The receipts stay what the door observed, under the person's word.
    expect(container.querySelector('[data-word="e1"] [data-receipts]')?.textContent).toMatch(/^sent [^·]+ · taken [^·]+$/);
  });

  it('MIXAGENT3a-iii joins a helper’s answer to the ask it answers by the helper’s session, never by its words', () => {
    const { turns } = toTurns(senderEvents());
    expect(exchangesOf(turns, [personsAsk()], nameOf).answers).toEqual({
      e11: { agent: 'Codex', question: HELPER_QUESTION, asked: '2026-11-04T16:15:00Z', byPerson: true },
    });
    // Another helper's ask answers nothing here; a partner's answer is no helper's.
    expect(exchangesOf(turns, [personsAsk('helper-8')], nameOf).answers).toEqual({});
    const partner = toTurns(senderEvents(), { workOf: () => 'partner' }).turns;
    expect(exchangesOf(partner, [personsAsk()], nameOf).answers).toEqual({});
    // An ask named by no agent is headed by the agent it started on; by neither, as a helper's.
    const unnamed = exchangesOf(turns, [{ ...personsAsk(), agent: null, startedOn: null }], nameOf).answers.e11;
    expect(unnamed?.agent).toBeNull();
  });

  it('MIXAGENT3a-iii draws a helper’s answer to the person’s ask as that agent’s block, with the question it answers', () => {
    const { container } = draw();
    const block = container.querySelector('[data-word="e11"]')!;
    expect(block.textContent).toContain('Codex’s answer · a claim to check, not your words');
    expect(block.querySelector('[data-answers]')?.textContent).toMatch(/^Answers your question of .+: .*Does the import keep/);
    expect(block.textContent).toContain(HELPER_ANSWER);
    expect(screen.queryByText('A helper’s answer, in Daoris’s words')).toBeNull();
    // The answer is drawn once, open, with no fold.
    expect(container.textContent!.split('releases it in its finally block').length - 1).toBe(1);
    expect(block.querySelector('button[aria-expanded]')).toBeNull();
  });

  it('MIXAGENT3a-iii says the session’s own question where its call is not held, and the sender alone where no ask is', () => {
    const { container, unmount } = draw({ asks: [{ ...personsAsk(), askedBy: 'agent' }] });
    expect(container.querySelector('[data-word="e11"] [data-answers]')?.textContent).toMatch(/^Answers this session’s question of /);
    unmount();
    const { container: none } = draw({ asks: [] });
    expect(none.querySelector('[data-word="e11"] [data-answers]')).toBeNull();
    expect(screen.getByText('A helper’s answer, in Daoris’s words')).toBeTruthy();
  });

  it('COMMS1h names a claim’s poster and opens the channel, its home, folding the words that carried it', async () => {
    const opened = vi.fn();
    const { container, unmount } = draw({ channel: channelHomeOf(CHANNEL, nameOf, opened) });
    const claim = container.querySelector('[data-word="e8"]')!;
    expect(claim.querySelector('button[aria-expanded]')?.getAttribute('aria-expanded')).toBe('false');
    await userEvent.click(screen.getByRole('button', { name: 'Open the channel' }));
    expect(opened).toHaveBeenCalledTimes(1);
    unmount();

    // With no channel to open, the claim is named as one and drawn whole, as before.
    const { container: alone } = draw({ channel: null });
    expect(alone.querySelector('[data-word="e8"]')?.textContent).toContain('Another session’s claim, in Daoris’s words');
    expect(screen.queryByRole('button', { name: 'Open the channel' })).toBeNull();
    expect(alone.querySelector('[data-word="e8"] button[aria-expanded]')).toBeNull();
    expect(alone.querySelector('[data-word="e8"] p')?.textContent).toBe(CLAIM);
  });

  it('COMMS1h reads a poster from the channel’s messages alone: the person’s post and an unknown message name none', () => {
    const home = channelHomeOf(CHANNEL, nameOf, () => {})!;
    expect(home.posterOf?.('m4')).toEqual({ repository: 'billing', agent: 'Claude Code' });
    expect(home.posterOf?.('m9')).toBeUndefined();
    const person = { ...CHANNEL, items: [{ ...CHANNEL.items[0]!, person: true }] };
    expect(channelHomeOf(person, nameOf, () => {})!.posterOf?.('m4')).toBeUndefined();
    expect(channelHomeOf(null, nameOf, () => {})).toBeUndefined();
  });

  /**
   * CHANNELJOIN1c (the event-channel design §5.8): a message an agent outside Daoris posted as a member of the channel is headed as that
   * member's, saying it is outside Daoris, and never as a session's, the person's or Daoris's own; in both languages.
   */
  it('CHANNELJOIN1c heads a member’s message as the member’s, outside Daoris, never as a session’s', async () => {
    const member = { ...CHANNEL, items: [{ ...CHANNEL.items[0]!, session: 'o1a2b3c', repository: 'checker', agent: 'codex', outside: true }] };
    const home = channelHomeOf(member, nameOf, () => {})!;
    expect(home.posterOf?.('m4')).toEqual({ repository: 'checker', agent: 'Codex', outside: true });
    const { container, unmount } = draw({ channel: home });
    const label = container.querySelector('[data-word="e8"] [data-sender] > span > span:first-child')?.textContent;
    expect(label).toBe('A claim from an outside agent (checker · Codex), on the channel');
    expect(label).not.toMatch(/session/);
    unmount();

    await i18n.changeLanguage('zh');
    try {
      const { container: chinese } = draw({ channel: home });
      expect(chinese.querySelector('[data-word="e8"] [data-sender] > span > span:first-child')?.textContent)
        .toBe('来自外部智能体（checker · Codex）的说法，发在频道里');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('COMMS1h reaches a chip’s door before the fold it heads, in the keyboard’s order', async () => {
    draw();
    const user = userEvent.setup();
    const order: string[] = [];
    for (let at = 0; at < 16; at += 1) {
      await user.tab();
      order.push(document.activeElement?.textContent ?? '');
    }
    const door = order.indexOf('Open the channel');
    expect(door).toBeGreaterThanOrEqual(0);
    expect(order[door + 1]).toBe('Show all');
  });

  it('COMMS1h says the chips in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      const { container } = draw();
      expect(labels(container)).toEqual([
        '你',
        'Daoris 的话',
        '来自 billing 会话（Claude Code）的说法，发在频道里',
        'Codex 的回答 · 待核实的说法，不是你的话',
        '主管代你答复',
        '另一个智能体的发现',
      ]);
      expect(container.querySelector('[data-word="e11"] [data-answers]')?.textContent).toMatch(/^回答你 .+ 提的问题：/);
      expect(screen.getByRole('button', { name: '打开频道' })).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
