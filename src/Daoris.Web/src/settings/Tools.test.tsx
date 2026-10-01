import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import {
  changes, choiceOf, endSaid, GitSwitch, ToolCard, type ToolChoice, ToolLocations, type ToolShown,
} from './Tools';
import { BUILT_IN, GH, GIT, MIRROR, NODE, PWSH } from './toolsFixtures';

// TOOLS7 (D121 §4.1): one tool's card, git's switch said before it applies, and the resource locations, drawn from
// props. ToolsDomain holds the bridge, and `daoris tool` is the terminal's door to the same file (D50).

const card = (tool: ToolShown, props: Partial<Parameters<typeof ToolCard>[0]> = {}) => {
  const handlers = {
    onChoice: vi.fn(), onUse: vi.fn(), onDownload: vi.fn(), onStop: vi.fn(), onDelete: vi.fn(),
  };
  const view = render(
    <Tooltip.Provider>
      <ToolCard tool={tool} choice={choiceOf(tool)} {...handlers} {...props} />
    </Tooltip.Provider>,
  );
  return { ...handlers, view };
};

describe('a tool\'s choice', () => {
  it('opens on the way set, at the version or the file set, else what a person would pick first', () => {
    expect(choiceOf(GIT)).toEqual({ way: 'system', version: '2.51.0', file: '' });
    expect(choiceOf(NODE)).toEqual({ way: 'managed', version: '22.11.0', file: '' });
    expect(choiceOf(PWSH)).toEqual({ way: 'file', version: '', file: 'C:/somewhere/PowerShell/7/pwsh.exe' });
    // A file that does not read refuses every tool and says no way: the card opens on the system's.
    expect(choiceOf({ ...GH, way: null })).toMatchObject({ way: 'system', version: '2.63.0' });
  });

  it('changes what runs only where it differs from the way set', () => {
    const at = (choice: Partial<ToolChoice>, tool = NODE) => changes(tool, { ...choiceOf(tool), ...choice });
    expect(at({})).toBe(false);
    expect(at({ version: '20.18.0' })).toBe(true);
    expect(at({ way: 'system' })).toBe(true);
    expect(at({ file: '  C:/somewhere/PowerShell/7/pwsh.exe ' }, PWSH)).toBe(false);
    expect(at({ file: 'C:/elsewhere/pwsh.exe' }, PWSH)).toBe(true);
  });
});

