import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { type PressView, PressViewLine } from '../PressViewLine';

// A workflow's draft saved or applied is drawn on the Confirm card from WORKFLOW_SAVE's view: the graph as the editor draws it, and its
// changes against the version it replaces, worded from the catalogue in the window's language.

const GRAPH = {
  schemaVersion: 2, entries: ['qa'], end: 'close',
  nodes: [
    { id: 'qa', kind: 'human', version: 1, reach: 'pressed', options: { brief: 'Verify the release' } },
    { id: 'landing', kind: 'land', version: 1, reach: 'performed', options: { method: 'merge' } },
    { id: 'close', kind: 'quest.close', version: 1, reach: 'performed' },
  ],
  wires: [{ id: 'a', kind: 'after', from: 'qa', to: 'landing' }, { id: 'b', kind: 'after', from: 'landing', to: 'close' }],
};

const EDIT: PressView = {
  kind: 'workflow-save-edit',
  facts: { id: 'docs', name: 'Docs', base: '1', version: '2' },
  command: 'daoris workflow apply docs <file> --base 1',
  workflow: {
    graph: GRAPH,
    changes: [
      { id: 'step:+qa', what: 'step', change: 'added', step: 'qa', kind: 'human', fields: [], needs: [], said: 'Add the step `qa`.' },
      { id: 'way:+a', what: 'way', change: 'added', way: 'a', kind: 'after', fields: [], needs: [], said: 'Add the way `a`.' },
      { id: 'graph:~entries', what: 'graph', change: 'changed', fields: ['entries'], needs: [], said: 'Change the entries.' },
    ],
  },
};

const NEW: PressView = {
  kind: 'workflow-save-new',
  facts: { id: 'docs', name: 'Docs to a pull request', version: '1' },
  command: 'daoris workflow import <file>',
  workflow: { graph: GRAPH, changes: null },
};

describe("a workflow's save on the Confirm card", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says the save, draws the graph as the editor draws it, and lists its changes against the version it replaces', () => {
    render(<PressViewLine view={EDIT} />);

    expect(screen.getByText(/^Saves workflow docs \(Docs\) v1 → v2/)).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'The workflow as it would be saved' })).toBeInTheDocument();
    const changes = within(screen.getByRole('list', { name: 'Changes against v1' }));
    expect(changes.getByText(/^Adds the step qa/)).toBeInTheDocument();
    expect(changes.getByText('Adds the way a.')).toBeInTheDocument();
    expect(changes.getByText("Changes the graph's entries.")).toBeInTheDocument();
    expect(screen.getByText(EDIT.command)).toBeInTheDocument();
  });

  it('draws a new workflow with no changes, since it replaces none', () => {
    render(<PressViewLine view={NEW} />);

    expect(screen.getByText(/^Saves a new workflow docs \(Docs to a pull request\) as v1/)).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'The workflow as it would be saved' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: /^Changes against/ })).toBeNull();
  });

  it('words the save and its changes in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<PressViewLine view={EDIT} />);

    expect(screen.getByText(/^保存工作流 docs（Docs）v1 → v2/)).toBeInTheDocument();
    expect(screen.getByRole('group', { name: '保存后的工作流' })).toBeInTheDocument();
    const changes = within(screen.getByRole('list', { name: '相对 v1 的改动' }));
    expect(changes.getByText(/^新增步骤 qa/)).toBeInTheDocument();
    expect(changes.getByText('新增路径 a。')).toBeInTheDocument();
  });
});
