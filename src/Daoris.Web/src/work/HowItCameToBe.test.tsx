import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { moment } from '../format';
import { HowItCameToBe } from './HowItCameToBe';
import {
  answer, MISSING_CHAIN, NO_ASK_CHAIN, NOTHING, OLD_CHAIN, QUEST_CHAIN, SESSION_CHAIN, UNREAD_ANSWER, WAITING_CHAIN,
} from './traceFixtures';

// Props-only, like every molecule here (TRACE1b, D143): how a session or a quest came to be, worded from the chain's codes in
// the reader's language, someone's words as written, every state reached by passing an answer.

const section = () => screen.getByRole('region', { name: 'How this came to be' });
const step = (name: string | RegExp) => screen.getByRole('button', { name }).closest('li')!;
const at = (iso: string) => moment(iso, 'en');

describe('how this came to be', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('is folded by default, reads nothing shown, and a press asks the page to open it', async () => {
    const onToggle = vi.fn();
    render(<HowItCameToBe kind="quest" id="q1" open={false} onToggle={onToggle} />);

    const toggle = within(section()).getByRole('button', { name: /How this came to be/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveTextContent(/^How this came to be$/);
    expect(screen.queryByRole('list')).toBeNull();

    await userEvent.click(toggle);
    expect(onToggle).toHaveBeenCalledOnce();
  });

  it('once read, tells its story on the folded line, from the ask through the quest to its sessions', () => {
    render(<HowItCameToBe kind="quest" id="q1" open={false} onToggle={() => {}} answer={answer(QUEST_CHAIN)} />);

    expect(within(section()).getByRole('button', { name: /How this came to be/ }))
      .toHaveTextContent('How this came to be · ask #a1 → quest #q1 → 2 sessions');
  });

  it('reads the ask: the person’s words as written, each with how and when, and its go-aheads worded', () => {
    render(<HowItCameToBe kind="quest" id="q1" open onToggle={() => {}} answer={answer(QUEST_CHAIN)} onAsk={() => {}} />);

    const ask = step('Ask #a1');
    expect(ask).toHaveTextContent("from the service's ask record");
    expect(within(ask).getByText('The dashboard figure reads zero. Use the v3 bridge, as common-report does.').tagName).toBe('BLOCKQUOTE');
    expect(ask).toHaveTextContent(`${at('2026-10-03T09:20:00Z')} · answered session s1 · on quest #q1`);
    expect(ask).toHaveTextContent(`#1 · write on production “dashboard configuration” · approved ${at('2026-10-03T09:30:00Z')} “run the put”`);
    expect(ask).toHaveTextContent('#2 · push on origin “main” · waiting on you');
    expect(ask).toHaveTextContent('Its intake: session i1 · completed');
  });

  it('reads the quest: what the person required in their words, its check, and how its done answered', () => {
    render(<HowItCameToBe kind="quest" id="q1" open onToggle={() => {}} answer={answer(QUEST_CHAIN)} />);

    const quest = screen.getByText('Quest #q1').closest('li')!;
    expect(quest).toHaveTextContent('from the service\'s quest, as its moves replay');
    expect(within(quest).getByText('use the v3 bridge').tagName).toBe('BLOCKQUOTE');
    expect(quest).toHaveTextContent('1 · check: the report reads through bridge v3');
    expect(quest).toHaveTextContent('met: the tile reads through v3 now');
    expect(quest).toHaveTextContent('departed: the bridge has no tile feed · on your words “as common-report does”');
    expect(quest).toHaveTextContent('Asked by ask #a1.');
    expect(quest).toHaveTextContent('Published by session i1.');
  });

  it('reads each session oldest first: what ran it, its tree by name, the carry-on, what it was handed, its landing', () => {
    render(<HowItCameToBe kind="quest" id="q1" open onToggle={() => {}} answer={answer(QUEST_CHAIN)} onSession={() => {}} />);

    const steps = screen.getAllByRole('listitem').filter((item) => /^Session s\d/.test(item.textContent ?? ''));
    expect(steps.map((item) => item.textContent?.slice(0, 10))).toEqual(['Session s1', 'Session s2']);
    const [first, second] = steps as [HTMLElement, HTMLElement];
    expect(first).toHaveTextContent('by an account\'s limit');
    expect(first).toHaveTextContent('agent claude-code · version 2.1.3 · account personal');
    expect(first).toHaveTextContent('The first session on its quest.');
    expect(second).toHaveTextContent('tree dashboards-q1 · grew from abc1234');
    expect(second).toHaveTextContent(`After session s1, which ended failed ${at('2026-10-03T09:15:00Z')}, in the same tree.`);
    // The driver's own start note is its English, marked; what it was handed is the account's fold.
    expect(second).toHaveTextContent('shown as recorded');
    expect(second).toHaveTextContent('carried on from session s1 on work');
    expect(second).toHaveTextContent('Handed an instruction of 6,786 characters');
    expect(within(second).getByRole('button', { name: /What it was handed/ })).toHaveAttribute('aria-expanded', 'false');
    expect(second).toHaveTextContent('1a2b3c4 Read the figure through v3');
    expect(second).toHaveTextContent(`Landed on feature/q1-fix-the-dashboard-figure from main, ${at('2026-10-03T10:05:00Z')} · pushed by the plugin github`);
    expect(within(second).getByRole('link', { name: 'its pull request' })).toHaveAttribute('href', 'https://example.test/pull/7');
  });

  it('says who accepted a landing and the rule it was made under, and its entry on the due list try by try (LAND2b)', () => {
    render(<HowItCameToBe kind="session" id="s2" open onToggle={() => {}} answer={answer(SESSION_CHAIN)} />);

    const session = screen.getByText('Session s2').closest('li')!;
    expect(session).toHaveTextContent(
      'accepted automatically when its quest was done · under the workspace\'s rule · naming the plugin github · accepting automatically');
    const due = within(session).getByText('Due to land automatically').parentElement!;
    expect(due).toHaveTextContent(`due since ${at('2026-10-03T10:00:00Z')} · closed ${at('2026-10-03T10:05:00Z')}`);
    expect(due).toHaveTextContent("from this machine's list of work due to land");
    expect(due).toHaveTextContent(`${at('2026-10-03T10:01:00Z')} · its tree holds uncommitted work · 2 uncommitted paths · at 9f8e7d6`);
    expect(due).toHaveTextContent(`${at('2026-10-03T10:05:00Z')} · landed · branch feature/q1-fix-the-dashboard-figure · 2 commits · at 1a2b3c4`);
  });

  it('says a session still waiting to land, and an earlier landing from before who accepted it was kept', () => {
    const { rerender } = render(<HowItCameToBe kind="session" id="s8" open onToggle={() => {}} answer={answer(WAITING_CHAIN)} />);

    const session = screen.getByText('Session s8').closest('li')!;
    expect(session).toHaveTextContent('who accepted it is not kept: it landed before landings kept it');
    expect(session).not.toHaveTextContent('under the');
    expect(session).toHaveTextContent(`due since ${at('2026-10-03T12:30:00Z')} · still waiting`);
    expect(session).toHaveTextContent('the branch already stands, and Daoris does not move it · branch feature/q8-tidy-the-legend');

    const untried = structuredClone(WAITING_CHAIN);
    untried.links[0]!.session!.landing!.due!.tries = [];
    rerender(<HowItCameToBe kind="session" id="s8" open onToggle={() => {}} answer={answer(untried)} />);
    expect(screen.getByText('Not tried yet.')).toBeInTheDocument();
  });

  it('says what stood when each session started, by the moments kept', () => {
    render(<HowItCameToBe kind="quest" id="q1" open onToggle={() => {}} answer={answer(QUEST_CHAIN)} />);

    const [first, second] = screen.getAllByRole('listitem').filter((item) => /^Session s\d/.test(item.textContent ?? '')) as [HTMLElement, HTMLElement];
    expect(first).toHaveTextContent(`Standing answer for dashboards: set ${at('2026-10-03T09:25:00Z')}, after it started. What stood before is not kept.`);
    expect(first).toHaveTextContent('Go-ahead #1: first asked after it started · by this session');
    expect(first).toHaveTextContent('1 of your 3 words on ask #a1 said before it started');
    expect(second).toHaveTextContent(`Standing answer for dashboards, set ${at('2026-10-03T09:25:00Z')}, before it started:`);
    expect(within(second).getByText('test on dev first, never production').tagName).toBe('BLOCKQUOTE');
    expect(second).toHaveTextContent(`Go-ahead #1: approved ${at('2026-10-03T09:30:00Z')}, before it started`);
  });

  it('says each link nothing keeps missing, where it would have been, and guesses none', () => {
    render(<HowItCameToBe kind="session" id="s3" open onToggle={() => {}} answer={answer(MISSING_CHAIN)} />);

    const missing = screen.getAllByText('missing').map((tag) => tag.parentElement!.textContent);
    expect(missing).toEqual([
      'missingAsk #a9 is not on the service: it holds no ask by that id, though quest #q3 names it as where it came from.',
      'missingThis machine holds no record of it.',
      'missingRules handed: not kept past its run, since the file goes when its run ends.',
      'missingLanding: no branch landing names it, and no record of it here could say whether its work was accepted.',
      'missingIts record says no moment it opened, so nothing is read against one.',
    ]);
    const session = screen.getByText('Session s3').closest('li')!;
    expect(session).toHaveTextContent('agent claude-code · version not recorded · no account named: the agent\'s own sign-in');
    expect(session).toHaveTextContent('tree not recorded · base commit not recorded');
    expect(session).toHaveTextContent('not by you: the driver closed, or the clean-up found it orphaned');
    expect(screen.getByText('Quest #q3').closest('li')).toHaveTextContent('not answered yet: a done answers it');
  });

  it('reads a quest no ask asked, a running session’s rules by count, and an instruction its record cut', () => {
    render(<HowItCameToBe kind="quest" id="q5" open onToggle={() => {}} answer={answer(NO_ASK_CHAIN)} />);

    expect(screen.getByText('Quest #q5').closest('li')).toHaveTextContent(
      'On no ask: dashboards asked it, so no words of yours, go-aheads or requirements hold it.');
    const session = screen.getByText('Session s5').closest('li')!;
    expect(session).toHaveTextContent('Rules handed: 14 allowed, 0 asked, 4 denied · 1 hard denial beside the agent\'s own · the tree guard holds it to its tree');
    expect(session).toHaveTextContent('Handed an instruction of 70,000 characters, of which its record keeps the first 65,536');
    expect(session).toHaveTextContent('Its sections were not kept: it was handed before Daoris kept them.');
    expect(session).toHaveTextContent('Standing answer for reports: none set now, and one cleared since is not kept.');
  });

  it('reads an ask from before words were kept, a merge kept only as the acceptance, and a teammate’s record', () => {
    render(<HowItCameToBe kind="quest" id="q7" open onToggle={() => {}} answer={answer(OLD_CHAIN)} />);

    const ask = screen.getByText('Ask #a7').closest('li')!;
    expect(ask).toHaveTextContent('The service answers none of your words');
    expect(within(ask).getByText('Rename the export button to "Download".').tagName).toBe('BLOCKQUOTE');
    expect(ask).toHaveTextContent('The service answers no go-aheads');
    const merged = screen.getByText('Session s7').closest('li')!;
    expect(merged).toHaveTextContent(`Your acceptance, ${at('2026-10-02T09:50:00Z')}`);
    expect(merged).toHaveTextContent('Landing: merged into the line. Its record keeps your acceptance, and the merge\'s own commit is not kept.');
    const teammate = screen.getByText('Session laptop/s8').closest('li')!;
    expect(teammate).toHaveTextContent('A teammate\'s record: it ran on another machine');
    // Its account is its own machine's: neither named nor said to be the agent's own sign-in.
    expect(teammate).not.toHaveTextContent('no account named');
    expect(teammate).toHaveTextContent('agent claude-code · version 2.1.0');
  });

  it('says a store that did not answer, and names the terminal’s door, which says why', () => {
    render(<HowItCameToBe kind="quest" id="q1" open onToggle={() => {}} answer={UNREAD_ANSWER} />);

    expect(section()).toHaveTextContent('The service\'s session records could not be read, so no session is read here.');
    expect(section()).toHaveTextContent('Its session records could not be read.');
    expect(section()).toHaveTextContent('A terminal reads the same chain with daoris-driver trace quest q1');
  });

  it('says nothing names it, reading while it reads, and the route’s refusal in its own words', () => {
    const { rerender } = render(<HowItCameToBe kind="session" id="s9" open onToggle={() => {}} answer={NOTHING} />);
    expect(section()).toHaveTextContent('Nothing on the record names this session.');

    rerender(<HowItCameToBe kind="session" id="s9" open onToggle={() => {}} reading />);
    expect(screen.getByRole('status')).toHaveTextContent('Reading the record…');

    rerender(<HowItCameToBe kind="session" id="s9" open onToggle={() => {}} refusal="the driver is still coming up." />);
    expect(section()).toHaveTextContent('the driver is still coming up.');
  });

  it('opens another session or quest through its door, and the page’s own id is no door', async () => {
    const onSession = vi.fn();
    const onQuest = vi.fn();
    render(<HowItCameToBe kind="session" id="s2" open onToggle={() => {}} answer={answer(SESSION_CHAIN)} onSession={onSession} onQuest={onQuest} />);

    expect(screen.queryByRole('button', { name: 'Session s2' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 's1' }));
    expect(onSession).toHaveBeenCalledWith('s1');
    await userEvent.click(screen.getByRole('button', { name: 'Quest #q1' }));
    expect(onQuest).toHaveBeenCalledWith('q1');
  });

  it('words every line in 中文, and shows someone’s words as written', async () => {
    await i18n.changeLanguage('zh');
    render(<HowItCameToBe kind="session" id="s3" open onToggle={() => {}} answer={answer(MISSING_CHAIN)} />);

    expect(screen.getByRole('region', { name: '来龙去脉' })).toBeInTheDocument();
    expect(screen.getAllByText('缺失')).toHaveLength(5);
    expect(screen.getByText('本机没有它的记录。')).toBeInTheDocument();
    // A requirement's quote is the person's, never translated.
    expect(screen.getByText('keep the colours').tagName).toBe('BLOCKQUOTE');
  });
});
