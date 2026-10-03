import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { moment } from '../format';
import { HandedAccount } from './HandedAccount';
import { BARE, CLAIMED, CUT, NEWER, UNREAD } from './handedFixtures';

// Props-only, like every molecule here (CONTEXT1, D143 point 1): what a session was handed, section by section, worded from
// the account's codes in the reader's language, every state reached by passing an account.

const rowOf = (name: string | RegExp) =>
  screen.getByRole('rowheader', { name: typeof name === 'string' ? new RegExp(`^${name}`) : name }).closest('tr')!;

describe('what a session was handed', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('folds to one line saying the instruction’s size and its sections, and opens on a press', async () => {
    render(<HandedAccount account={CLAIMED} />);

    const toggle = screen.getByRole('button', { name: /What it was handed/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveTextContent('What it was handed · 6,786 characters in 15 sections');
    expect(screen.queryByRole('table')).toBeNull();

    await userEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  it('names each section with its size, where it came from and how many of its items it handed', () => {
    render(<HandedAccount account={CLAIMED} defaultOpen />);

    expect(within(rowOf('The quest')).getByText('238')).toBeInTheDocument();
    expect(rowOf('The quest')).toHaveTextContent('the quest, as the service answered it · #abc123');
    expect(rowOf('Your words on the ask')).toHaveTextContent("504the ask's record · #a1b2c3 · 3 of 3");
    expect(rowOf('Your standing answer')).toHaveTextContent(
      `set on this machine for its repository · reports · set ${moment('2026-09-30T02:26:00+00:00', 'en')}`);
    expect(rowOf('The closing note')).toHaveTextContent('655the driver\'s own words');
    // The instruction's words are not here: they are the target's, above.
    expect(screen.queryByText(/daily comparison report/)).toBeNull();
  });

  it('counts the rules handed beside the instruction, and lists what was not handed with why', () => {
    render(<HandedAccount account={CLAIMED} defaultOpen />);

    const rules = rowOf('Permission rules');
    expect(rules).toHaveTextContent('Permission rules');
    expect(within(rules).getByText('handed beside it')).toBeInTheDocument();
    expect(within(rules).getByText('18 rules')).toBeInTheDocument();
    expect(rules).toHaveTextContent("this machine's permission rules");
    expect(screen.getByText('Not handed: Session language (none set on this machine) and Code map (nothing to hand)')).toBeInTheDocument();
  });

  it('says what each bound left out, in the reader’s words, and counts the sections it cut', () => {
    render(<HandedAccount account={CUT} defaultOpen />);

    expect(screen.getByRole('button', { name: /What it was handed/ })).toHaveTextContent('8 with a part left out');
    expect(screen.getByText('2 left out past its bound of 8,000 characters')).toBeInTheDocument();
    expect(screen.getByText(
      `3 older ones, said from ${moment('2026-10-01T03:26:00+00:00', 'en')} to ${moment('2026-10-01T05:26:00+00:00', 'en')}, `
      + "left out past its bound of 8,000 characters; the ask's record keeps every one")).toBeInTheDocument();
    expect(screen.getByText('1 cut at the 2,000-character bound on one word')).toBeInTheDocument();
    expect(screen.getByText('1 cut at the 300-character bound on what one act touches')).toBeInTheDocument();
    expect(screen.getByText("16 left out past its bound of 8,000 characters; the ask's page shows each")).toBeInTheDocument();
    expect(screen.getByText('345 characters left out past its bound of 2,000')).toBeInTheDocument();
    expect(screen.getByText('7 plan steps left out past its bound of 50')).toBeInTheDocument();
    expect(screen.getByText('3 more counted, not named, past the 8 it names')).toBeInTheDocument();
    expect(screen.getByText('1 not on this machine, named without a path')).toBeInTheDocument();
    expect(screen.getByText("this machine's rules file could not be read, so only the defaults were handed")).toBeInTheDocument();
    expect(rowOf('Session language')).toHaveTextContent('set on this machine for its workspace · zh');
    expect(screen.queryByText('shown as recorded')).toBeNull();
  });

  it('says words that could not be read, and that an agent took no rules', () => {
    const { unmount } = render(<HandedAccount account={UNREAD} defaultOpen />);
    expect(screen.getByText('they could not be read for this start, so none after the ask itself were handed')).toBeInTheDocument();
    expect(screen.getByText(/Go-aheads \(the service answered none\)/)).toBeInTheDocument();
    unmount();

    render(<HandedAccount account={BARE} defaultOpen />);
    expect(screen.getByText(/Permission rules \(its agent takes no rules from Daoris\)/)).toBeInTheDocument();
    expect(screen.queryByRole('rowheader', { name: /Permission rules/ })).toBeNull();
  });

  /** D143 point 3: a target handed before the account was kept says so, rather than showing nothing. */
  it('says a target from before kept no sections', () => {
    render(<HandedAccount account={null} />);

    expect(screen.getByText('Its sections were not kept: it was handed before Daoris kept them.')).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  /** A newer driver's codes: its own English, marked, beside what this page words. */
  it('shows a code it has no words for as the driver recorded it, marked', () => {
    render(<HandedAccount account={NEWER} defaultOpen />);

    expect(screen.getAllByText('shown as recorded')).toHaveLength(3);
    expect(screen.getByText("what the workspace's knowledge recalled: 790 characters, 4 entries by meaning")).toBeInTheDocument();
    expect(screen.getByText('1 word left out by the session budget')).toBeInTheDocument();
    expect(screen.getByText('the standing answer: held for the person')).toBeInTheDocument();
    expect(rowOf('The quest')).toHaveTextContent('#abc123');
  });

  it('words it in 中文, leaving ids and names as written', async () => {
    await i18n.changeLanguage('zh');
    render(<HandedAccount account={CLAIMED} defaultOpen />);

    expect(screen.getByRole('button', { name: /交付给它的内容/ })).toHaveTextContent('15 个部分，共 6,786 个字符');
    expect(rowOf(/^委托$/)).toHaveTextContent('服务给出的委托 · #abc123');
    expect(within(rowOf('权限规则')).getByText('18 条规则')).toBeInTheDocument();
    expect(screen.getByText(/^未交付：会话语言（本机没有设定）/)).toBeInTheDocument();
    expect(screen.queryByText(/Permission rules/)).toBeNull();
  });
});
