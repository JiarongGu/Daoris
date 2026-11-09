import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import type { AllowRead } from './lineSupervision';
import { SupervisorAllowCard } from './SupervisorAllowCard';

// The supervisor's answer to a tool call the session held for the person, on the session's card: that it allowed (or refused) it once and
// when, with no Overrule, since it takes effect where it is kept; in each shipped language.

const allow = (over: Partial<AllowRead> = {}): AllowRead => ({
  id: 'd1', kind: 'allow', shape: 'request', subject: 'r1', repository: 'engine', do: 'once', role: 'supervisor-allows', state: 'went-on', why: null,
  holder: { kind: 'terminal' }, keptAt: new Date(Date.now() - 120_000).toISOString(), goesOnAt: new Date(Date.now() - 120_000).toISOString(),
  stateAt: new Date(Date.now() - 120_000).toISOString(), ...over,
});

describe('the supervisor’s allow card', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'once', "The supervisor's answer to a held tool call", 'The supervisor allowed this once, 2m ago: the call went on.'],
    ['en', 'reject', "The supervisor's answer to a held tool call", 'The supervisor refused this once, 2m ago: the agent was told, and went on without it.'],
    ['zh', 'once', '主管对被搁置的工具调用的答复', '主管已允许这一次（2 分钟前）：该调用继续。'],
    ['zh', 'reject', '主管对被搁置的工具调用的答复', '主管已拒绝这一次（2 分钟前）：智能体会得知，并在没有它的情况下继续。'],
    ['en', 'leave', "The supervisor's answer to a held tool call", 'The supervisor left this to you, 2m ago: the call still waits for you.'],
    ['zh', 'leave', '主管对被搁置的工具调用的答复', '主管已交给你处理（2 分钟前）：该调用仍在等你决定。'],
  ])('says what the supervisor did with the held call and when, with no Overrule (%s, %s)', async (language, act, title, says) => {
    await i18n.changeLanguage(language);
    render(<SupervisorAllowCard allow={allow({ do: act })} />);
    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(says)).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('draws nothing for an answer to something else, an old one, or none', () => {
    const { container, rerender } = render(<SupervisorAllowCard allow={allow({ shape: 'widening', do: 'standing' })} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<SupervisorAllowCard allow={allow({ keptAt: new Date(Date.now() - 3_600_000).toISOString() })} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<SupervisorAllowCard allow={null} />);
    expect(container).toBeEmptyDOMElement();
  });
});
