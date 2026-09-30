import { useTranslation } from 'react-i18next';
import { figure, stamp } from '../format';
import { cn } from '../lib/cn';
import { Button, Card, Icon, PathText, Prose, Segmented, SelectField, SettingRow, SkeletonRows } from '../ui';

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

/** The longest a value is shown: a stack is the file's to hold, and a clue is the screen's. */
const SHOWN = 160;

/**
 * A line's data as `key=value`, the way the terminal prints it: a value that would break the line (a
 * space, a quote, an equals sign) is quoted, and a long one is cut.
 */
export function dataText(data: LogRow['data']): string {
  return Object.entries(data).map(([key, value]) => {
    if (typeof value !== 'string') return `${key}=${String(value)}`;
    const shown = value.length > SHOWN ? `${value.slice(0, SHOWN)}…` : value;
    return /^[^\s"=]+$/.test(shown) ? `${key}=${shown}` : `${key}=${JSON.stringify(shown)}`;
  }).join(' ');
}

const EVERY = '*';

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
      <Prose className="text-small text-ink-soft">{t('settings.logs.body')}</Prose>

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
          {reading.lines.map((line, index) => (
            <li
              // Two lines of one moment are as likely as not: the index keeps them apart.
              key={`${line.time}-${index}`}
              aria-label={line.event}
              className="grid grid-cols-[minmax(0,10rem)_4.5rem_4.5rem_minmax(0,1fr)] items-baseline gap-x-3 border-b border-line py-1.5 text-small"
            >
              <span className="truncate tabular-nums text-ink-faint">{stamp(line.time)}</span>
              <span className="truncate font-mono text-meta text-ink-soft">{line.source}</span>
              <span className={cn(
                'truncate text-meta',
                // A failure wears an outcome's hue, as a failed tool call does; the rest stay quiet.
                line.level === 'error' ? 'font-medium text-st-declined' : line.level === 'warn' ? 'font-medium text-ink' : 'text-ink-faint',
              )}
              >
                {level(line.level)}
              </span>
              <span className="min-w-0">
                <span className="font-mono text-small text-ink">{line.event}</span>
                {Object.keys(line.data).length > 0 && (
                  <span className="mt-0.5 block font-mono text-meta text-ink-soft [overflow-wrap:anywhere]">{dataText(line.data)}</span>
                )}
              </span>
            </li>
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
