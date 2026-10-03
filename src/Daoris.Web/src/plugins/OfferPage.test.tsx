import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { code } from '../test/code';
import { cappedBlocks } from '../test/measure';
import type { OfferShown } from './catalog';
import { OfferPage } from './OfferPage';

// One of Daoris's own plugins, not installed here (PLUGUI1b, D119 §3.2): a molecule, every state by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const OFFER: OfferShown = {
  id: 'land-github', name: 'Land on GitHub', version: '0.2.0', description: 'Pushes a landed branch and opens its pull request.',
  problem: null, harnesses: [], points: ['work/land'], servers: ['github'],
  needs: ['`gh` on the PATH, signed in', 'A repository whose origin is on GitHub'], installed: false,
};

const KIT = [{ name: 'work/land', kind: 'act' }];
const header = () => screen.getByRole('heading', { level: 1 }).closest('header')!;

describe("an offer's page", () => {
  it('heads with its name, its version, not installed and Install', async () => {
    const onInstall = vi.fn();
    render(<OfferPage offer={OFFER} kitPoints={KIT} onInstall={onInstall} />);

    expect(screen.getByRole('heading', { level: 1, name: 'Land on GitHub' })).toBeInTheDocument();
    expect(within(header()).getByText('0.2.0')).toBeInTheDocument();
    expect(within(header()).getByText('not installed')).toBeInTheDocument();
    await userEvent.click(within(header()).getByRole('button', { name: 'Install land-github' }));
    expect(onInstall).toHaveBeenCalledWith('land-github');
  });

  /** D140 §2 and D141: the detail's blocks share the pane's width, none keeping a measure of its own. */
  it('lets every block take the pane\'s width', () => {
    const { container } = render(<OfferPage offer={OFFER} kitPoints={KIT} onInstall={vi.fn()} />);
    expect(cappedBlocks(container)).toEqual([]);
  });

  /** D140 §2: an offer wears its icon too, and the monogram it will wear installed, since both are keyed by its id. */
  it('leads its header with its icon', () => {
    render(<OfferPage offer={OFFER} kitPoints={KIT} onInstall={vi.fn()} />);

    const monogram = header().querySelector('[data-hue]')!;
    expect(monogram.textContent).toBe('L');
    expect(monogram.getAttribute('data-hue')).toBe('slate');
    expect(monogram.className).toContain('size-12');
  });

  it('shows what its manifest declares and what its README says it needs, verbatim', () => {
    render(<OfferPage offer={OFFER} kitPoints={KIT} onInstall={vi.fn()} />);

    expect(screen.getByRole('region', { name: 'Points' })).toHaveTextContent('work/landAn act, once a landing has made its branch');
    expect(screen.getByRole('region', { name: 'Servers' })).toHaveTextContent('github');
    expect(screen.queryByRole('region', { name: 'Agents' })).toBeNull();
    const needs = screen.getByRole('region', { name: 'What it needs' });
    expect(within(needs).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'gh on the PATH, signed in', 'A repository whose origin is on GitHub',
    ]);
  });

  /** Installing runs nothing, and a landing plugin runs only where a landing rule names it (D103). */
  it('says installing runs nothing, and names its terminal twin', () => {
    render(<OfferPage offer={OFFER} kitPoints={KIT} onInstall={vi.fn()} />);

    expect(screen.getByText(/Installing runs nothing/)).toBeInTheDocument();
    expect(screen.getByText(code(/daoris plugin add --offer land-github/))).toBeInTheDocument();
  });

  it("gives the driver's sentence in Install's place for one that cannot be installed as it stands", () => {
    render(<OfferPage offer={{ ...OFFER, problem: 'needs plugin API 2, and this build speaks 1.' }} kitPoints={KIT} onInstall={vi.fn()} />);

    expect(within(header()).queryByRole('button')).toBeNull();
    expect(screen.getByText('needs plugin API 2, and this build speaks 1.').closest('p')!.className).toMatch(/\bborder-warn\b/);
  });

  it('holds Install while an install is on its way', () => {
    render(<OfferPage offer={OFFER} kitPoints={KIT} installing onInstall={vi.fn()} />);
    expect(within(header()).getByRole('button', { name: 'Install land-github' })).toBeDisabled();
  });
});
