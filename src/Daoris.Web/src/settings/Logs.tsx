import { Fragment, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { figure, stamp } from '../format';
import { cn } from '../lib/cn';
import { Button, Card, Icon, Inline, MonoWell, PathText, Prose, Segmented, SelectField, SettingRow, SkeletonRows } from '../ui';

/** The process kinds that write a file (D94 §2), in the order the terminal names them. */
export const LOG_SOURCES = ['desktop', 'host', 'mcp', 'browser', 'driver'] as const;
export type LogSource = typeof LOG_SOURCES[number];

/** How far back the screen reads: spans the terminal's `--since` takes too. */
export type LogSince = '1h' | '1d' | '7d' | '30d';

/** What the domain asks for — the terminal's flags, where empty is no filter. */
export type LogFilters = {
  since: LogSince;
  source: LogSource | '';
  event: string;
  /** A floor: warnings mean warnings and errors. */
  level: '' | 'warn' | 'error';
};

/** Where the domain opens: the last day, every source, every event, every level. */
export const LOG_FILTERS: LogFilters = { since: '1d', source: '', event: '', level: '' };

/** One line, as the shell's `DAORIS.LOG` · `LINES` answers it. Its event and data are the log's own, never translated. */
export type LogRow = {
  /** UTC, as the log writes it. */
  time: string;
  source: string;
  level: string;
  event: string;
  data: Record<string, string | number | boolean | null>;
};

/** The shell's answer: the newest lines, and what the period holds beyond them. */
export type LogReading = {
  /** The home's `logs/`, or null where there is no home. */
  folder: string | null;
  /** Newest first, at most the cap. */
  lines: LogRow[];
  /** How many lines matched, of which `lines` are the newest. */
  total: number;
  /** The period's lines by level, before the level filter. */
  counts: { info: number; warn: number; error: number };
  /** The events the period holds, before the event filter. */
  events: string[];
  /** Lines that could not be read, skipped. */
  skipped: number;
};

/** The longest a value is shown on its line: a stack is the file's to hold, and a clue is the line's. */
const SHOWN = 160;

/** One value of a line's data as its line shows it. */
export type DataPart = {
  key: string;
  /** `key=value` as the terminal prints it, the value cut at the line's length. */
  text: string;
  /** How many characters the cut left out: 0 where the value is whole. */
  more: number;
};

/**
 * A line's data as `key=value` parts, the way the terminal prints them: a value that would break the line (a space, a
 * quote, an equals sign) is quoted, and a long one is cut, counting what it left out in characters, so a cut never
 * splits one (LOGVIEW1).
 */
export function dataParts(data: LogRow['data']): DataPart[] {
  return Object.entries(data).map(([key, value]) => {
    if (typeof value !== 'string') return { key, text: `${key}=${String(value)}`, more: 0 };
    const characters = Array.from(value);
    const more = Math.max(0, characters.length - SHOWN);
    const shown = more > 0 ? `${characters.slice(0, SHOWN).join('')}…` : value;
    return { key, text: /^[^\s"=]+$/.test(shown) ? `${key}=${shown}` : `${key}=${JSON.stringify(shown)}`, more };
  });
}

/** A line's data as one `key=value` line, each long value cut. */
export function dataText(data: LogRow['data']): string {
  return dataParts(data).map((part) => part.text).join(' ');
}

const EVERY = '*';

/**
 * Each line's key: its moment, process and event, and how many such came before it, since two lines of one moment are as
 * likely as not. Never its place in the list: a reading again puts newer lines above, and a line opened to read its
 * exception would close under the reader (LOGVIEW1).
 */
export function lineKeys(lines: readonly LogRow[]): string[] {
  const seen = new Map<string, number>();
  return lines.map((line) => {
    const key = `${line.time} ${line.source} ${line.event}`;
    const before = seen.get(key) ?? 0;
    seen.set(key, before + 1);
    return `${key} ${before}`;
  });
}

/** A value as its well shows it: whole, each line break one, since .NET writes an exception's frames CRLF apart. */
const whole = (value: string) => value.replace(/\r\n?/g, '\n');

/**
 * One line of the log: when, which process, how loud, the event and its data as the terminal prints them.
 *
 * @remarks
 * **A value cut short says so, and opens whole** (LOGVIEW1): beside the cut, how many characters it left out, a press
 * that opens the value beneath the line in a monospace well, its line breaks kept, so an exception reads a frame a
 * line and selects for copying. A line whose values are whole offers nothing to open. A host's exception cut at
 * Kestrel's sentence and a type's first words was read as "no type logged" for a day.
 */
function LogLine({ line, level }: { line: LogRow; level: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());
  const id = useId();
  const parts = dataParts(line.data);
  const toggle = (key: string) => setOpen((was) => {
    const next = new Set(was);
    if (!next.delete(key)) next.add(key);
    return next;
  });
  const opened = parts.filter((part) => part.more > 0 && open.has(part.key));

  return (
    <li
      aria-label={line.event}
      className="grid grid-cols-[minmax(0,10rem)_4.5rem_4.5rem_minmax(0,1fr)] items-baseline gap-x-3 border-b border-line py-1.5 text-small"
    >
      <span className="truncate tabular-nums text-ink-faint">{stamp(line.time)}</span>
      <span className="truncate font-mono text-meta text-ink-soft">{line.source}</span>
      <span className={cn(
        'truncate text-meta',
        // A failure wears an outcome's hue, as a failed tool call does, in the danger ink a red word wears
        // (UXFIX5b); the rest stay quiet.
        line.level === 'error' ? 'font-medium text-ink-danger' : line.level === 'warn' ? 'font-medium text-ink' : 'text-ink-faint',
      )}
      >
        {level}
      </span>
      <span className="min-w-0">
        <span className="font-mono text-small text-ink">{line.event}</span>
        {parts.length > 0 && (
          <span className="mt-0.5 block font-mono text-meta text-ink-soft [overflow-wrap:anywhere]">
            {parts.map((part, index) => (
              <Fragment key={part.key}>
                {index > 0 && ' '}
                {part.text}
                {part.more > 0 && (
                  <button
                    type="button"
                    aria-expanded={open.has(part.key)}
                    aria-controls={`${id}-${part.key}`}
                    onClick={() => toggle(part.key)}
                    className="ml-1.5 inline-flex cursor-pointer items-baseline gap-0.5 border-0 bg-transparent p-0 font-sans text-meta text-accent hover:underline"
                  >
                    <Icon name={open.has(part.key) ? 'chevronDown' : 'chevronRight'} size={12} className="self-center" />
                    {t('settings.logs.more', { count: part.more, shown: figure(part.more) })}
                  </button>
                )}
              </Fragment>
            ))}
          </span>
        )}
      </span>
      {opened.length > 0 && (
        <div className="col-span-full mt-1.5 grid min-w-0 gap-2 pb-1">
          {opened.map((part) => (
            <div key={part.key} id={`${id}-${part.key}`} className="min-w-0">
              <MonoWell label={<span className="font-mono">{part.key}</span>} text={whole(String(line.data[part.key]))} />
            </div>
          ))}
        </div>
      )}
    </li>
  );
}

/**
 * Settings → Logs (LOG1c, D94): what happens on this machine, read back — the recent lines newest first,
 * the terminal's filters, a count per level, and the folder they live in.
 *
 * @remarks
 * **Desktop only** (D47 §4): the log is the machine's, and a browser is offered no such domain. **Drawn
 * from props**: SettingsView holds the bridge, and the filters are applied by the shell, the same reading
 * `daoris-driver logs` prints (D50). **Nobody's words are here** because none are in the log: an event's
 * name and data are the log's own and shown as written, never translated.
 */
export function LogList({ reading, filters, busy, onFilters, onOpenFolder, onRefresh }: {
  /** Undefined while the shell is asked. */
  reading?: LogReading;
  filters: LogFilters;
  busy?: boolean;
  onFilters: (filters: LogFilters) => void;
  onOpenFolder: () => void;
  onRefresh: () => void;
}) {
  const { t } = useTranslation();
  const set = (change: Partial<LogFilters>) => onFilters({ ...filters, ...change });
  // An event chosen for an earlier period stays a choice, so the select never shows a blank.
  const events = reading?.events ?? [];
  const eventChoices = filters.event && !events.includes(filters.event) ? [filters.event, ...events] : events;
  const level = (name: string) =>
    (name === 'info' || name === 'warn' || name === 'error' ? t(`settings.logs.level.${name}`) : name);

  return (
    // No title of its own (NAME1b, UX5 U57): the card is alone in its domain, and the settings list names
    // it *Machine log*, as every card alone in its domain leaves it.
    <Card id="settings-logs" className="scroll-mt-3">
      <Prose className="text-small text-ink-soft"><Inline text={t('settings.logs.body')} /></Prose>

      {reading?.folder && (
        <div className="mt-3">
          <SettingRow
            label={t('settings.logs.folder')}
            control={(
              <>
                <PathText path={reading.folder} className="text-small text-ink-faint" />
                <Button onClick={onOpenFolder}>
                  <Icon name="folder" size={14} />
                  {t('settings.logs.open')}
                </Button>
              </>
            )}
          />
        </div>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-line pt-3">
        <Segmented<LogSince>
          label={t('settings.logs.since')}
          value={filters.since}
          options={(['1h', '1d', '7d', '30d'] as const).map((since) => ({ value: since, label: t(`settings.logs.span.${since}`) }))}
          onChange={(since) => set({ since })}
        />
        <SelectField
          ariaLabel={t('settings.logs.source')}
          value={filters.source || EVERY}
          options={[
            { value: EVERY, label: t('settings.logs.everySource') },
            ...LOG_SOURCES.map((source) => ({ value: source, label: source })),
          ]}
          onChange={(source) => set({ source: source === EVERY ? '' : (source as LogSource) })}
        />
        <SelectField
          ariaLabel={t('settings.logs.event')}
          value={filters.event || EVERY}
          options={[
            { value: EVERY, label: t('settings.logs.everyEvent') },
            ...eventChoices.map((event) => ({ value: event, label: event })),
          ]}
          onChange={(event) => set({ event: event === EVERY ? '' : event })}
        />
        <Segmented<'' | 'warn' | 'error'>
          label={t('settings.logs.levels')}
          value={filters.level}
          options={[
            { value: '', label: t('settings.logs.floor.every') },
            { value: 'warn', label: t('settings.logs.floor.warn') },
            { value: 'error', label: t('settings.logs.floor.error') },
          ]}
          onChange={(floor) => set({ level: floor })}
        />
      </div>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-2">
        <span className="text-small tabular-nums text-ink-soft">
          {reading && [
            t('settings.logs.count.info', { count: reading.counts.info, shown: figure(reading.counts.info) }),
            t('settings.logs.count.warn', { count: reading.counts.warn, shown: figure(reading.counts.warn) }),
            t('settings.logs.count.error', { count: reading.counts.error, shown: figure(reading.counts.error) }),
          ].join(' · ')}
        </span>
        <Button variant="ghost" disabled={busy} onClick={onRefresh}>
          <Icon name="refresh" size={14} />
          {t('settings.logs.refresh')}
        </Button>
      </div>

      {!reading && <div className="mt-3"><SkeletonRows rows={4} /></div>}

      {reading && reading.lines.length === 0 && <Prose className="mt-3">{t('settings.logs.none')}</Prose>}

      {reading && reading.lines.length > 0 && (
        <ol aria-label={t('settings.logs.lines')} className="m-0 mt-2 list-none border-t border-line p-0">
          {lineKeys(reading.lines).map((key, index) => (
            <LogLine key={key} line={reading.lines[index]!} level={level(reading.lines[index]!.level)} />
          ))}
        </ol>
      )}

      {reading && reading.total > reading.lines.length && (
        <Prose className="mt-2 text-small text-ink-faint">
          {t('settings.logs.partial', { shown: figure(reading.lines.length), total: figure(reading.total) })}
        </Prose>
      )}
      {reading && reading.skipped > 0 && (
        <Prose className="mt-2 text-small text-ink-faint">
          {t('settings.logs.skipped', { count: reading.skipped, shown: figure(reading.skipped) })}
        </Prose>
      )}
    </Card>
  );
}
