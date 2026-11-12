import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { Note } from './Note';

// An intake that says no department owns an ask parks with one coded line, worded from the catalogue in the reader's language, and
// the intake's own sentence beneath it as its words: the line names Ask Daoris as the one that can open a department or a room.

const UNOWNED: NotePart[] = [
  { code: 'intake.unowned', values: { ask: '0fda18' }, text: 'no department owns ask `#0fda18`: …' },
  { words: 'a billing system, and no repository here declares billing', by: 'agent' },
];

describe('the note of an ask no department owns', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says in English that Ask Daoris can open a department or a room for it, then what the intake said', () => {
    const { container } = render(<Note parts={UNOWNED} />);

    // Its commands are code spans, so the line is read as the page shows it, whole.
    expect(container.textContent).toContain(
      "No department owns ask #0fda18: nothing in this workspace's declarations covers the work. It is held for Ask Daoris. "
      + 'Ask it to open a department or a room for it (daoris repository create, or daoris place <repository> --room <path>), '
      + 'or route it yourself with daoris ask publish 0fda18 --to <repository>, or end it with daoris ask close 0fda18 --reason "…". '
      + 'The intake said:',
    );
    const said = screen.getByText('a billing system, and no repository here declares billing');
    expect(said.closest('blockquote')).not.toBeNull();
  });

  it('says it in Chinese, the intake’s own words as it wrote them', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(<Note parts={UNOWNED} />);

    expect(container.textContent).toContain('没有部门负责需求 #0fda18：这个工作区的声明里没有哪一项涵盖这项工作。它留给 Ask Daoris：请它为此开设一个部门或房间');
    expect(container.textContent).not.toContain('No department owns');
    expect(screen.getByText('a billing system, and no repository here declares billing')).toBeInTheDocument();
  });
});
