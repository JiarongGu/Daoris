import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { HoldFields } from './HoldFields';
import type { HoldDraft } from './appointedHold';

const STEWARD: HoldDraft = { kind: 'steward', acts: ['drive'], holder: 'terminal', window: '5', decides: [] };

const GATEKEEPER: HoldDraft = { kind: 'supervisor', acts: [], holder: 'rules', window: '5', decides: ['allow'] };

describe("the role editor's appointed fields", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it("offers the steward's hand-over as an act the person grants, worded in English and Chinese", async () => {
    const onChange = vi.fn();
    const { unmount } = render(<HoldFields hold={STEWARD} disabled={false} onChange={onChange} />);
    const tick = screen.getByRole('checkbox', { name: 'hand a session over to another agent or account' });
    expect(tick).not.toBeChecked();
    await userEvent.click(tick);
    expect(onChange).toHaveBeenCalledWith({ ...STEWARD, acts: ['drive', 'handover'] });
    unmount();

    await i18n.changeLanguage('zh');
    render(<HoldFields hold={{ ...STEWARD, acts: ['handover'] }} disabled={false} onChange={vi.fn()} />);
    expect(screen.getByRole('checkbox', { name: '把会话转交给另一个智能体或账户' })).toBeChecked();
  });

  it("shows a supervisor's kinds and only the acts of the kinds it decides, unticked, in English and Chinese", async () => {
    const onChange = vi.fn();
    const { unmount } = render(<HoldFields hold={GATEKEEPER} disabled={false} onChange={onChange} />);
    expect(screen.getByRole('checkbox', { name: 'Allows' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'allow a call once' })).not.toBeChecked();
    expect(screen.queryByRole('checkbox', { name: 'answer a question' })).toBeNull();
    expect(screen.getByText('It may always leave a decision to you.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('checkbox', { name: 'Questions' }));
    expect(onChange).toHaveBeenCalledWith({ ...GATEKEEPER, decides: ['allow', 'question'] });
    unmount();

    await i18n.changeLanguage('zh');
    render(<HoldFields hold={{ ...GATEKEEPER, decides: [], acts: ['question.answer'] }} disabled={false} onChange={vi.fn()} />);
    expect(screen.getByRole('checkbox', { name: '这里其他主管不接的每项决定' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: '回答问题' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: '采纳工作以落地' })).not.toBeChecked();
  });
});
