import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { PluginKitCard, TrialReport, type KitPoint, type PluginTrialResult } from './PluginKit';

// PLUG8 (D101): the kit a plugin is made with, on the screen — New writes a plugin's folder where the
// person names, Try starts one as the driver would. `daoris-driver plugins new|try` is the other door.

const POINTS: KitPoint[] = [
  { name: 'quest/consider', kind: 'decision' },
  { name: 'session/ended', kind: 'observation' },
  { name: 'work/land', kind: 'act' },
];

const PASSED: PluginTrialResult = {
  plugin: 'acme.gate',
  folder: 'C:/work/plugins/acme.gate',
  command: ['node', 'C:/work/plugins/acme.gate/plugin.mjs'],
  passed: true,
  summary: '`acme.gate` answered as the driver reads it.',
  steps: [
    { name: 'handshake', ok: true, sentence: 'speaks hook wire 1 and listens on quest/consider.' },
    { name: 'quest/consider', ok: true, sentence: 'allow — the start would go ahead.' },
    { name: 'shutdown', ok: true, sentence: 'left when told (exit 0).' },
    { name: 'stdout', ok: true, sentence: 'held nothing but frames.' },
  ],
  said: [],
};

const FAILED: PluginTrialResult = {
  ...PASSED,
  passed: false,
  summary: '`acme.gate` failed 1 of 4 checks.',
  steps: [
    PASSED.steps[0]!,
    { name: 'quest/consider', ok: false, sentence: 'plugin `acme.gate` answered {"kind":"maybe"}, which is not a decision.' },
    PASSED.steps[2]!,
    PASSED.steps[3]!,
  ],
  said: ['cannot read the calendar'],
};

const draw = (props: Partial<Parameters<typeof PluginKitCard>[0]> = {}) => {
  const handlers = {
    onPick: vi.fn(async () => 'C:/work/plugins' as string | null),
    onMake: vi.fn(),
    onTry: vi.fn(),
  };
  render(
    <Tooltip.Provider>
      <PluginKitCard points={POINTS} {...handlers} {...props} />
    </Tooltip.Provider>,
  );
  return handlers;
};

describe('the plugin kit card', () => {
  it('offers every point the driver has, each with what kind of question it is', () => {
    draw();

    expect(screen.getByRole('checkbox', { name: /quest\/consider/ })).toBeInTheDocument();
    expect(screen.getByText(/a decision, before a planned start/)).toBeInTheDocument();
    expect(screen.getByText(/an observation, after a session ends/)).toBeInTheDocument();
    expect(screen.getByText(/an act, once a landing has made its branch/)).toBeInTheDocument();
  });

  // A zero-basis field never wraps its row, it only shrinks: with the side bar open the folder showed
  // two characters beside its two buttons (seen on the window). A floor makes the row wrap instead.
  it('keeps each field wide enough to read, wrapping its row rather than shrinking the field', () => {
    draw();

    for (const field of screen.getAllByRole('textbox')) {
      expect(field).toHaveClass('min-w-[min(14rem,100%)]', 'flex-1');
    }
  });

  it('makes nothing until it has an id, a point and a folder, then sends all three', async () => {
    const { onMake } = draw();
    const user = userEvent.setup();
    const make = screen.getByRole('button', { name: 'New' });

    expect(make).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: 'Plugin id' }), 'acme.gate');
    expect(make).toBeDisabled();
    await user.click(screen.getByRole('checkbox', { name: /quest\/consider/ }));
    await user.click(screen.getByRole('checkbox', { name: /work\/land/ }));
    expect(make).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: 'Make it in' }), 'C:/work/plugins');
    await user.click(make);

    expect(onMake).toHaveBeenCalledWith({ id: 'acme.gate', points: ['quest/consider', 'work/land'], folder: 'C:/work/plugins' });
  });

  it('fills a folder from the chooser, and a cancelled chooser leaves it as it was', async () => {
    const { onPick } = draw();
    const user = userEvent.setup();

    await user.click(within(screen.getByRole('group', { name: 'New' })).getByRole('button', { name: 'Choose…' }));
    expect(onPick).toHaveBeenCalled();
    expect(await screen.findByDisplayValue('C:/work/plugins')).toBeInTheDocument();

    onPick.mockResolvedValueOnce(null);
    await user.click(within(screen.getByRole('group', { name: 'New' })).getByRole('button', { name: 'Choose…' }));
    expect(screen.getByRole('textbox', { name: 'Make it in' })).toHaveValue('C:/work/plugins');
  });

  it('tries a folder, and offers the one just made', async () => {
    const { onTry } = draw({ made: 'C:/work/plugins/acme.gate' });
    const user = userEvent.setup();

    const folder = screen.getByRole('textbox', { name: 'Folder to try' });
    expect(folder).toHaveValue('C:/work/plugins/acme.gate');
    await user.click(screen.getByRole('button', { name: 'Try' }));

    expect(onTry).toHaveBeenCalledWith('C:/work/plugins/acme.gate');
  });

  it('holds every button while the kit works', () => {
    draw({ busy: true, made: 'C:/work/plugins/acme.gate' });

    expect(screen.getByRole('button', { name: 'Try' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'New' })).toBeDisabled();
  });

  it('shows the folder trial\'s report beneath', () => {
    draw({ trial: FAILED });

    expect(screen.getByRole('region', { name: 'What try found' })).toBeInTheDocument();
  });
});

describe('a trial\'s report', () => {
  it('lists each check with its verdict and the driver\'s own sentence, then the summary', () => {
    render(<TrialReport trial={FAILED} />);

    const checks = screen.getAllByRole('listitem');
    expect(checks).toHaveLength(4);
    expect(within(checks[1]!).getByText('failed')).toBeInTheDocument();
    expect(within(checks[1]!).getByText('quest/consider')).toBeInTheDocument();
    // Content, verbatim: the driver's sentence is not translated.
    expect(checks[1]).toHaveTextContent('which is not a decision.');
    expect(within(checks[0]!).getByText('ok')).toBeInTheDocument();
    expect(screen.getByText(/failed 1 of 4 checks/)).toBeInTheDocument();
    // What the plugin said on stderr, as it said it.
    expect(screen.getByText(/cannot read the calendar/)).toBeInTheDocument();
  });

  it('says nothing about stderr when the plugin said nothing there', () => {
    render(<TrialReport trial={PASSED} />);

    expect(screen.getByText(/answered as the driver reads it/)).toBeInTheDocument();
    expect(screen.queryByText('What it said on stderr')).toBeNull();
  });
});
