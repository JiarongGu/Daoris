import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import i18n from '../locales/i18n';
import { type MilestoneEntry, type MilestoneUnit, unitOfBranch, waitsOnPerson } from './milestone';
import { MilestoneLine, WorkMilestone } from './WorkMilestone';

// The milestone on the work's foot (FABRIC-MILESTONE1b, the fabric events design §D0): a molecule over the driver's answer, each
// state reached by its props. The driver's own clauses pass through as recorded; the labels and the proofs' codes are the page's.

const entry = (over: Partial<MilestoneEntry>): MilestoneEntry => ({
  unit: 'u1', kind: 'session-branch', resource: 'daoris/s-1', role: 'temporary', disposition: 'kept', detail: 'standing', ...over,
});

const unit = (over: Partial<MilestoneUnit> = {}): MilestoneUnit => ({
  unit: 'u1', repository: 'engine', branch: 'daoris/s-1', session: 's-1',
  end: { state: 'ended', by: 'merged', on: 'main', revision: 'a1b2c3d4e5f6', at: '2026-10-17T09:00:00Z', said: 'every commit of it is on `main`' },
  result: [entry({ kind: 'line', resource: 'main', role: 'result', detail: 'the line holds it' })],
  cleanup: { state: 'completed', waits: [], gone: [entry({ disposition: 'removed', detail: 'went 2026-10-17 11:00 UTC' })] },
  recovery: [],
  entries: [],
  ...over,
});

describe('the milestone on the work’s foot', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says the end reached by its proof, the result kept, the clean-up completed with what went, and no recovery', () => {
    render(<WorkMilestone unit={unit()} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(within(region).getByText('End')).toBeInTheDocument();
    expect(region).toHaveTextContent('Reached: every commit of it is on main');
    expect(region).toHaveTextContent('Kept: main');
    expect(region).toHaveTextContent('Completed: removed session branch daoris/s-1');
    expect(region).toHaveTextContent('Nothing kept for recovery');
  });

  it('says a clean-up pending with what waits and why, a blocked wait apart from one that waits', () => {
    render(<WorkMilestone unit={unit({
      cleanup: {
        state: 'pending',
        waits: [
          entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' }),
          entry({ kind: 'end', resource: 'the end', disposition: 'pending', detail: 'the cycle waits' }),
        ],
        gone: [],
      },
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Pending');
    expect(region).toHaveTextContent('Blocked: tree s-1, a session still holds this tree');
    expect(region).toHaveTextContent('Waits: the end, the cycle waits');
  });

  it('says the recovery kept and until when, and a folder or ref kept with no date', () => {
    render(<WorkMilestone unit={unit({
      recovery: [
        entry({ kind: 'moved-aside', resource: 's-1-20261017', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' }),
        entry({ kind: 'recovery-ref', resource: 'refs/daoris/discarded/x', role: 'recovery', disposition: 'retained', detail: 'kept' }),
      ],
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent(/folder set aside s-1-20261017, kept until/);
    expect(region).toHaveTextContent('recovery ref refs/daoris/discarded/x, kept');
  });

  it('says an end not reached in the driver’s sentence, and an unknown owner as one', () => {
    const { rerender } = render(<WorkMilestone unit={unit({
      end: { state: 'not-ended', said: 'nothing was handed on and the line does not hold it' }, result: [],
      cleanup: { state: 'pending', waits: [], gone: [] },
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Not reached: nothing was handed on and the line does not hold it');
    expect(region).toHaveTextContent('None kept yet');
    rerender(<WorkMilestone unit={unit({ end: { state: 'unknown', said: 'no record names its unit, so its end is not said' } })} />);
    expect(region).toHaveTextContent('Unknown: no record names its unit');
  });

  it('is said in Chinese by the same facts', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkMilestone unit={unit({
      cleanup: { state: 'pending', waits: [entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' })], gone: [] },
      recovery: [entry({ kind: 'moved-aside', resource: 's-1-x', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' })],
    })} />);
    const region = screen.getByRole('region', { name: i18n.t('work.milestone.title') });
    expect(i18n.t('work.milestone.title')).not.toBe('Where this work stands');
    expect(region).toHaveTextContent(i18n.t('work.milestone.end.ended', { proof: i18n.t('work.milestone.by.merged', { on: 'main' }) }));
    expect(region).toHaveTextContent('成果');
    expect(region).toHaveTextContent('结束');
  });

  it('is one line for a branch row: the end, the clean-up and what is kept', () => {
    render(<MilestoneLine unit={unit({ recovery: [entry({ kind: 'moved-aside', until: '2026-10-24T09:00:00Z', disposition: 'retained', role: 'recovery' })] })} />);
    const line = screen.getByText(/Ended/);
    expect(line).toHaveTextContent('Ended');
    expect(line).toHaveTextContent('clean-up completed');
    expect(line).toHaveTextContent('recovery kept until');
  });
});

describe('what of a unit waits on the person', () => {
  it('is the blocked entries of its clean-up and nothing a cycle waits on by itself', () => {
    const blocked = entry({ kind: 'tree', disposition: 'blocked' });
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [blocked, entry({ disposition: 'pending' })], gone: [] } }))).toEqual([blocked]);
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [entry({ disposition: 'pending' })], gone: [] } }))).toEqual([]);
    expect(waitsOnPerson(unit())).toEqual([]);
  });

  it('finds the unit a branch belongs to by its repository and either of its branches', () => {
    const held = unit({ entries: [entry({ kind: 'landing-branch', resource: 'feature/x' })] });
    expect(unitOfBranch([held], 'Engine', 'daoris/s-1')).toBe(held);
    expect(unitOfBranch([held], 'engine', 'feature/x')).toBe(held);
    expect(unitOfBranch([held], 'other', 'feature/x')).toBeUndefined();
  });
});
