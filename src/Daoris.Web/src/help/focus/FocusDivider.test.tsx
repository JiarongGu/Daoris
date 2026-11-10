import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { FocusDivider, focusDividers } from './FocusDivider';

// The transcript's line where a message carried a change of focus (`docs/2026-11-11-ask-scope-ux-design.md` §5), drawn from the
// message's record, in both catalogues.

const games = { kind: 'workspace', workspace: 'Games' } as const;
const engine = { kind: 'repository', workspace: 'Games', repository: 'engine' } as const;
const at = '2026-11-11T14:02:00';

describe.each(['en', 'zh'])('the focus divider in %s', (language) => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says what the focus was and became, with the time', async () => {
    await i18n.changeLanguage(language);
    render(<FocusDivider mark={{ word: 'w2', at, from: games, to: engine, widened: false }} />);
    const from = i18n.t('help.focus.divider.workspace', { workspace: 'Games' });
    const to = i18n.t('help.focus.divider.repository', { repository: 'engine' });
    const line = screen.getByRole('separator');
    expect(line).toHaveAttribute('aria-label', expect.stringContaining(i18n.t('help.focus.divider.changed', { from, to })));
    expect(line).toHaveTextContent(i18n.t('help.focus.divider.changed', { from, to }));
    expect(line).toHaveTextContent('14:02');
  });

  it('reads widened where the driver widened a place no longer here, and names this machine', async () => {
    await i18n.changeLanguage(language);
    render(<FocusDivider mark={{ word: 'w3', at, from: engine, to: { kind: 'machine' }, widened: true }} />);
    const from = i18n.t('help.focus.divider.repository', { repository: 'engine' });
    const to = i18n.t('help.focus.divider.machine');
    expect(screen.getByRole('separator')).toHaveTextContent(i18n.t('help.focus.divider.widened', { from, to }));
  });
});

describe('the dividers of a conversation', () => {
  it('are keyed by the message that carried each change, a mark with no message left out', () => {
    const marks = [
      { word: 'w2', at, from: games, to: engine, widened: false },
      { word: null, at, from: engine, to: games, widened: false },
    ];
    expect(Object.keys(focusDividers(marks))).toEqual(['w2']);
    expect(focusDividers(undefined)).toEqual({});
  });
});
