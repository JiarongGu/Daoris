import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { CJK, ELSEWHERE, ENGINE, GAME, NEWBIE, UNDECLARED } from './fixtures';
import { ProjectList } from './ProjectList';

// Repositories' list as a molecule (FRAME1e, D118 §2): rows arrive with what each says, and every press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const list = (over: Partial<Parameters<typeof ProjectList>[0]> = {}) => {
  const onChoose = vi.fn();
  render(
    <ProjectList
      adopted={[{ registration: ENGINE }, { registration: GAME }]}
      outside={[{ registration: NEWBIE, entries: 3 }, { registration: ELSEWHERE, entries: null }]}
      chosen={null}
      onChoose={onChoose}
      {...over}
    />,
  );
  return onChoose;
};

describe("Repositories' list", () => {
  it('puts the adopted first, then the registered not adopted, each group counted, each row a row of its list', () => {
    list();

    expect(screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      'Adopted (2)', 'Registered, not adopted (2)',
    ]);
    for (const item of screen.getAllByRole('listitem')) expect(item).toHaveAttribute('data-list-row');
  });

  it('chooses a repository by its name, and wears the choice on its row', async () => {
    const onChoose = list({ chosen: 'game' });

    await userEvent.click(within(screen.getByRole('listitem', { name: 'newbie' })).getByRole('button'));
    expect(onChoose).toHaveBeenCalledWith('newbie');
    expect(within(screen.getByRole('listitem', { name: 'game' })).getByRole('button')).toHaveAttribute('aria-current', 'true');
  });

  /** D34: adopted and undeclared is addressable, and an asker would be guessing, so its row says so. */
  it("says an adopter's summary as it wrote it, and that one declared nothing", () => {
    list({ adopted: [{ registration: ENGINE }, { registration: UNDECLARED }], outside: [] });

    expect(within(screen.getByRole('listitem', { name: 'engine' })).getByText(ENGINE.summary!)).toHaveAttribute('title', ENGINE.summary);
    expect(within(screen.getByRole('listitem', { name: 'sandbox' })).getByText('no domain declared yet')).toBeInTheDocument();
    // A group with no row is absent, never an empty heading.
    expect(screen.queryByRole('heading', { name: /Registered, not adopted/ })).toBeNull();
  });

  it('says what the index reads of one not adopted, in one sentence for nothing', () => {
    list();

    expect(within(screen.getByRole('listitem', { name: 'newbie' })).getByText('3 entries indexed read-only')).toBeInTheDocument();
    expect(within(screen.getByRole('listitem', { name: 'elsewhere' })).getByText('nothing indexed yet')).toBeInTheDocument();
  });

  it('says its standing as the session list does: held outranks drives here', () => {
    list({ adopted: [{ registration: ENGINE, drivable: true }, { registration: GAME, drivable: true, held: true }], outside: [] });

    expect(within(screen.getByRole('listitem', { name: 'engine' })).getByText('drives here')).toBeInTheDocument();
    const game = within(screen.getByRole('listitem', { name: 'game' }));
    expect(game.getByText('held')).toBeInTheDocument();
    expect(game.queryByText('drives here')).toBeNull();
  });

  /** A repository's name and summary are content: shown as they are, never translated. */
  it('shows a 中文 name as it is', () => {
    list({ adopted: [{ registration: CJK }], outside: [] });

    expect(screen.getByRole('listitem', { name: '渲染管线' })).toHaveTextContent('渲染管线');
  });

  /** D118 §3h: a list that never had an answer says the sentence in place, and is never blank. */
  it('says the sentence of a registry that never answered, in place', () => {
    list({ adopted: [], outside: [], unanswered: 'Daoris could not reach this machine\'s host.' });

    expect(screen.getByText('Daoris could not reach this machine\'s host.')).toBeInTheDocument();
    expect(screen.queryByRole('listitem')).toBeNull();
  });
});
