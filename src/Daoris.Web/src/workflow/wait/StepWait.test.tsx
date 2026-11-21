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

  it('says why a check waits for one of the machine\'s check slots: the slots all run, memory, or its repository\'s heavy run, with its place in the queue, never as ready', async () => {
    const full = { code: 'slot', values: { running: '2', slots: '2', position: '2', of: '3' }, since: '2026-11-20T08:20:00Z' };
    const memory = { code: 'slot-memory', values: { free: '4.1', need: '6', position: '1', of: '1' } };
    const repository = { code: 'slot-repository', values: { repository: 'Daoris', position: '1', of: '2' } };
    expect(waitSaid(t, 'check', full)).toBe('Waits for a check slot: 2 of 2 run; 2 of 3 in its queue.');
    expect(waitSaid(t, 'check', memory)).toBe('Waits for memory: 4.1 GB free, the next check needs 6 GB; 1 of 1 in its queue.');
    expect(waitSaid(t, 'check', repository)).toBe('Waits for `Daoris`\'s heavy-run slots, each held by another of its heavy runs; 1 of 2 in its queue.');
    expect(waitSaid(t, 'check', { code: 'slot-admitting', values: { position: '1', of: '2' } })).toBe("Waits for the admission's lock, which another start holds; 1 of 2 in its queue.");
    expect(waitSaid(t, 'check', { code: 'slot-queued', values: { position: '1', of: '2' } })).toBe('Waits for a check slot: 1 of 2 in its queue.');
    await i18n.changeLanguage('zh');
    expect(waitSaid(t, 'check', memory, 'zh')).toContain('4.1 GB');
    expect(waitSaid(t, 'check', repository, 'zh')).toContain('`Daoris`');
    for (const wait of [full, memory]) expect(waitSaid(t, 'check', wait, 'zh')).not.toMatch(/[A-Za-z]{4}/);
  });

  it('says a wait this page has no words for as recorded', () => {
    expect(waitSaid(t, 'merge', { code: 'newer-wait' })).toBe('Waits: newer-wait');
    expect(waitSaid(t, 'acme/deploy', { code: 'approval' })).toBe('Waits: approval');
  });

  it('says a second opinion hold the risk policy passed as passed, with its line, on nobody', async () => {
    const passed = { code: 'passed', values: { count: '2', held: 'disputed', why: 'no risk declared: it passes (rule seed at 0e80e115d1ce)' } };
    expect(waitSaid(t, 'opinion', passed)).toBe(
      "The risk policy passed what the second opinion held (2): no risk declared: it passes (rule seed at 0e80e115d1ce). Daoris's next look reaches the step.");
    await i18n.changeLanguage('zh');
    expect(waitSaid(t, 'opinion', passed)).toContain('no risk declared: it passes (rule seed at 0e80e115d1ce)');
    await i18n.changeLanguage('en');
  });

  it('words every wait the kit declares, in both languages', () => {
    const declared = NODE_KINDS.flatMap((row) => row.waits.map((code) => `workflow.wait.${row.kind}.${code}`));
    expect(declared.length).toBeGreaterThan(10);
    for (const language of ['en', 'zh']) {
      expect(declared.filter((key) => !i18n.exists(key, { lng: language, fallbackLng: false }))).toEqual([]);
    }
  });

  it('says a plugin step waiting on an actor by the actor and its holder, alike for you and an agent, with the page it is taken on', async () => {
    const page = 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90';
    const words = 'Release-12 of app-release: Approve Production in Azure DevOps: asked of Release Approvers.';
    const approver = { code: 'actor', values: { actor: 'approver', holder: 'person', words, url: page, environment: 'Production' }, since: '2026-11-16T09:51:00Z' };
    expect(waitSaid(t, 'daoris-azure-devops/release', approver)).toBe(`Waiting on \`approver\`: you. ${words}`);
    expect(waitSaid(t, 'daoris-azure-devops/release', { ...approver, values: { ...approver.values, holder: 'agent', holderName: 'release-steward' } }))
      .toBe(`Waiting on \`approver\`: \`release-steward\`. ${words}`);
    render(<StepWait step={{ ...step('daoris-azure-devops/release', approver), state: 'waiting' } as RunStep} />);
    expect(screen.getByRole('link', { name: 'Open where it is taken' }).getAttribute('href')).toBe(page);
    await i18n.changeLanguage('zh');
    expect(waitSaid(t, 'daoris-azure-devops/release', approver)).toBe(`等待 \`approver\`：你。${words}`);
  });

  it('says a second opinion\'s dispute by whoever holds it: a role\'s agent never reads as you, and your own holding as before', async () => {
    const disputed = { code: 'disputed', values: { count: '2', session: 's1', role: 'opinion', why: 'classed role at the machine' } };
    const since = { code: 'commits-since', values: { count: '3', session: 's1', role: 'opinion' } };
    const agent = { kind: 'agent', name: 'reviewer' };
    expect(waitSaid(t, 'opinion', disputed, 'en', agent)).toBe(
      "Waiting on `opinion`: `reviewer`, for its answer to the second opinion's disputed findings (2). The risk policy asks this role, not you.");
    expect(waitSaid(t, 'opinion', since, 'en', agent)).toContain('Waiting on `opinion`: `reviewer`, for its answer to the commits no other agent read (3)');
    expect(waitSaid(t, 'opinion', disputed, 'en', agent)).not.toMatch(/your answer|Your Accept/);
    expect(waitSaid(t, 'opinion', disputed, 'en', { kind: 'person' })).toContain('Waits on your answer');
    render(<StepWait step={{ ...step('opinion', disputed), state: 'waiting', holder: agent } as RunStep} />);
    expect(screen.getByText(/Waiting on/)).toBeTruthy();
    expect(screen.queryByText(/your answer/)).toBeNull();
    await i18n.changeLanguage('zh');
    expect(waitSaid(t, 'opinion', disputed, 'zh', agent)).toContain('`reviewer`');
    expect(waitSaid(t, 'opinion', disputed, 'zh', agent)).not.toContain('等待你');
  });

  it('draws the wait under the step, and nothing for a step that waits on none', () => {
    const { rerender } = render(<StepWait step={step('merge', { code: 'root-dirty', since: '2026-11-09T14:02:00Z' })} />);
    expect(screen.getByText(/Waits for the repository's checkout to be clean/)).toBeTruthy();
    rerender(<StepWait step={step('merge', null)} />);
    expect(screen.queryByText(/Waits/)).toBeNull();
  });
});
