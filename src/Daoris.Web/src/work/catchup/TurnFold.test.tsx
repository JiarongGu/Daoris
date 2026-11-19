import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { span } from '../../locales/format';
import { toTurns } from '../conversation/conversation';
import type { SessionEvent } from '../conversation/conversationTypes';
import { ConversationView } from '../conversation/ConversationView';
import { CHAT, MOVED } from './catchup.fixtures';

// Each ended turn folds to one line, and nothing on the never-folded list folds with it.

const view = (events: SessionEvent[], reveal?: string, live = false) => render(
  <Tooltip.Provider>
    <ConversationView turns={toTurns(events).turns} reveal={reveal} live={live} />
  </Tooltip.Provider>);

const shown = () => document.body.textContent ?? '';

const foldOf = (name: RegExp) => screen.getByRole('button', { name });

describe('an ended turn folded to one line', () => {
  it('reads its calls by kind, how many failed, the files it changed and how long it took', () => {
    view(CHAT);

    const second = foldOf(/2 edits · 2 commands · 2 files/);
    expect(second.getAttribute('aria-expanded')).toBe('false');
    expect(second.textContent).toContain(span(9 * 60_000));
    expect(second.textContent).toContain('1 failed');
    expect(foldOf(/1 read · 1 edit · 1 file/).textContent).toContain(span(3 * 60_000 + 5_000));
    expect(screen.queryByRole('button', { name: /tool calls?/ })).toBeNull();
  });

  it('keeps the person\'s words, a failure, an act outside, a part\'s boundary and the closing words in view', () => {
    view(CHAT);

    for (const kept of ['also cover the tests', 'Both tests pass now.', 'npm test', 'git push origin fix-login', 'Pushed fix-login.']) {
      expect(shown()).toContain(kept);
    }
    expect(shown()).not.toContain('I added a test for the expiry path.');
    expect(shown()).not.toContain('Edit test/auth.test.ts');

    view(MOVED);
    for (const kept of ['Went on after the account limit.', 'npm run lint', 'Lint fails on a rule I cannot change.', 'gh pr create --fill']) {
      expect(shown()).toContain(kept);
    }
    expect(shown()).not.toContain('Search "expir"');
  });

  it('opens whole on its line, and where a jump lands inside it', () => {
    view(CHAT);
    fireEvent.click(foldOf(/2 edits · 2 commands · 2 files/));
    expect(foldOf(/2 edits · 2 commands · 2 files/).getAttribute('aria-expanded')).toBe('true');
    expect(shown()).toContain('I added a test for the expiry path.');
    document.body.innerHTML = '';

    view(CHAT, 'e11');
    expect(shown()).toContain('I added a test for the expiry path.');
  });

  it('leaves a turn still running open, its work counted by kind', () => {
    view(CHAT.slice(0, 3), undefined, true);
    expect(screen.getByRole('button', { name: '1 read · 1 edit' }).getAttribute('aria-expanded')).toBe('true');
    expect(shown()).toContain('Edit src/auth.ts');
  });
});
