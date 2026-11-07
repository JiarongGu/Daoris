import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import type { CourseRead } from './lineSupervision';
import { SupervisorCourseCard } from './SupervisorCourseCard';

// The supervisor's decision on a point a person's rule referred, on the session's card: which rule referred, what it chose and when, and
// *Overrule* while the window lasts, in each shipped language.

const course = (state: string, over: Partial<CourseRead> = {}): CourseRead => ({
  id: 'd1', point: 'rule-1', count: 'answers-alone', n: 2, rule: 'aa', place: 'repository:engine', do: 'go-on', state, why: null,
  holder: { kind: 'terminal' }, keptAt: new Date(Date.now() - 120_000).toISOString(), goesOnAt: new Date(Date.now() + 180_000).toISOString(),
  stateAt: new Date(Date.now() - 120_000).toISOString(), earlier: false, ...over,
});

describe('the supervisor’s decision card', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each([
    ['en', "The supervisor's decision", /^Your rule for answers alone in a row \(engine\) asked the supervisor at 2; it chose to go on 2m ago\. It goes on at .+ unless you overrule it\.$/, 'Overrule'],
    ['zh', '主管的决定', /^你为连续仅靠回答继续（engine）设的规则在 2 时请主管判断；它决定继续，2 分钟前。到 .+ 时继续，除非你否决它。$/, '否决'],
  ])('says which rule referred, what it chose and when, with Overrule while it is held (%s)', async (language, title, says, overrule) => {
    await i18n.changeLanguage(language);
    render(<SupervisorCourseCard course={course('held')} onOverrule={() => {}} />);
    expect(screen.getByRole('heading', { name: title })).toBeInTheDocument();
    expect(screen.getByText(says)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: overrule })).toBeInTheDocument();
  });

  it('overrules that decision when the person presses it, once, and not while it is on its way', async () => {
    const onOverrule = vi.fn();
    const { rerender } = render(<SupervisorCourseCard course={course('held')} onOverrule={onOverrule} />);
    await userEvent.click(screen.getByRole('button', { name: 'Overrule' }));
    expect(onOverrule).toHaveBeenCalledExactlyOnceWith('d1');
    rerender(<SupervisorCourseCard course={course('held')} onOverrule={onOverrule} busy />);
    expect(screen.getByRole('button', { name: 'Overrule' })).toBeDisabled();
  });

  it('offers no Overrule where nothing can press it, but still says the decision', () => {
    render(<SupervisorCourseCard course={course('held')} />);
    expect(screen.getByText(/it chose to go on 2m ago\. It goes on at .+ unless you overrule it\.$/)).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says why an overrule did not take, in the bridge’s words', () => {
    render(<SupervisorCourseCard course={course('held')} onOverrule={() => {}} said="It already went on." />);
    expect(screen.getByRole('status')).toHaveTextContent('It already went on.');
  });

  it.each([['went-on', {}], ['overruled', {}], ['stale', { why: 'rule-holds' }], ['left', { do: 'hold' }]])(
    'draws nothing for a decision that is %s: the card stands only while Overrule can take it back, and Details keeps the rest', (state, over) => {
      const { container, rerender } = render(<SupervisorCourseCard course={course(state, over)} onOverrule={() => {}} />);
      expect(container).toBeEmptyDOMElement();
      rerender(<SupervisorCourseCard course={null} />);
      expect(container).toBeEmptyDOMElement();
    });
});
