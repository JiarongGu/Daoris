import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../data/api';
import i18n from '../locales/i18n';
import { AskPage } from './AskPage';
import { INTAKE_ASKED, PROPOSED, PUBLISHED } from './fixtures';

// ASKANSWER1: a proposed ask is waiting on the person, and its answer is its page's. So *Close ask* and *Delete…*, which
// are any caller's under D284, stand in its header as presses of their own rather than behind its ⋯; every other state keeps them there.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const page = (ask: Ask, over: Partial<Parameters<typeof AskPage>[0]> = {}) => {
  const props = {
    ask, receivers: ['engine'], questTitles: {}, onPublish: vi.fn(), onClose: vi.fn(), onDelete: vi.fn(), onOpenQuest: vi.fn(), ...over,
  };
  render(<AskPage {...props} />);
  return { ...props, head: screen.getByRole('main').querySelector('header')! };
};

const menu = async () => {
  const user = userEvent.setup();
  screen.getByRole('button', { name: i18n.t('work.head.more') }).focus();
  await user.keyboard('{Enter}');
  return (await screen.findAllByRole('menuitem')).map((item) => item.textContent);
};

describe('a proposed ask’s page (ASKANSWER1)', () => {
  afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

  it('draws Close ask and Delete… in its header, and none of them in its ⋯', async () => {
    const { head } = page({ ...PROPOSED, deletable: true });

    expect(within(head).getByRole('button', { name: 'Close ask' })).toBeInTheDocument();
    expect(within(head).getByRole('button', { name: 'Delete…' })).toBeInTheDocument();
    expect(await menu()).toEqual(['Copy ask ID']);
  });

  it('closes with a reason from the header press, and not without one', async () => {
    const { head, onClose } = page({ ...PROPOSED, deletable: true });

    await userEvent.click(within(head).getByRole('button', { name: 'Close ask' }));
    const confirm = screen.getByRole('button', { name: 'Close with this reason' });
    expect(confirm).toBeDisabled();
    await userEvent.type(screen.getByLabelText('why — what became of it'), 'Answered elsewhere.');
    await userEvent.click(confirm);

    expect(onClose).toHaveBeenCalledWith('Answered elsewhere.', expect.objectContaining({ done: expect.any(Function) }));
  });

  it('asks once from the header press, then deletes', async () => {
    const { head, onDelete } = page({ ...PROPOSED, deletable: true });

    await userEvent.click(within(head).getByRole('button', { name: 'Delete…' }));
    expect(onDelete).not.toHaveBeenCalled();
    await userEvent.click(within(screen.getByRole('group', { name: 'delete this ask' })).getByRole('button', { name: 'Delete ask' }));

    expect(onDelete).toHaveBeenCalledTimes(1);
  });

  it('draws a press only while its question is open: the one asking is not offered twice', async () => {
    const { head } = page({ ...PROPOSED, deletable: true });

    await userEvent.click(within(head).getByRole('button', { name: 'Close ask' }));
    expect(within(head).queryByRole('button', { name: 'Close ask' })).toBeNull();
    expect(within(head).getByRole('button', { name: 'Delete…' })).toBeInTheDocument();
  });

  it('offers no Delete… where the service does not say the ask may go, and still offers Close ask', () => {
    const { head } = page({ ...PROPOSED, deletable: false });

    expect(within(head).getByRole('button', { name: 'Close ask' })).toBeInTheDocument();
    expect(within(head).queryByRole('button', { name: 'Delete…' })).toBeNull();
  });

  it('offers no Delete… where the page holds no door to delete', () => {
    const { head } = page({ ...PROPOSED, deletable: true }, { onDelete: undefined });

    expect(within(head).queryByRole('button', { name: 'Delete…' })).toBeNull();
  });

  it('keeps them in its ⋯ for an ask that is published, as before', async () => {
    const { head } = page({ ...PUBLISHED, deletable: true });

    expect(within(head).queryByRole('button', { name: 'Close ask' })).toBeNull();
    expect(await menu()).toEqual(['Close ask', 'Delete…', 'Copy ask ID']);
  });

  it('offers them on a proposal whose intake parked, which is the ask the intake is waiting on', () => {
    const { head } = page({ ...INTAKE_ASKED, deletable: true });

    expect(within(head).getByRole('button', { name: 'Close ask' })).toBeInTheDocument();
    expect(within(head).getByRole('button', { name: 'Delete…' })).toBeInTheDocument();
  });

  it('says them in 中文', async () => {
    await i18n.changeLanguage('zh');
    const { head } = page({ ...PROPOSED, deletable: true });

    expect(within(head).getByRole('button', { name: i18n.t('asks.record.close') })).toBeInTheDocument();
    expect(within(head).getByRole('button', { name: i18n.t('asks.record.delete') })).toBeInTheDocument();
  });
});
