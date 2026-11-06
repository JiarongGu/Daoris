import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { DoorSheetLines } from './DoorSheetLines';
import { TOOLS } from '../agentsFixtures';

// The sheet on the agent's page (COAGENT1e): each capability a door's sheet answered, in both languages.

const sheet = TOOLS.find((tool) => tool.name === 'claude-code')!.sheet!.find((one) => one.door === 'claude-code-acp')!;

describe('a door’s sheet on the agent’s page', () => {
  afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

  it('says each capability with its value and how it is known, and an absent one as not known with its fallback', async () => {
    await i18n.changeLanguage('en');
    render(<DoorSheetLines t={i18n.t} sheet={sheet} />);
    const lines = within(screen.getByRole('list', { name: 'What claude-code-acp can do' }));
    expect(lines.getByText(/Holds a conversation/).parentElement!.textContent).toBe('Holds a conversation: yes (declared)');
    expect(lines.getByText(/Resumes a kept conversation/).parentElement!.textContent).toBe('Resumes a kept conversation: yes (told when it opened)');
    expect(lines.getByText(/Can be steered now/).parentElement!.textContent).toBe('Can be steered now: not known. Now waits for the turn to end.');
    // The door's own row is the wire word the header says, so it is not repeated.
    expect(screen.queryByText(/^door/i)).toBeNull();
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<DoorSheetLines t={i18n.t} sheet={sheet} />);
    expect(screen.getByText('可持续对话').parentElement!.textContent).toBe('可持续对话：是（声明）');
    expect(screen.getByText('可即时引导').parentElement!.textContent).toBe('可即时引导：未知。Now waits for the turn to end.');
  });

  it('draws nothing for a door whose roster carried no rows', () => {
    const { container } = render(<DoorSheetLines t={i18n.t} sheet={{ ...sheet, rows: [] }} />);
    expect(container.textContent).toBe('');
  });
});
