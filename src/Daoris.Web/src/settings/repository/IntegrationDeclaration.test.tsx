import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import {
  INTEGRATION_ACCEPTED, INTEGRATION_CHANGED, INTEGRATION_UNACCEPTED, INTEGRATION_UNREAD,
} from '../../workflow/fixtures';
import { IntegrationDeclaration } from './IntegrationDeclaration';

// A repository's integration declaration on its Setup, beside the landed
// route: where its acceptance stands, the declaration as written where it waits, and the person's Accept and withdraw. A molecule.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe("a repository's integration declaration on its Setup", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says an accepted declaration with no blob id, and offers to withdraw it', async () => {
    const onAccept = vi.fn();
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_ACCEPTED} onAccept={onAccept} />);

    expect(screen.getByText('Checks the line declares')).toBeInTheDocument();
    expect(screen.getByText(/^Accepted\./)).toHaveTextContent("Accepted. The line's checks run as you accepted them.");
    expect(screen.queryByRole('button', { name: 'Accept' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Withdraw' }));
    expect(onAccept).toHaveBeenCalledWith({ clear: true }, expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
  });

  it('shows a changed declaration as written, and Accept sends the blob it shows', async () => {
    const onAccept = vi.fn();
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_CHANGED} onAccept={onAccept} />);

    expect(screen.getByText(/changed since you accepted them/)).toBeInTheDocument();
    expect(screen.getByLabelText('daoris.gates.json on main')).toHaveTextContent('"scratch": ["dist/", "coverage/"]');
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));
    expect(onAccept).toHaveBeenCalledWith({ gates: INTEGRATION_CHANGED.tip!.gates }, expect.anything());
    // Changed, it is accepted already: it can be withdrawn too.
    expect(screen.getByRole('button', { name: 'Withdraw' })).toBeInTheDocument();
  });

  it('says a never accepted declaration, and withdraws nothing', () => {
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_UNACCEPTED} onAccept={vi.fn()} />);

    expect(screen.getByText(/not accepted yet/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Withdraw' })).not.toBeInTheDocument();
  });

  it("says an unread tip in the driver's words and offers nothing to press", () => {
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_UNREAD} onAccept={vi.fn()} />);

    expect(screen.getByText("engine's line could not be read here.")).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('says a moved refusal under the press, and the declaration shown stays the one to read again', async () => {
    const onAccept = vi.fn((_request, answered) => answered.refused('`engine`\'s line holds another declaration than the one you read, so nothing was accepted.'));
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_UNACCEPTED} onAccept={onAccept} />);

    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));
    expect(screen.getByRole('alert')).toHaveTextContent('another declaration than the one you read');
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<IntegrationDeclaration repository="engine" integration={INTEGRATION_UNACCEPTED} onAccept={vi.fn()} />);
    expect(screen.getByText('主线声明的检查项')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '采纳' })).toBeInTheDocument();
  });
});
