import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { code } from '../test/code';
import { dataText, LOG_FILTERS, LogList, type LogFilters, type LogReading } from './Logs';

// LOG1c (D94): Settings → Logs, the screen's door to the machine log — the recent lines newest first,
// the terminal's filters, a count per level, and the folder. Drawn from props: SettingsView holds the
// bridge, and `daoris-driver logs` is the terminal's door to the same reading (D50).

const READING: LogReading = {
  folder: 'C:/somewhere/data/logs',
  lines: [
    { time: '2026-09-30T11:30:00.000Z', source: 'desktop', level: 'error', event: 'page.error', data: { where: 'window', message: 'x is undefined' } },
    { time: '2026-09-30T11:00:00.000Z', source: 'host', level: 'warn', event: 'request.failed', data: { method: 'GET', route: '/api/search', status: 500 } },
    { time: '2026-09-30T09:00:00.000Z', source: 'desktop', level: 'info', event: 'session.opened', data: { session: '76cdd5db', openMs: 5840 } },
  ],
  total: 3,
  counts: { info: 1, warn: 1, error: 1 },
  events: ['page.error', 'request.failed', 'session.opened'],
  skipped: 0,
};

const draw = (props: Partial<Parameters<typeof LogList>[0]> = {}) => {
  const handlers = { onFilters: vi.fn(), onOpenFolder: vi.fn(), onRefresh: vi.fn() };
  render(
    <Tooltip.Provider>
      <LogList reading={READING} filters={LOG_FILTERS} {...handlers} {...props} />
    </Tooltip.Provider>,
  );
  return handlers;
};

const rows = () => within(screen.getByRole('list', { name: 'log lines' })).getAllByRole('listitem');

describe('the logs domain', () => {
  /**
   * NAME1b (UX5 U57): the card is alone in its domain and the list names it *Machine log*, so it carries
   * no title of its own, as every card alone in its domain leaves it; it opens on what the log is.
   */
  it('carries no title of its own, and opens on what the log is', () => {
    draw();

    expect(screen.queryByRole('heading')).toBeNull();
    expect(screen.queryByText('Machine log')).toBeNull();
  });

  /** LOOK5: the opening names its terminal twin as code, as every setting's is, and never prints the backticks. */
  it('sets the terminal twin in its opening as code', () => {
    draw();

    expect(screen.getByText(code('daoris-driver logs'))).toBeInTheDocument();
    expect(screen.queryByText(/`/)).toBeNull();
  });

  it('lists the lines newest first, each with its source, level, event and data', () => {
    draw();

    expect(rows().map((row) => row.getAttribute('aria-label'))).toEqual(['page.error', 'request.failed', 'session.opened']);
    const failed = within(rows()[1]!);
    expect(failed.getByText('host')).toBeInTheDocument();
    expect(failed.getByText('warning')).toBeInTheDocument();
    expect(failed.getByText('method=GET route=/api/search status=500')).toBeInTheDocument();
    expect(within(rows()[0]!).getByText('where=window message="x is undefined"')).toBeInTheDocument();
  });

  it('counts each level, and says where the files are with a way to open them', async () => {
    const { onOpenFolder } = draw();

    expect(screen.getByText('1 info · 1 warning · 1 error')).toBeInTheDocument();
    expect(screen.getByText('C:/somewhere/data/logs')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Open the folder' }));
    expect(onOpenFolder).toHaveBeenCalledOnce();
  });

  it('changes a filter by handing the whole set back, as the terminal takes its flags', async () => {
    const { onFilters, onRefresh } = draw();

    await userEvent.click(screen.getByRole('radio', { name: 'Last hour' }));
    expect(onFilters).toHaveBeenLastCalledWith({ ...LOG_FILTERS, since: '1h' } satisfies LogFilters);

    await userEvent.click(screen.getByRole('radio', { name: 'Errors' }));
    expect(onFilters).toHaveBeenLastCalledWith({ ...LOG_FILTERS, level: 'error' } satisfies LogFilters);

    await userEvent.click(screen.getByRole('button', { name: 'Read again' }));
    expect(onRefresh).toHaveBeenCalledOnce();
  });

  it('offers every source and the events the period holds, each with an every choice', async () => {
    const { onFilters } = draw();
    // Opened from the keyboard, as the asks' select is: a click's pointer state carried over from an
    // earlier test in the file left the next select shut.
    const user = userEvent.setup();

    screen.getByRole('combobox', { name: 'Source' }).focus();
    await user.keyboard('{Enter}');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent))
      .toEqual(['Every source', 'desktop', 'host', 'mcp', 'browser', 'driver']);
    await user.click(screen.getByRole('option', { name: 'host' }));
    expect(onFilters).toHaveBeenLastCalledWith({ ...LOG_FILTERS, source: 'host' } satisfies LogFilters);

    screen.getByRole('combobox', { name: 'Event' }).focus();
    await user.keyboard('{Enter}');
    expect((await screen.findAllByRole('option')).map((option) => option.textContent))
      .toEqual(['Every event', 'page.error', 'request.failed', 'session.opened']);
  });

  it('keeps an event chosen earlier among the choices when the period no longer holds it', async () => {
    draw({ filters: { ...LOG_FILTERS, event: 'turn.ended' } });
    const user = userEvent.setup();

    expect(screen.getByRole('combobox', { name: 'Event' })).toHaveTextContent('turn.ended');
    screen.getByRole('combobox', { name: 'Event' }).focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('option', { name: 'turn.ended' })).toBeInTheDocument();
  });

  it('says when it shows only the newest part, and how many lines could not be read', () => {
    draw({ reading: { ...READING, total: 1234, skipped: 2 } });

    expect(screen.getByText('The newest 3 of 1,234 lines.')).toBeInTheDocument();
    expect(screen.getByText('2 lines could not be read and were skipped.')).toBeInTheDocument();
  });

  it('says when nothing matches, and draws placeholders while the shell is asked', () => {
    draw({ reading: { ...READING, lines: [], total: 0, counts: { info: 0, warn: 0, error: 0 } } });
    expect(screen.getByText('Nothing in the log for these filters.')).toBeInTheDocument();
  });

  it('draws no list while the shell is asked', () => {
    draw({ reading: undefined });
    expect(screen.queryByRole('list', { name: 'log lines' })).toBeNull();
  });
});

describe('a line\'s data, as key=value', () => {
  it('writes a plain value bare, and quotes one that would break the line', () => {
    expect(dataText({ view: 'sessions', openMs: 5840, applied: true, missing: null })).toBe('view=sessions openMs=5840 applied=true missing=null');
    expect(dataText({ where: 'the driver loop', message: 'a "quoted" word', empty: '' }))
      .toBe('where="the driver loop" message="a \\"quoted\\" word" empty=""');
  });

  it('cuts a long value, so a stack does not fill the screen', () => {
    const text = dataText({ stack: 'at A.B()\n'.repeat(100) });
    expect(text.length).toBeLessThan(220);
    expect(text.endsWith('…"')).toBe(true);
  });
});
