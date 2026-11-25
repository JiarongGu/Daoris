import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as draw, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { PlaceRiskActs, PlaceRiskActsSection, type RiskActsBridge, type RiskActsView } from './PlaceRiskActs';

const render = (ui: ReactElement) => draw(<Tooltip.Provider>{ui}</Tooltip.Provider>);

const view: RiskActsView = {
  place: 'repository/Aurora', mode: 'restricted', revision: 'r1', unknown: [],
  rows: [
    { kind: 'work.done', class: 'none', source: 'seed', reason: 'no risk declared', passes: 4, gates: 0, locked: false },
    { kind: 'spend.money', class: 'owner', source: 'repository/Aurora', reason: 'set here', passes: 0, gates: 2, locked: false },
    { kind: 'reach.widen', class: 'role', by: 'approver', source: 'seed', reason: 'seed', answers: { role: 'approver', holder: 'agent:codex' }, passes: 1, gates: 3, locked: false },
    { kind: 'policy.change', class: 'owner', source: 'fixed', reason: 'fixed', passes: 0, gates: 0, locked: true },
  ],
  shifts: [],
};

afterEach(() => i18n.changeLanguage('en'));

describe('a place’s act table', () => {
  it('says per kind its class, where it comes from, who answers by the cast and the last seven days, and locks policy.change', () => {
    render(<PlaceRiskActs name="Aurora" twin="twin" view={view} onPick={vi.fn()} onSave={vi.fn()} onCancel={vi.fn()} />);

    const list = screen.getByRole('list', { name: 'Acts at Aurora' });
    const money = within(list).getByText('spend.money').closest('li')!;
    expect(within(money).getByRole('radio', { name: 'You' })).toBeChecked();
    expect(money).toHaveTextContent(/Set at repository\/Aurora/);
    expect(money).toHaveTextContent(/0 passed, 2 asked/);
    const reach = within(list).getByText('reach.widen').closest('li')!;
    expect(within(reach).getByRole('radio', { name: 'A role' })).toBeChecked();
    expect(reach).toHaveTextContent(/approver, filled by codex/);
    expect(within(list).getByText('work.done').closest('li')).toHaveTextContent(/Daoris’s seed/);
    const locked = within(list).getByText('policy.change').closest('li')!;
    expect(within(locked).queryByRole('radio')).toBeNull();
    expect(locked).toHaveTextContent(/Fixed: only you change who holds power/);
  });

  it('picks a class without sending it, previews the waits it opens and releases, and sends on Save only', async () => {
    const onPick = vi.fn();
    const onSave = vi.fn();
    const onCancel = vi.fn();
    const pending = { kind: 'work.done', class: 'owner' };
    const shifts = [{ place: 'repository/Aurora', act: 'work.done', mode: 'restricted', from: 'none', to: 'owner', effect: 'opens' }];
    const { rerender } = render(<PlaceRiskActs name="Aurora" twin="twin" view={view} onPick={onPick} onSave={onSave} onCancel={onCancel} />);

    await userEvent.click(within(screen.getByText('work.done').closest('li')!).getByRole('radio', { name: 'You' }));
    expect(onPick).toHaveBeenCalledWith('work.done', 'owner');
    expect(onSave).not.toHaveBeenCalled();

    rerender(<Tooltip.Provider><PlaceRiskActs name="Aurora" twin="twin" view={{ ...view, shifts }} pending={pending} onPick={onPick} onSave={onSave} onCancel={onCancel} /></Tooltip.Provider>);
    expect(screen.getByRole('status')).toHaveTextContent(/work\.done.*restricted access.*opens a wait/);
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('says a pending change that opens and releases nothing, and that a policy that does not read reads every act as yours', () => {
    const { rerender } = render(<PlaceRiskActs name="Aurora" twin="twin" view={view} pending={{ kind: 'work.done', class: 'none' }} onPick={vi.fn()} onSave={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent(/opens and releases no wait/);

    rerender(<Tooltip.Provider><PlaceRiskActs name="Aurora" twin="twin" view={{ ...view, fault: '`risk` is not an object of places' }} onPick={vi.fn()} onSave={vi.fn()} onCancel={vi.fn()} /></Tooltip.Provider>);
    expect(screen.getByRole('alert')).toHaveTextContent(/does not read.*every act reads as yours/);
    expect(screen.queryAllByRole('radio')).toHaveLength(0);
  });

  it('says it all in Chinese with the glossary’s words', async () => {
    await i18n.changeLanguage('zh');
    render(<PlaceRiskActs name="Aurora" twin="twin" view={view} pending={{ kind: 'work.done', class: 'owner' }} onPick={vi.fn()} onSave={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.getByRole('list', { name: 'Aurora 的操作' })).toBeInTheDocument();
    expect(screen.getByText('spend.money')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '保存' })).toBeInTheDocument();
    expect(screen.getByText(/固定：只有你/)).toBeInTheDocument();
  });
});

describe('the act table’s section', () => {
  const table = (edit: unknown) => ({ data: edit ? { ...view, shifts: [{ place: 'repository/Aurora', act: 'work.done', mode: 'restricted', from: 'none', to: 'owner', effect: 'opens' }] } : view });

  it('reads the table, previews a picked change through the read, and saves it at the revision it read', async () => {
    const mutate = vi.fn();
    const useTable = vi.fn((_workspace: string | null, _repository: string | null, edit: unknown) => table(edit));
    const bridge: RiskActsBridge = { useTable, useSet: () => ({ mutate, isPending: false }) };
    render(<PlaceRiskActsSection bridge={bridge} workspace="work" repository="Aurora" />);

    await userEvent.click(within(screen.getByText('work.done').closest('li')!).getByRole('radio', { name: 'You' }));
    expect(useTable).toHaveBeenLastCalledWith('work', 'Aurora', { place: 'repository/Aurora', act: 'work.done', class: 'owner' });
    expect(screen.getByRole('status')).toHaveTextContent(/opens a wait/);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(mutate).toHaveBeenCalledWith({ edits: [{ place: 'repository/Aurora', act: 'work.done', class: 'owner' }], revision: 'r1' }, expect.anything());
  });

  it('names the machine’s and a workspace’s places', () => {
    const useTable = vi.fn((_w: string | null, _r: string | null, _e: unknown) => ({ data: view }));
    render(<PlaceRiskActsSection bridge={{ useTable, useSet: () => ({ mutate: vi.fn(), isPending: false }) }} workspace="work" repository={null} />);
    expect(useTable).toHaveBeenCalledWith('work', null, null);
  });
});
