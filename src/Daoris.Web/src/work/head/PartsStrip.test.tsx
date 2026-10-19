import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import { PartsStrip } from './PartsStrip';

// SESSONE1c (D167 point 5): a session's page lists its entry's parts, oldest first: each with its word, how it began and which
// tree or branch it held; a press shows that part, and every act stays the part shown's.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

describe('the parts strip', () => {
  it('draws nothing for a line of work of one part', () => {
    const lone = { ...APP, parts: [APP.parts[0]!], leading: APP.parts[0]!.session };
    const { container } = render(<PartsStrip entry={lone} attended={lone.leading} rows={rows} onShow={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('lists the parts oldest first, with each one\'s word, how it began and the tree it held', () => {
    render(<PartsStrip entry={APP} attended="ap000003" rows={rows} onShow={() => {}} />);

    const strip = screen.getByRole('navigation', { name: 'Parts' });
    const parts = within(strip).getAllByRole('button');
    expect(parts).toHaveLength(3);
    expect(parts[0]).toHaveTextContent(/failed.*started.*in t-one/);
    expect(parts[1]).toHaveTextContent(/completed.*retried after part 1.*in t-one/);
    expect(parts[2]).toHaveTextContent(/working.*asked by db.*in t-two/);
    expect(within(strip).getByText('Part 3 of 3')).toBeInTheDocument();
  });

  it('marks the part shown, and shows another on a press', async () => {
    const onShow = vi.fn();
    render(<PartsStrip entry={APP} attended="ap000002" rows={rows} onShow={onShow} />);

    const parts = within(screen.getByRole('navigation', { name: 'Parts' })).getAllByRole('button');
    expect(parts[1]).toHaveAttribute('aria-current', 'true');
    expect(parts[0]).not.toHaveAttribute('aria-current');
    expect(screen.getByText('Part 2 of 3')).toBeInTheDocument();

    await userEvent.click(parts[2]!);
    expect(onShow).toHaveBeenCalledWith('ap000003');
  });

  it('says where a part\'s work landed', () => {
    const landed = { ...rows, ap000002: { ...rows.ap000002!, where: { treeGone: true, landed: { repository: 'app', branch: 'daoris/t-one', state: 'standing' as const } } } };
    render(<PartsStrip entry={APP} attended="ap000003" rows={landed} onShow={() => {}} />);

    expect(screen.getAllByRole('button')[1]).toHaveTextContent('landed on daoris/t-one');
  });

  it('says how each part began in Chinese, with the parts counted', async () => {
    await i18n.changeLanguage('zh');
    render(<PartsStrip entry={APP} attended="ap000002" rows={rows} onShow={() => {}} />);

    expect(screen.getByRole('navigation', { name: '各段' })).toBeInTheDocument();
    expect(screen.getByText('第 2 段，共 3 段')).toBeInTheDocument();
    expect(screen.getAllByRole('button')[1]).toHaveTextContent('接第 1 段重试');
    expect(screen.getAllByRole('button')[2]).toHaveTextContent('由 db 委托');
  });
});
