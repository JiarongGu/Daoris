import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import type { WorkflowRun } from './run';
import { answerOf, GRAPH_RUN_GO_AHEAD } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

const asked = vi.hoisted(() => vi.fn());
vi.mock('../plugins/commandAsks', () => ({ askCommand: asked }));

// A refused step whose plugin named a declared command offers it as the step's door, which opens that command's round card.

const refused = (needs: NonNullable<WorkflowRun['steps'][number]['needs']> | null): WorkflowRun => ({
  ...GRAPH_RUN_GO_AHEAD,
  steps: GRAPH_RUN_GO_AHEAD.steps.map((step) => step.step.id === 'release'
    ? { ...step, state: 'failed', detail: 'failed', acts: null, needs }
    : step),
});

describe('a refused step that names its fix', () => {
  afterEach(async () => {
    asked.mockClear();
    await i18n.changeLanguage('en');
  });

  it("draws the command's title as a door that asks the round card for it", async () => {
    const needs = { plugin: 'acme.deploy', command: 'login', title: { en: 'Sign in', zh: '登录' } };
    const { unmount } = rtlRender(<Tooltip.Provider><WorkflowRunView answer={answerOf(refused(needs))} /></Tooltip.Provider>);
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(asked).toHaveBeenCalledWith('acme.deploy', 'login');
    unmount();

    await i18n.changeLanguage('zh');
    rtlRender(<Tooltip.Provider><WorkflowRunView answer={answerOf(refused(needs))} /></Tooltip.Provider>);
    expect(screen.getByRole('button', { name: '登录' })).toBeInTheDocument();
  });

  it('draws no such door where it named none', () => {
    rtlRender(<Tooltip.Provider><WorkflowRunView answer={answerOf(refused(null))} /></Tooltip.Provider>);
    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull();
  });
});
