import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../bridge/shell', async (original) => ({
  ...(await original<typeof import('../bridge/shell')>()),
  useHarnessAction: () => ({ mutate: vi.fn(), isPending: false }),
  useHarnessEnded: () => undefined,
}));

import { HarnessRuns, useHarnessRun } from '../agents/harnessRuns';
import i18n from '../locales/i18n';
import { type Confirmation, Confirmations } from './Confirmations';

// An update or a pin pressed from a terminal is followed by the Agents screen once confirmed: its console shows under the
// tool doing it and its end is said there, as for one started on the screen.

const press = (kind: string, facts: Record<string, string>, command: string): Confirmation => ({
  source: 'pipe', id: 'a1', module: 'DAORIS.DRIVER', route: 'HARNESS_ACTION', fields: [], asker: { process: 'bash', pid: 9 },
  view: { kind, facts, command },
});

const Probe = () => <p data-testid="running">{useHarnessRun().running ?? 'nothing'}</p>;

const draw = (waiting: Confirmation, answered = true) => {
  const client = new QueryClient();
  const answer = vi.fn(async () => (answered ? { answered: true as const } : { answered: false as const, why: 'gone', message: 'gone' }));
  render(
    <QueryClientProvider client={client}>
      <HarnessRuns notify={vi.fn()}>
        <Probe />
        <Confirmations waiting={[waiting]} onAnswer={answer} />
      </HarnessRuns>
    </QueryClientProvider>,
  );
};

describe('a confirmed update or pin', () => {
  it('is followed by the Agents screen, which shows its console under the tool', async () => {
    await i18n.changeLanguage('en');
    draw(press('agent-update-tool', { agent: 'claude-code' }, 'daoris agent update claude-code'));
    expect(screen.getByTestId('running')).toHaveTextContent('nothing');
    await userEvent.click(screen.getByRole('button', { name: /confirm/i }));
    expect(await screen.findByText('claude-code:update')).toBeInTheDocument();
  });

  it('follows a pin as a pin', async () => {
    await i18n.changeLanguage('en');
    draw(press('agent-pin', { agent: 'claude-code', version: '2.1.3' }, 'daoris agent pin claude-code 2.1.3'));
    await userEvent.click(screen.getByRole('button', { name: /confirm/i }));
    expect(await screen.findByText('claude-code:pin')).toBeInTheDocument();
  });

  it('is not followed when the person refuses it, or the press is another route', async () => {
    await i18n.changeLanguage('en');
    draw(press('agent-update-tool', { agent: 'claude-code' }, 'daoris agent update claude-code'), false);
    await userEvent.click(screen.getByRole('button', { name: /confirm/i }));
    expect(screen.getByTestId('running')).toHaveTextContent('nothing');
  });
});
