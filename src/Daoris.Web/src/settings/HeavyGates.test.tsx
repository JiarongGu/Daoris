import { act, cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import type { Answered } from '../work/InlineConfirm';
import { type HeavyGate, HeavyGates, type HeavyMarks, heavySummary, heavyToast } from './HeavyGates';

// HEAVYRUN1b2b (D180's HEAVYRUN1b2a note): this machine's heavy gates for a repository whose own daoris.gates.json marks none, shown
// and edited on its Setup. The list is one sequence, so it is edited whole and saved whole, in its order, or cleared; the driver
// judges it, and its refusal is said where Save was pressed.

const t = i18n.t.bind(i18n);

const GATES: HeavyGate[] = [
  { name: 'build', run: 'dotnet build -nodeReuse:false' },
  { name: 'web', run: 'npm test -- --maxWorkers=2', cwd: 'src/web' },
  { name: 'e2e', run: 'npm run e2e' },
];

const draw = (gates: HeavyGate[], onSave = vi.fn(), marks?: HeavyMarks) => {
  render(
    <Tooltip.Provider>
      <HeavyGates repository="engine" gates={gates} marks={marks} onSave={onSave} />
    </Tooltip.Provider>,
  );
  return onSave;
};

/** The row's own region: the gate list's setting row, found by its label. */
const row = (label: string) => screen.getByText(label, { selector: 'span' }).closest('.\\@container') as HTMLElement;

afterEach(async () => { await i18n.changeLanguage('en'); });

describe("a repository's heavy gates folded to a line", () => {
  it('names each gate in its order, or says none is set here', () => {
    expect(heavySummary(t, GATES)).toBe('3 heavy gates: build, web, e2e');
    expect(heavySummary(t, GATES.slice(0, 1))).toBe('1 heavy gate: build');
    expect(heavySummary(t, [])).toBe('none set on this machine');
  });

  it('says what a save did, as a toast', () => {
    expect(heavyToast(t, 'engine', GATES)).toBe('3 heavy gates set for engine on this machine, run in the order shown.');
    expect(heavyToast(t, 'engine', GATES.slice(0, 1))).toBe('1 heavy gate set for engine on this machine.');
    expect(heavyToast(t, 'engine', null)).toBe("engine's heavy gates on this machine are cleared.");
  });
});

describe("a repository's heavy gates on its Setup", () => {
  it('shows each gate in its order with its command and folder, its twin, and plainly where it applies', () => {
    draw(GATES);

    const list = screen.getByRole('list', { name: "engine's heavy gates, in the order they run" });
    const items = within(list).getAllByRole('listitem');
    expect(items.map((item) => item.textContent)).toEqual([
      'builddotnet build -nodeReuse:false',
      'webnpm test -- --maxWorkers=2in src/web',
      'e2enpm run e2e',
    ]);
    const mine = row("This machine's heavy gates");
    expect(within(mine).getByText(code('daoris-driver heavy engine <name>=<command> [--cwd <folder>]…|--clear'))).toBeInTheDocument();
    expect(mine).toHaveTextContent("Used only where engine's own daoris.gates.json marks no heavy gate");
    expect(within(mine).getByText(code('npm test -- --maxWorkers=2'), { selector: 'p code' })).toBeInTheDocument();
    expect(within(mine).getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(within(mine).getByRole('button', { name: 'Clear' })).toBeInTheDocument();
  });

  it('says none is set and offers to add them, with nothing to clear', async () => {
    const onSave = draw([]);

    const mine = row("This machine's heavy gates");
    expect(mine).toHaveTextContent('None set on this machine.');
    expect(within(mine).queryByRole('button', { name: 'Clear' })).toBeNull();
    await userEvent.click(within(mine).getByRole('button', { name: 'Add heavy gates' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Name of gate 1' }), 'unit');
    await userEvent.type(screen.getByRole('textbox', { name: 'Command of gate 1' }), 'npm test');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenLastCalledWith([{ name: 'unit', run: 'npm test' }], expect.anything());
  });

  it('edits the whole list in place, its order moved, a gate removed and one added, and saves it whole', async () => {
    const onSave = draw(GATES);

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move gate 1 up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move gate 3 down' })).toBeDisabled();

    const command = screen.getByRole('textbox', { name: 'Command of gate 1' });
    await userEvent.clear(command);
    await userEvent.type(command, 'dotnet build');
    await userEvent.click(screen.getByRole('button', { name: 'Move gate 2 up' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove gate 3' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a gate' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Name of gate 3' }), 'lint');
    await userEvent.type(screen.getByRole('textbox', { name: 'Command of gate 3' }), 'npm run lint');
    await userEvent.type(screen.getByRole('textbox', { name: 'Folder of gate 3' }), ' src/web ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenLastCalledWith([
      { name: 'web', run: 'npm test -- --maxWorkers=2', cwd: 'src/web' },
      { name: 'build', run: 'dotnet build' },
      { name: 'lint', run: 'npm run lint', cwd: 'src/web' },
    ], expect.anything());
  });

  it('passes over a row left wholly blank, and saves a list emptied as none', async () => {
    const onSave = draw(GATES.slice(0, 1));

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a gate' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Remove gate 1' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenLastCalledWith(null, expect.anything());
  });

  it('sends a half-written gate as it stands, for the driver to judge', async () => {
    const onSave = draw([]);

    await userEvent.click(screen.getByRole('button', { name: 'Add heavy gates' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Command of gate 1' }), 'npm test');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenLastCalledWith([{ name: '', run: 'npm test' }], expect.anything());
  });

  it("says the driver's refusal where Save was pressed and keeps the list open, and closes it once the save lands", async () => {
    const onSave = draw(GATES);

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Name of gate 3' }), '2');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    const answered = onSave.mock.lastCall![1] as Answered;
    act(() => answered.refused("`web`'s folder, `../web`, is not under the repository's root: name it from there, with no `..`."));

    expect(screen.getByRole('alert')).toHaveTextContent("is not under the repository's root");
    expect(screen.getByRole('textbox', { name: 'Name of gate 3' })).toHaveValue('e2e2');

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    act(() => (onSave.mock.lastCall![1] as Answered).done());
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('textbox', { name: 'Name of gate 1' })).toBeNull();
  });

  it('clears the whole list with one press, and cancelling an edit sends nothing', async () => {
    const onSave = draw(GATES);

    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove gate 1' }));
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getAllByRole('listitem')).toHaveLength(3);

    await userEvent.click(screen.getByRole('button', { name: 'Clear' }));
    expect(onSave).toHaveBeenLastCalledWith(null, expect.anything());
  });

  /**
   * HEAVYRUN1b2c (D180): the section says whether this repository's own file wins, as the driver read it (`HEAVY_MARKS`). The page judges
   * nothing: a file that does not read is "cannot tell" in the driver's words, never "marks none".
   */
  it("says whether the repository's own file wins, and cues the folded line where it does", async () => {
    const wins: HeavyMarks = { marks: true, names: ['web', 'e2e'] };
    draw(GATES, vi.fn(), wins);
    const mine = row("This machine's heavy gates");
    expect(mine).toHaveTextContent("engine's own daoris.gates.json marks heavy gates (web, e2e), so those are run and this list is not.");
    expect(heavySummary(t, GATES, wins)).toBe('3 heavy gates: build, web, e2e (not used: its own file marks heavy gates)');
    expect(heavySummary(t, [], wins)).toBe('none set on this machine');
  });

  it('says this list is used where the file marks none or there is none, and says cannot tell in the driver\'s words', () => {
    const { unmount } = render(
      <Tooltip.Provider><HeavyGates repository="engine" gates={GATES} marks={{ marks: false, names: [] }} onSave={vi.fn()} /></Tooltip.Provider>,
    );
    expect(row("This machine's heavy gates")).toHaveTextContent("engine's own daoris.gates.json marks no heavy gate, or there is none here, so this list is used.");
    expect(heavySummary(t, GATES, { marks: false, names: [] })).toBe('3 heavy gates: build, web, e2e');
    unmount();

    draw(GATES, vi.fn(), { marks: null, names: [], refusal: 'daoris.gates.json is not valid JSON: x' });
    const mine = row("This machine's heavy gates");
    expect(mine).toHaveTextContent("Cannot tell whether engine's own daoris.gates.json marks heavy gates: daoris.gates.json is not valid JSON: x");
    expect(mine).not.toHaveTextContent('marks no heavy gate');
    expect(heavySummary(t, GATES, { marks: null, names: [], refusal: 'x' })).toBe('3 heavy gates: build, web, e2e');
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw(GATES, vi.fn(), { marks: true, names: ['web'] });
    expect(row('本机的重型检查')).toHaveTextContent('engine 自己的 daoris.gates.json 标出了重型检查（web），所以运行它标出的，本列表不运行。');
    expect(heavySummary(t, GATES, { marks: true, names: ['web'] })).toBe('3 项重型检查：build、web、e2e（未使用：它自己的文件标出了重型检查）');
    cleanup();
    draw(GATES);

    const mine = row('本机的重型检查');
    expect(mine).toHaveTextContent('只在 engine 自己的 daoris.gates.json 没有标出重型检查时使用');
    expect(within(mine).getByRole('button', { name: '编辑' })).toBeInTheDocument();
    expect(heavySummary(t, GATES)).toBe('3 项重型检查：build、web、e2e');
    expect(heavySummary(t, [])).toBe('本机未设定');
    expect(heavyToast(t, 'engine', null)).toBe('已清除 engine 在本机的重型检查。');

    await userEvent.click(within(mine).getByRole('button', { name: '编辑' }));
    expect(screen.getByRole('textbox', { name: '第 2 项的命令' })).toHaveValue('npm test -- --maxWorkers=2');
    expect(screen.getByRole('button', { name: '上移第 2 项' })).toBeEnabled();
  });
});
