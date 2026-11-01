import { createElement, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// WORKFLOWEDIT1a: the step editor's three reads call their routes and type what the driver answers.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { useWorkflowEditor, useWorkflowList, useWorkflowPalette, useWorkflowShown } from './workflowEdit';

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(QueryClientProvider, { client: new QueryClient({ defaultOptions: { queries: { retry: false } } }) }, children);

describe('the step editor reads', () => {
  afterEach(() => invoke.mockReset());

  it('lists the saved workflows with the places that choose them, and the presets', async () => {
    invoke.mockResolvedValueOnce({
      workflows: [{ id: 'web-verify', name: 'Verify', problem: null, versions: [1], schema: 2, digest: 'ab', line: 'x', pipeline: 'verification',
        chosen: [{ level: 'repository', name: 'engine', role: 'default', kind: null }] }],
      presets: [{ id: 'integrated', name: 'Integrated', graph: true, line: 'y', preset: true }],
    });
    const { result } = renderHook(() => useWorkflowList(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_LIST', expect.objectContaining({ payload: expect.objectContaining({ held: expect.any(String) }) }));
    expect(result.current.data!.workflows[0]!.chosen[0]!.name).toBe('engine');
    expect(result.current.data!.presets[0]!.preset).toBe(true);
  });

  it('asks for one version by its id, and for none without one', async () => {
    invoke.mockResolvedValueOnce({ id: 'web-verify', name: 'Verify', version: 2, newest: true, versions: [1, 2], at: null, door: null, digest: 'ab', schema: 2,
      line: 'x', body: { nodes: [] }, problems: [{ code: 'c', wire: null, node: 'n', words: 'w' }] });
    const { result } = renderHook(() => useWorkflowShown('web-verify', 2), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_SHOW', expect.objectContaining({ payload: expect.objectContaining({ id: 'web-verify', version: 2 }) }));
    expect(result.current.data!.problems[0]!.node).toBe('n');

    invoke.mockClear();
    renderHook(() => useWorkflowShown(null), { wrapper });
    expect(invoke).not.toHaveBeenCalled();
  });

  it('reads the plan of a choice with `plan: true`, writing nothing and asking no read again', async () => {
    invoke.mockResolvedValue({ done: false, plan: true, refused: null, message: null, changed: true, reach: [], expected: 'use-1' });
    const { result } = renderHook(() => useWorkflowEditor({ repository: 'engine' }), { wrapper });

    const answer = await result.current.onPlan!({ repository: 'engine', workflow: 'verify-docs', version: 2 });

    expect(answer.expected).toBe('use-1');
    expect(invoke).toHaveBeenCalledTimes(1);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_USE', expect.objectContaining({
      payload: expect.objectContaining({ repository: 'engine', workflow: 'verify-docs', version: 2, plan: true }),
    }));
  });

  it('reads the palette: kinds with their options, wire kinds, and plugin steps', async () => {
    invoke.mockResolvedValueOnce({
      kinds: [{ kind: 'land', reach: 'performed', reachBy: 'landing', failureBy: 'landing', personJudges: false, personSkips: false, mayEnd: true,
        evidenced: true, skipBy: null, options: [{ name: 'method', type: 'choice', choices: ['merge'], required: false }] }],
      wires: [{ kind: 'failed', eligibility: true, from: ['land'], to: null }],
      plugins: [{ plugin: 'acme.deploy', steps: [{ kind: 'acme.deploy/deploy', reach: 'performed', rejectable: false, skippable: true, inputs: {}, outputs: {} }] }],
    });
    const { result } = renderHook(() => useWorkflowPalette(), { wrapper });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_PALETTE', expect.anything());
    expect(result.current.data!.kinds[0]!.options[0]!.choices).toEqual(['merge']);
    expect(result.current.data!.plugins[0]!.steps[0]!.kind).toBe('acme.deploy/deploy');
  });
});
