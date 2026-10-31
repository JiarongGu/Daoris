import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { RoleDiagram } from './RoleDiagram';
import { buildStructure, NO_FILTERS } from './roleStructure';
import { DRIVER, largeStructure, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS } from './structureFixtures';

// COMPANYVIEW1f (the company-view design §4; the flowchart design §3.7's reader and keys): the diagram is one list in reading order with
// one tab stop; ↓ ↑ Home End move in reading order, → along the first edge out and ← back, Enter chooses; past 24 nodes a workspace's
// fold opens by its press. A unit test's DOM measures nothing, so the diagram is its list here.

const diagramOf = (large = false) => buildStructure({
  ...(large ? largeStructure() : { structure: STRUCTURE, registry: REGISTRY }), tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] },
  plugins: PLUGINS, lines: large ? null : LINES, driver: large ? null : DRIVER, workflows: large ? {} : WORKFLOWS, filters: NO_FILTERS,
}).diagram;

const item = (name: string) => screen.getByRole('listitem', { name });

describe('the roles diagram', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(cleanup);

  it('is one list in reading order with one tab stop, its keys said to a reader', () => {
    render(<RoleDiagram diagram={diagramOf()} selected={null} onSelect={() => {}} />);
    const list = screen.getByRole('list', { name: 'The diagram: places, roles and agents' });
    expect(document.getElementById(list.getAttribute('aria-describedby')!)!.textContent).toMatch(/^Arrow keys move between places, roles and agents/);
    const items = within(list).getAllByRole('listitem');
    expect(items.map((each) => each.getAttribute('data-flow-node')).slice(0, 4)).toEqual(['place:machine', 'place:aurora', 'place:aurora/engine', 'place:default/tools']);
    expect(items.filter((each) => each.tabIndex === 0)).toHaveLength(1);
    expect(items[0]!.tabIndex).toBe(0);
  });

  it('moves down and up in reading order, along the first edge out and back, and chooses with Enter', async () => {
    const onSelect = vi.fn();
    render(<RoleDiagram diagram={diagramOf()} selected={null} onSelect={onSelect} />);
    const user = userEvent.setup();
    item('This Daoris').focus();
    await user.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(item('aurora'));
    await user.keyboard('{ArrowUp}{ArrowRight}');
    expect(document.activeElement).toBe(item('Build'));
    await user.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(item('Claude Code'));
    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(document.activeElement).toBe(item('This Daoris'));
    // The agents follow the roles leaning on them: Codex's two sit higher than Claude Code's eight.
    await user.keyboard('{End}');
    expect(document.activeElement).toBe(item('Claude Code'));
    await user.keyboard('{Enter}');
    expect(onSelect).toHaveBeenCalledWith('agent:claude-code');
  });

  it("follows an edge from its foot's press, and gives Escape back to the node", async () => {
    render(<RoleDiagram diagram={diagramOf()} selected="role:build" onSelect={() => {}} />);
    const user = userEvent.setup();
    const build = item('Build');
    expect(build).toHaveAttribute('aria-current', 'true');
    // The chosen node holds the tab stop.
    expect(build.tabIndex).toBe(0);
    const foot = within(build).getByRole('button', { name: /^Codex, choice 2/ });
    foot.focus();
    await user.keyboard('{Escape}');
    expect(document.activeElement).toBe(build);
    await user.click(foot);
    expect(document.activeElement).toBe(item('Codex'));
  });

  it('folds each workspace past 24 nodes, and opens one by its press, the focus on its first place', async () => {
    render(<RoleDiagram diagram={diagramOf(true)} selected={null} onSelect={() => {}} />);
    const user = userEvent.setup();
    const aurora = item('aurora');
    expect(aurora).toHaveAttribute('data-kind', 'fold');
    expect(aurora.textContent).toContain('9 places, folded');
    await user.click(within(aurora).getByRole('button', { name: 'Show its places' }));
    // Its first place is the workspace's own node, before its repositories.
    expect(item('aurora')).toHaveAttribute('data-kind', 'place');
    expect(document.activeElement).toBe(item('aurora'));
    expect(item('aurora-1')).toHaveAttribute('data-kind', 'place');
    expect(item('borealis')).toHaveAttribute('data-kind', 'fold');
    await user.click(screen.getByRole('button', { name: 'Show whole' }));
    expect(screen.queryAllByRole('listitem').filter((each) => each.getAttribute('data-kind') === 'fold')).toEqual([]);
  });
});
