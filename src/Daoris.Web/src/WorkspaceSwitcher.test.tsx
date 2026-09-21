import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { WorkspaceSwitcher } from './WorkspaceSwitcher';

// A props-only component (the component plan's rule: no hook from `./queries` or `./shell`), so every
// state is reachable by passing it — which is what these four do.

const show = (node: React.ReactElement) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('WorkspaceSwitcher', () => {
  it('renders nothing while the deployment holds one workspace — silence is today, byte for byte', () => {
    const { container } = show(
      <WorkspaceSwitcher workspaces={['default']} value={null} onChange={() => {}} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('states the scope it spans when nothing is chosen — the D24 shape, never a silent mix', () => {
    show(<WorkspaceSwitcher workspaces={['default', 'aurora']} value={null} onChange={() => {}} />);
    expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('every workspace · 2');
  });

  it('shows the chosen workspace as the current value', () => {
    show(<WorkspaceSwitcher workspaces={['default', 'aurora']} value="aurora" onChange={() => {}} />);
    expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('aurora');
  });

  it('choosing a workspace hands back its name; choosing every hands back null', async () => {
    const onChange = vi.fn();
    const { rerender } = show(
      <WorkspaceSwitcher workspaces={['default', 'aurora']} value={null} onChange={onChange} />,
    );

    await userEvent.click(screen.getByRole('combobox', { name: 'workspace' }));
    await userEvent.click(await screen.findByRole('option', { name: 'aurora' }));
    expect(onChange).toHaveBeenLastCalledWith('aurora');

    rerender(
      <Tooltip.Provider>
        <WorkspaceSwitcher workspaces={['default', 'aurora']} value="aurora" onChange={onChange} />
      </Tooltip.Provider>,
    );
    await userEvent.click(screen.getByRole('combobox', { name: 'workspace' }));
    await userEvent.click(await screen.findByRole('option', { name: /every workspace/ }));
    expect(onChange).toHaveBeenLastCalledWith(null);
  });
});
