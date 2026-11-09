import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { NODE_KINDS } from '../../data/generated/service';
import i18n from '../../locales/i18n';
import type { RunStep } from '../run';
import { StepWait, waitSaid } from './StepWait';

// A step's wait is said under it in the reader's language, from its kind's declared waits (`workflow.wait.<kind>.<code>`) and the facts the
// record names; one this page has no words for is said as recorded, and a step that waits on nothing says nothing.

const t = i18n.t.bind(i18n);

const step = (kind: string, wait: RunStep['wait']): RunStep =>
  ({ step: { id: kind, kind }, state: 'working', detail: 'active', node: kind, wait } as unknown as RunStep);

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('a step\'s wait', () => {
  it('says each kind\'s wait in its words, the record\'s facts in place, in both languages', async () => {
    const resolving = { code: 'session', values: { files: '`src/a.cs` (2 hunks)', count: '1', of: '3' }, until: '2026-11-09T15:02:00Z' };
    expect(waitSaid(t, 'resolve', resolving)).toContain('`src/a.cs` (2 hunks)');
    expect(waitSaid(t, 'resolve', resolving)).toContain('1 of 3');
    expect(waitSaid(t, 'merge', { code: 'integration', values: { line: 'main', candidate: 'c-7' } })).toBe('Waits in `main`\'s integration, at candidate `c-7`.');
    expect(waitSaid(t, 'rebase', { code: 'member', values: { session: 's-41' } })).toBe('Waits for `s-41`\'s Accept, which brings this work in with it.');
    await i18n.changeLanguage('zh');
    expect(waitSaid(t, 'merge', { code: 'integration', values: { line: 'main', candidate: 'c-7' } })).toContain('`c-7`');
    expect(waitSaid(t, 'rebase', { code: 'turn' })).not.toMatch(/[A-Za-z]{4}/);
  });

  it('says a wait this page has no words for as recorded', () => {
    expect(waitSaid(t, 'merge', { code: 'newer-wait' })).toBe('Waits: newer-wait');
    expect(waitSaid(t, 'acme/deploy', { code: 'approval' })).toBe('Waits: approval');
  });

  it('words every wait the kit declares, in both languages', () => {
    const declared = NODE_KINDS.flatMap((row) => row.waits.map((code) => `workflow.wait.${row.kind}.${code}`));
    expect(declared.length).toBeGreaterThan(10);
    for (const language of ['en', 'zh']) {
      expect(declared.filter((key) => !i18n.exists(key, { lng: language, fallbackLng: false }))).toEqual([]);
    }
  });

  it('draws the wait under the step, and nothing for a step that waits on none', () => {
    const { rerender } = render(<StepWait step={step('merge', { code: 'root-dirty', since: '2026-11-09T14:02:00Z' })} />);
    expect(screen.getByText(/Waits for the repository's checkout to be clean/)).toBeTruthy();
    rerender(<StepWait step={step('merge', null)} />);
    expect(screen.queryByText(/Waits/)).toBeNull();
  });
});
