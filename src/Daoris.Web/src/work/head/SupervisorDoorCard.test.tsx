import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { type DoorRead, readSupervision } from './lineSupervision';
import { SupervisorDoorCard } from './SupervisorDoorCard';

// An appointed role's answer at a door that waits on the person, held for the role's window: what it answered, by which role and
// agent as configured, when it takes effect, and *Overrule* while it is held, in each shipped language.

const door = (over: Partial<DoorRead> = {}): DoorRead => ({
  id: 'd1', kind: 'memory', subject: 'staged', do: 'keep', choice: 'keep', role: 'daoris-landing', agent: 'codex-acp · gpt-5 · high',
  state: 'held', keptAt: new Date(Date.now() - 60_000).toISOString(), goesOnAt: new Date(Date.now() + 240_000).toISOString(), ...over,
});

describe('the appointed role’s door card', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', door(), "An appointed role's answer for you", /^daoris-landing \(codex-acp · gpt-5 · high\) keeps this session's staged memory changes; it goes on at .+\.$/, 'Overrule'],
    ['en', door({ choice: 'drop' }), "An appointed role's answer for you", /^daoris-landing \(codex-acp · gpt-5 · high\) drops this session's staged memory changes; it goes on at .+\.$/, 'Overrule'],
    ['en', door({ subject: 'op-3', do: 'decide', choice: 'approve' }), "An appointed role's answer for you", /^daoris-landing \(codex-acp · gpt-5 · high\) approves the memory end; it goes on at .+\.$/, 'Overrule'],
    ['en', door({ subject: 'op-3', do: 'decide', choice: 'decline' }), "An appointed role's answer for you", /^daoris-landing \(codex-acp · gpt-5 · high\) declines the memory end; it goes on at .+\.$/, 'Overrule'],
    ['en', door({ kind: 'departure', subject: 'q7', do: 'accept', choice: null }), "An appointed role's answer for you", /^daoris-landing \(codex-acp · gpt-5 · high\) accepts the departure on quest #q7; it goes on at .+\.$/, 'Overrule'],
    ['zh', door(), '任命的角色替你作的回答', /^daoris-landing（codex-acp · gpt-5 · high）保留这个会话暂存的记忆改动；将于 .+ 生效。$/, '否决'],
    ['zh', door({ choice: 'drop' }), '任命的角色替你作的回答', /^daoris-landing（codex-acp · gpt-5 · high）丢弃这个会话暂存的记忆改动；将于 .+ 生效。$/, '否决'],
    ['zh', door({ subject: 'op-3', do: 'decide', choice: 'approve' }), '任命的角色替你作的回答', /^daoris-landing（codex-acp · gpt-5 · high）批准这项记忆的结束；将于 .+ 生效。$/, '否决'],
    ['zh', door({ subject: 'op-3', do: 'decide', choice: 'decline' }), '任命的角色替你作的回答', /^daoris-landing（codex-acp · gpt-5 · high）谢绝这项记忆的结束；将于 .+ 生效。$/, '否决'],
    ['zh', door({ kind: 'departure', subject: 'q7', do: 'accept', choice: null }), '任命的角色替你作的回答', /^daoris-landing（codex-acp · gpt-5 · high）采纳委托 #q7 完成时的偏离；将于 .+ 生效。$/, '否决'],
  ])('says what the role answered, by its role and agent, and when, with Overrule while it is held (%s)', async (language, held, title, says, overrule) => {
    await i18n.changeLanguage(language);
    render(<SupervisorDoorCard door={held} onOverrule={() => {}} />);
    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(says)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: overrule })).toBeInTheDocument();
  });

  it('takes the held answer back by its id on Overrule', async () => {
    const onOverrule = vi.fn();
    render(<SupervisorDoorCard door={door()} onOverrule={onOverrule} />);
    await userEvent.click(screen.getByRole('button', { name: 'Overrule' }));
    expect(onOverrule).toHaveBeenCalledWith('d1');
  });

  it('draws nothing once the answer is no longer held, and no Overrule where nothing can act', () => {
    const { container, rerender } = render(<SupervisorDoorCard door={door({ state: 'went-on' })} onOverrule={() => {}} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<SupervisorDoorCard door={door({ state: 'overruled' })} onOverrule={() => {}} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<SupervisorDoorCard door={door()} />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('reads the door answers from the supervision answer, none from an older driver', () => {
    const held = door();
    expect(readSupervision({ doors: [held] }).doors).toEqual([held]);
    expect(readSupervision({}).doors).toEqual([]);
  });
});
