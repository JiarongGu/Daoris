import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ActorRow, StaffingFound } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { ActorsCard } from './Actors';

// FOUNDATION1w-s (the foundation design §19.15, "What was found, offered"): *Actors* lists each change of driver.json's actors another
// program made, as the driver kept it, saying what it changed against what Daoris last wrote, with *Take it* and *Dismiss*.

const before = (id: string, agent: string): ActorRow => ({ id, state: 'before', choices: [{ agent, from: 'before' }] });

const FOUND: StaffingFound[] = [{
  id: 7,
  at: '2026-10-30T10:00:00Z',
  changes: [
    { role: 'build', was: before('build', 'claude-code-acp'), now: before('build', 'codex-acp'), applied: false },
    { role: 'intake', was: before('intake', 'codex-acp'), now: { id: 'intake', state: 'off', choices: [] }, applied: true },
  ],
}];

const card = (onAnswerFound = vi.fn()) => {
  render(<ActorsCard rows={[before('build', 'claude-code-acp')]} agents={[]} onSave={() => {}} found={FOUND} onAnswerFound={onAnswerFound} />);
  return within(screen.getByRole('list', { name: /changed outside daoris|在 Daoris 之外的更改/i }));
};

describe("Actors' found changes", () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says each change against what Daoris last wrote, a turn-off applied at once, and presses Take it and Dismiss', () => {
    const answer = vi.fn();
    const found = card(answer);

    expect(found.getByText(/Build: claude-code-acp · account default · account default · as before → codex-acp/)).toBeTruthy();
    expect(found.getByText(/Intake: .* → Off — declarations only \(turned off at once\)/)).toBeTruthy();
    fireEvent.click(found.getByRole('button', { name: 'Take it' }));
    fireEvent.click(found.getByRole('button', { name: 'Dismiss' }));

    expect(answer).toHaveBeenNthCalledWith(1, 7, 'take', expect.anything());
    expect(answer).toHaveBeenNthCalledWith(2, 7, 'dismiss', expect.anything());
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    const found = card();

    expect(found.getByRole('button', { name: '采纳' })).toBeTruthy();
    expect(found.getByRole('button', { name: '忽略' })).toBeTruthy();
    expect(found.getByText(/已立即停用/)).toBeTruthy();
  });
});