describe('a tool\'s card', () => {
  /** What Daoris needs it for, how it is run as one choice of three, the file it runs and the version it answers. */
  it('says what Daoris needs it for, how it is run, and the file and version it runs', () => {
    card(GIT);

    const article = screen.getByRole('article');
    expect(within(article).getByText('Git')).toBeInTheDocument();
    expect(within(article).getByText(/session trees, landings, reviews/)).toBeInTheDocument();
    const way = screen.getByRole('radiogroup', { name: 'How Git is run' });
    expect(within(way).getAllByRole('radio').map((radio) => radio.textContent)).toEqual(['System', 'Managed', 'Custom']);
    expect(within(way).getByRole('radio', { name: 'System' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText('C:/somewhere/Git/cmd/git.exe')).toBeInTheDocument();
    expect(screen.getByText('It answers version 2.47.1.')).toBeInTheDocument();
    // The way set is the way chosen: nothing to press.
    expect(screen.queryByRole('button', { name: "Use the system's" })).toBeNull();
  });

  /** Nothing applies on choosing (§4.1): a segment shows a way's controls, and a press applies it. */
  it('applies nothing on choosing a way: the choice goes up, and only a press uses it', async () => {
    const { onChoice, onUse } = card(GIT);

    await userEvent.click(screen.getByRole('radio', { name: 'Managed' }));

    expect(onChoice).toHaveBeenCalledWith({ way: 'managed', version: '2.51.0', file: '' });
    expect(onUse).not.toHaveBeenCalled();
  });

  it('offers the system\'s with what it would run, and uses it on the press', async () => {
    const { onUse } = card(NODE, { choice: { ...choiceOf(NODE), way: 'system' } });

    expect(screen.getByText('C:/somewhere/nodejs/node.exe')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: "Use the system's" }));

    expect(onUse).toHaveBeenCalledWith({ action: 'system' });
  });

  /**
   * Managed (§4.1): the versions downloaded, then those the lists name for this machine with their sizes, the licence
   * and where it downloads from; *Download* for one not here and *Use this version*, and *Delete* for one here and not
   * in use, which asks once.
   */
  it('offers managed versions downloaded first, then the lists\', and downloads, uses and deletes on the presses', async () => {
    const { onDownload, onUse, onDelete, onChoice } = card(NODE, { choice: { ...choiceOf(NODE), version: '22.12.0' } });

    expect(screen.getByText(/MIT licence, downloaded from mirror\.example, nodejs\.org by this machine/)).toBeInTheDocument();
    expect(screen.getByText('Named by https://mirror.example/resources.json, the list built in')).toBeInTheDocument();
    // A select opens from the keyboard in this document, as the asks' tests open theirs.
    screen.getByRole('combobox', { name: 'The version of Node.js' }).focus();
    await userEvent.keyboard('{Enter}');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual([
      '22.11.0 · in use', '20.18.0 · downloaded', '22.12.0 · 31.2 MB',
    ]);
    await userEvent.keyboard('{Escape}');

    await userEvent.click(screen.getByRole('button', { name: 'Download' }));
    expect(onDownload).toHaveBeenCalledWith('22.12.0');
    await userEvent.click(screen.getByRole('button', { name: 'Use this version' }));
    expect(onUse).toHaveBeenCalledWith({ action: 'managed', version: '22.12.0' });
    // A version not here is not deleted.
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull();
    expect(onChoice).not.toHaveBeenCalled();
    expect(onDelete).not.toHaveBeenCalled();
  });

  it('deletes a downloaded version nothing uses only on the second press, and never the one in use', async () => {
    const { onDelete, view } = card(NODE, { choice: { ...choiceOf(NODE), version: '20.18.0' } });

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    const asking = screen.getByRole('group', { name: 'Delete Node.js 20.18.0' });
    expect(within(asking).getByText(/removes Node\.js 20\.18\.0 from Daoris's home/)).toBeInTheDocument();
    expect(onDelete).not.toHaveBeenCalled();
    await userEvent.click(within(asking).getByRole('button', { name: 'Confirm delete' }));
    expect(onDelete).toHaveBeenCalledWith('20.18.0');

    view.unmount();
    card(NODE);
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull();
  });

  it('says where no list names a version for this machine, and what changes that', () => {
    card({ ...GH, offered: [] }, { choice: { way: 'managed', version: '', file: '' } });

    expect(screen.getByText(/No list names a version of GitHub CLI for this machine/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Download' })).toBeNull();
  });

  /** A file the person names (§4.1): its whole path, *Browse…* where the window has a picker, and *Use this file*. */
  it('takes a named file by its whole path, with Browse where there is a picker', async () => {
    const onBrowse = vi.fn();
    const { onChoice, onUse } = card(GH, { choice: { ...choiceOf(GH), way: 'file', file: 'C:/tools/gh.exe' }, onBrowse });

    await userEvent.click(screen.getByRole('button', { name: 'Browse…' }));
    expect(onBrowse).toHaveBeenCalled();
    await userEvent.type(screen.getByRole('textbox', { name: 'The file GitHub CLI runs' }), 'x');
    expect(onChoice).toHaveBeenLastCalledWith({ way: 'file', version: '2.63.0', file: 'C:/tools/gh.exex' });
    await userEvent.click(screen.getByRole('button', { name: 'Use this file' }));
    expect(onUse).toHaveBeenCalledWith({ action: 'file', file: 'C:/tools/gh.exe' });
  });

  /** The way set cannot run: the driver's sentence leads, and the tool is marked not found, never run another way. */
  it('leads with the driver\'s sentence when the way set cannot run', () => {
    card(PWSH);

    expect(screen.getByText(/there is no file there — it never falls back to PATH/)).toBeInTheDocument();
    expect(screen.getByText('not found')).toBeInTheDocument();
  });

  it('marks a system tool PATH does not find, and says what gives Daoris one', () => {
    card(GH);

    expect(screen.getAllByText('not found').length).toBeGreaterThan(0);
    expect(screen.getByText(/GitHub CLI is not on this machine's PATH\. Managed or Custom gives Daoris one/)).toBeInTheDocument();
  });

  it('shows a running download with its stop and its console, and offers no second press', async () => {
    const { onStop } = card({ ...GH, running: { action: 'download', version: '2.63.0' } }, {
      choice: { ...choiceOf(GH), way: 'managed' },
      console: <pre>downloading https://github.com/…</pre>,
    });

    expect(screen.getByText('downloading')).toBeInTheDocument();
    expect(screen.getByText(/GitHub CLI 2\.63\.0 is downloading/)).toBeInTheDocument();
    expect(screen.getByText('downloading https://github.com/…')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Stop download' }));
    expect(onStop).toHaveBeenCalled();
  });

  /** Git's switch is said before it applies (§4.1): its press asks, and a version not downloaded is downloaded first. */
  it('asks before git switches, and downloads a version first, since the switch is asked of it', async () => {
    const onAsk = vi.fn();
    const { onUse, view } = card(GIT, { choice: { ...choiceOf(GIT), way: 'managed', version: '2.51.0' }, onAsk });

    await userEvent.click(screen.getByRole('button', { name: 'Use this version' }));
    expect(onAsk).toHaveBeenCalledWith({ action: 'managed', version: '2.51.0' });
    expect(onUse).not.toHaveBeenCalled();

    view.unmount();
    card(GIT, { choice: { ...choiceOf(GIT), way: 'managed', version: '2.50.1' }, onAsk });
    expect(screen.queryByRole('button', { name: 'Use this version' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Download' })).toBeInTheDocument();
    expect(screen.getByText(/so this version is downloaded first/)).toBeInTheDocument();
  });
});

describe('what a switch of git changes', () => {
  const draw = (asking: Parameters<typeof GitSwitch>[0]['asking']) => {
    const handlers = { onConfirm: vi.fn(), onCancel: vi.fn() };
    render(<GitSwitch asking={asking} {...handlers} />);
    return handlers;
  };

  it('names each checkout key the two read differently, as it is now and after, and switches on the press', async () => {
    const { onConfirm } = draw({
      use: { action: 'managed', version: '2.51.0' },
      reading: false,
      answer: {
        now: { file: 'C:/somewhere/Git/cmd/git.exe' },
        then: { file: 'C:/somewhere/data/tools/git/2.51.0/package/cmd/git.exe' },
        keys: [{ key: 'core.autocrlf', now: 'true', then: null }, { key: 'core.symlinks', now: 'false', then: 'true' }],
        same: false,
      },
    });

    expect(screen.getByText(/The two read a checkout differently/)).toBeInTheDocument();
    const items = screen.getAllByRole('listitem').map((item) => item.textContent);
    expect(items).toEqual(['core.autocrlf is true now, and not set after', 'core.symlinks is false now, and true after']);
    await userEvent.click(screen.getByRole('button', { name: 'Switch Git' }));
    expect(onConfirm).toHaveBeenCalled();
  });

  it('says when the two read a checkout the same way, while it asks, and when it cannot say', () => {
    draw({ use: { action: 'system' }, reading: false, answer: { now: {}, then: {}, keys: [], same: true } });
    expect(screen.getByText('Both read a checkout the same way.')).toBeInTheDocument();
  });

  it('holds the switch while it asks', () => {
    draw({ use: { action: 'system' }, reading: true });
    expect(screen.getByText(/Asking each Git how it reads a checkout/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Switch Git' })).toBeDisabled();
  });

  it('says why it cannot say, in the answer\'s own words', async () => {
    const { onCancel } = draw({ use: { action: 'system' }, reading: false, problem: '`git` is not on this machine\'s PATH' });
    expect(screen.getByText(/What it changes cannot be said/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(onCancel).toHaveBeenCalled();
  });
});

describe('the resource locations', () => {
  const draw = (props: Partial<Parameters<typeof ToolLocations>[0]> = {}) => {
    const handlers = { onLook: vi.fn(), onAdd: vi.fn(), onRemove: vi.fn() };
    render(
      <Tooltip.Provider>
        <ToolLocations locations={[MIRROR]} builtIn={BUILT_IN} {...handlers} {...props} />
      </Tooltip.Provider>,
    );
    return handlers;
  };

  /** §3.3: the person's in order, each with what vouches for it, when fetched and what it names, then the list built in. */
  it('lists the person\'s locations, then the list built in, which cannot be removed', async () => {
    const { onRemove } = draw();

    expect(screen.getByText('https://mirror.example/resources.json')).toBeInTheDocument();
    expect(screen.getByText('mirror.example · fetched 2h ago · 1 version for this machine')).toBeInTheDocument();
    expect(screen.getByText('Built in')).toBeInTheDocument();
    expect(screen.getByText('built in · 5 versions for this machine')).toBeInTheDocument();
    const removes = screen.getAllByRole('button', { name: 'Remove' });
    expect(removes).toHaveLength(1);
    await userEvent.click(removes[0]!);
    expect(onRemove).toHaveBeenCalledWith('https://mirror.example/resources.json');
  });

  it('adds a location behind a press, and looks for updates for every location', async () => {
    const { onAdd, onLook } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Add location…' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Address' }), 'https://team.example/resources.json');
    await userEvent.click(screen.getByRole('button', { name: 'Add location' }));
    expect(onAdd).toHaveBeenCalledWith('https://team.example/resources.json');

    await userEvent.click(screen.getByRole('button', { name: 'Look for updates' }));
    expect(onLook).toHaveBeenCalled();
  });

  it('offers no look where only the list built in is read, and says what adds a location', () => {
    draw({ locations: [] });

    expect(screen.queryByRole('button', { name: 'Look for updates' })).toBeNull();
    expect(screen.getByText(/Only the list built in is read/)).toBeInTheDocument();
  });

  it('says what the last look fetched, added and could not fetch, in the driver\'s words for a failure', () => {
    draw({
      locations: [MIRROR, { address: 'https://gone.example/resources.json', integrity: 'gone.example', exists: false, names: [], notes: [] }],
      looks: [
        { address: MIRROR.address!, outcome: 'fetched', sentence: 'fetched', added: ['node 22.12.0'], dropped: [] },
        { address: 'https://gone.example/resources.json', outcome: 'failed', sentence: 'https://gone.example/resources.json could not be fetched (could not reach gone.example); it has never been fetched, so it names nothing', added: [], dropped: [] },
      ],
    });

    expect(screen.getByText('fetched')).toBeInTheDocument();
    expect(screen.getByText('Added node 22.12.0')).toBeInTheDocument();
    expect(screen.getByText('failed')).toBeInTheDocument();
    expect(screen.getByText(/could not reach gone\.example/)).toBeInTheDocument();
    expect(screen.getByText('Never fetched, so it names nothing yet.')).toBeInTheDocument();
  });
});

describe('how a tool\'s action ended', () => {
  const t = i18n.t.bind(i18n) as unknown as Parameters<typeof endSaid>[0];
  const end = { tool: 'gh', name: 'GitHub CLI', action: 'download', version: '2.63.0', exitCode: 0, stopped: false };

  it('says a download, a switch, a stop and a refusal each in its own sentence', () => {
    expect(endSaid(t, end)).toBe('GitHub CLI 2.63.0 is downloaded and verified. Nothing switched.');
    expect(endSaid(t, { ...end, action: 'use' })).toBe('GitHub CLI now runs managed 2.63.0.');
    expect(endSaid(t, { ...end, exitCode: -1, stopped: true })).toBe('The download of GitHub CLI 2.63.0 was stopped. Nothing was kept, and nothing switched.');
    // The check is the typed half, and the driver's sentence stands as it wrote it.
    expect(endSaid(t, { ...end, exitCode: 1, check: 'hash', problem: 'GitHub CLI 2.63.0 was not downloaded: …' }))
      .toBe('GitHub CLI 2.63.0 is refused by its `hash` check: GitHub CLI 2.63.0 was not downloaded: …');
    // An end that failed with no check (a tool error) still says the driver's words.
    expect(endSaid(t, { ...end, exitCode: 2, problem: 'disk full' })).toBe('GitHub CLI 2.63.0 did not finish: disk full');
  });
});
