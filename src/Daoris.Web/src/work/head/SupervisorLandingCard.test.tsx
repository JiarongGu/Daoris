import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import type { LandingRead } from './lineSupervision';
import { SupervisorLandingCard } from './SupervisorLandingCard';

// The supervisor's accept, approve or reject of a step waiting for the person's press, on the session's card: what it did and when it takes
// effect, and *Overrule* while the window lasts, in each shipped language.

const landing = (state: string, over: Partial<LandingRead> = {}): LandingRead => ({
  id: 'd1', kind: 'landing', run: 'q7', node: 'review', attempt: 1, do: 'accept', state, why: null, holder: { kind: 'terminal' },
  keptAt: new Date(Date.now() - 120_000).toISOString(), goesOnAt: new Date(Date.now() + 180_000).toISOString(),
  stateAt: new Date(Date.now() - 120_000).toISOString(), earlier: false, ...over,
});

describe('the supervisor’s landing card', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', 'accept', "The supervisor's decision on this step", /^The supervisor accepted this; it goes on at .+\.$/, 'Overrule'],
    ['en', 'approve', "The supervisor's decision on this step", /^The supervisor approved this; it goes on at .+\.$/, 'Overrule'],
    ['en', 'reject', "The supervisor's decision on this step", /^The supervisor rejected this; it goes back at .+\.$/, 'Overrule'],
    ['zh', 'accept', '主管对这一步的决定', /^主管已采纳这项工作；将于 .+ 继续。$/, '否决'],
    ['zh', 'approve', '主管对这一步的决定', /^主管已批准这一步；将于 .+ 继续。$/, '否决'],
    ['zh', 'reject', '主管对这一步的决定', /^主管已驳回这一步；将于 .+ 退回。$/, '否决'],
  ])('says what the supervisor did and when it takes effect, with Overrule while it is held (%s, %s)', async (language, act, title, says, overrule) => {
    await i18n.changeLanguage(language);
    render(<SupervisorLandingCard landing={landing('held', { do: act })} onOverrule={() => {}} />);
    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(says)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: overrule })).toBeInTheDocument();
  });

  it.each([
    ['en', 'branch', /^The supervisor accepted the commits on this branch; it goes on at .+\.$/],
    ['en', 'checkout', /^The supervisor accepted the commits made in this checkout, which are there already; it goes on at .+\.$/],
    ['en', 'folder', /^The supervisor accepted the files changed in this folder, which are there already; it goes on at .+\.$/],
    ['en', 'copy', /^The supervisor accepted the files changed in its copy of this folder; it goes on at .+\.$/],
    ['en', 'plan', /^The supervisor accepted the plan for this system; it goes on at .+\.$/],
    ['en', 'answer', /^The supervisor accepted the answer, which changed nothing; it goes on at .+\.$/],
    ['zh', 'branch', /^主管已采纳这个分支上的提交；将于 .+ 继续。$/],
    ['zh', 'checkout', /^主管已采纳在这个检出里做的提交，它们已经在那里了；将于 .+ 继续。$/],
    ['zh', 'folder', /^主管已采纳这个文件夹里改动的文件，它们已经在文件夹里了；将于 .+ 继续。$/],
    ['zh', 'copy', /^主管已采纳它在这个文件夹的副本里改动的文件；将于 .+ 继续。$/],
    ['zh', 'plan', /^主管已采纳对这个系统的计划；将于 .+ 继续。$/],
    ['zh', 'answer', /^主管已采纳它的回答，它什么都没有改动；将于 .+ 继续。$/],
  ])('says an Accept by the kind of work its review read (%s, %s)', async (language, work, says) => {
    await i18n.changeLanguage(language);
    render(<SupervisorLandingCard landing={landing('held', { work })} onOverrule={() => {}} />);
    expect(screen.getByText(says)).toBeInTheDocument();
  });

  it('overrules that decision when the person presses it, once, and not while it is on its way', async () => {
    const onOverrule = vi.fn();
    const { rerender } = render(<SupervisorLandingCard landing={landing('held')} onOverrule={onOverrule} />);
    await userEvent.click(screen.getByRole('button', { name: 'Overrule' }));
    expect(onOverrule).toHaveBeenCalledExactlyOnceWith('d1');
    rerender(<SupervisorLandingCard landing={landing('held')} onOverrule={onOverrule} busy />);
    expect(screen.getByRole('button', { name: 'Overrule' })).toBeDisabled();
  });

  it('offers no Overrule where nothing can press it, but still says the decision', () => {
    render(<SupervisorLandingCard landing={landing('held')} />);
    expect(screen.getByText(/^The supervisor accepted this; it goes on at .+\.$/)).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it.each([['released', {}], ['went-on', {}], ['overruled', {}], ['superseded', {}], ['stale', { why: 'owner-held' }], ['left', { do: 'hold' }]])(
    'draws nothing for a decision that is %s: the card stands only while Overrule can take it back', (state, over) => {
      const { container, rerender } = render(<SupervisorLandingCard landing={landing(state, over)} onOverrule={() => {}} />);
      expect(container).toBeEmptyDOMElement();
      rerender(<SupervisorLandingCard landing={null} />);
      expect(container).toBeEmptyDOMElement();
    });
});
