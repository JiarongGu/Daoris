import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { PermissionWait } from '../../work/waits/PermissionAsks';
import { AskHeld } from './AskHeld';

// Ask Daoris's own conversation in restricted mode holds a tool call for the person: the panel shows it under the conversation with
// its allow once and its refusal, answered through the one answer door; another session's held call is not its to show.

const mutate = vi.fn();
const waits: PermissionWait[] = [
  {
    session: 'h1', request: 'r1', call: null, title: 'Bash: npm test', kind: 'execute',
    choices: [{ id: 'allow', name: 'Allow once', kind: 'allow_once' }, { id: 'reject', name: 'Refuse', kind: 'reject_once' }],
    since: new Date().toISOString(), until: null,
  },
  {
    session: 's-quest', request: 'r2', call: null, title: 'Edit: a.ts', kind: 'edit',
    choices: [{ id: 'allow', name: 'Allow once', kind: 'allow_once' }], since: new Date().toISOString(), until: null,
  },
];

vi.mock('../../bridge/shell', () => ({
  usePermissionWaits: () => ({ data: waits }),
  useAnswerPermission: () => ({ mutate, isPending: false }),
}));

describe.each(['en', 'zh'])('Ask Daoris’s held calls in %s', (language) => {
  afterEach(async () => {
    cleanup();
    mutate.mockReset();
    await i18n.changeLanguage('en');
  });

  it('shows its own conversation’s held call and answers it, and none of another session’s', async () => {
    await i18n.changeLanguage(language);
    render(<AskHeld session="h1" live />);

    const section = screen.getByRole('region', { name: i18n.t('work.wait.permission.title') });
    expect(section.textContent).toContain('npm test');
    expect(section.textContent).not.toContain('a.ts');

    await userEvent.click(within(section).getByRole('button', { name: i18n.t('work.attention.act.allowOnce') }));
    expect(mutate).toHaveBeenCalledWith({ session: 'h1', request: 'r1', option: 'allow' }, expect.anything());
  });

  it('draws nothing where its conversation holds nothing', () => {
    const { container } = render(<AskHeld session="h9" live />);
    expect(container).toBeEmptyDOMElement();
  });
});
