import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// The Confirm card of a department's or a room's press, where the person's growth switch asks for it: the same card restricted mode shows, with one more
// line saying the switch asks for it here, so a person who sees a card in full access knows why.

const CREATE: PressView = {
  kind: 'repository-create',
  facts: { name: 'ledger', workspace: 'aurora', folder: '/work/aurora/ledger', line: 'main', author: 'Fixture <fixture@example.test>', purpose: 'keeps the books' },
  command: 'daoris repository create ledger --purpose "keeps the books" --workspace aurora',
  also: [{ kind: 'growth-asked', facts: { place: 'aurora' } }],
};

const ROOM: PressView = {
  kind: 'place-room',
  facts: { repositories: 'engine', workspace: 'aurora', room: 'tools/web', purpose: 'the web app needs its own conventions' },
  command: 'daoris place engine --room tools/web --purpose "the web app needs its own conventions" --apply',
  also: [{ kind: 'growth-asked', facts: { place: 'engine' } }],
};

describe('the line that says the growth switch asks', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('names the workspace of a department and the repository of a room', () => {
    render(<PressViewLine view={CREATE} />);
    expect(screen.getByText('Your switch for new repositories and rooms in aurora asks you each time, so this waits for your Confirm even in full access.')).toBeInTheDocument();
    cleanup();

    render(<PressViewLine view={ROOM} />);
    expect(screen.getByText('Your switch for new repositories and rooms in engine asks you each time, so this waits for your Confirm even in full access.')).toBeInTheDocument();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={ROOM} />);

    expect(screen.getByText('你为 engine 中新仓库与新房间设置的开关要求每次都问你，所以即使在完全访问下，这里也要等你确认。')).toBeInTheDocument();
  });
});
