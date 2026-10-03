import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ago, size } from '../format';
import {
  Button, Card, CardHeader, Inline, PathText, Pill, Prose, Segmented, SelectField, SettingRow,
} from '../ui';

/** How a tool is run (D121 §2.2): the system's, from PATH; a version Daoris keeps; or a file the person names. */
export type ToolWay = 'system' | 'managed' | 'file';

/**
 * One tool as the shell's `TOOLS_LIST` answers it (TOOLS7, D121 §4.1): the fields its card draws. The bridge reads the
 * same shape; a molecule names its props here rather than reach the bridge. The bridge leaves a null out, so every
 * field that may be null may also be absent.
 */
export type ToolShown = {
  tool: string;
  /** Its product's name, Latin in both languages (D116 §3b). */
  name: string;
  /** The way set; absent when the file does not read, and the tool is refused. */
  way?: ToolWay | null;
  /** The managed version set. */
  version?: string | null;
  /** The named file set. */
  file?: string | null;
  /** The file that starts, or absent: a refusal, or a system tool PATH does not find. */
  resolved?: string | null;
  /** The way set cannot run, and it never falls back to PATH. */
  refused: boolean;
  /** Why none starts, in the driver's own sentence. */
  problem?: string | null;
  /** What the system's way would run, found on PATH. */
  system?: string | null;
  /** The version the resolved file answered, once asked. */
  asked?: string | null;
  askedProblem?: string | null;
  downloaded: { version: string; size?: number | null; inUse: boolean }[];
  /** What the lists offer for this machine, newest first. */
  offered: { version: string; size: number; archive: string; lists: string[]; hosts: string[]; downloaded: boolean }[];
  /** Versions two lists disagree on, and so refused. */
  refusedVersions: { version: string; field: string; lists: string[]; problem: string }[];
  newest?: string | null;
  source?: string | null;
  licence?: { id: string; url?: string | null } | null;
  /** What runs for it now: a download or a use, and its version. */
  running?: { action: string; version?: string | null } | null;
};

/** One resource list as the screen shows it (§3.3): what vouches for it, when it was fetched, what it names here. */
export type ToolListShown = {
  /** The person's address; absent for the list built in. */
  address?: string | null;
  /** `built in`, `this machine`, or the host the person chose to trust (§3.5). */
  integrity?: string | null;
  exists: boolean;
  /** When its copy was fetched, for a location. */
  fetched?: string | null;
  sha256?: string | null;
  /** `<tool> <version>` it names for this machine. */
  names: string[];
  problem?: string | null;
  notes: string[];
};

export type ToolsShown = {
  /** `tools.json`, the file both doors edit. */
  file: string;
  exists: boolean;
  /** Why the file does not read; every tool is then refused. */
  problem?: string | null;
  tools: ToolShown[];
  locations: ToolListShown[];
  builtIn: ToolListShown;
};

/** One location looked at (§3.7). The sentence is the driver's. */
export type ToolLookShown = {
  address: string;
  outcome: 'fetched' | 'unread' | 'failed';
  sentence: string;
  added: string[];
  dropped: string[];
};

/** How a tool's action ended, as the `TOOLS_ENDED` news says it (§3.6). */
export type ToolEndShown = {
  tool: string;
  name: string;
  action: string;
  version?: string | null;
  exitCode: number;
  check?: string | null;
  problem?: string | null;
  stopped: boolean;
  nothing?: string | null;
};

/** What a switch of git changes (§4.1): the checkout keys the git that runs now and the one it would run read differently. */
export type GitSwitchShown = {
  now: { file?: string | null; problem?: string | null };
  then: { file?: string | null; problem?: string | null };
  keys: { key: string; now?: string | null; then?: string | null }[];
  same: boolean;
};

/** A press that sets a tool's way. */
export type ToolUse = { action: ToolWay; version?: string; file?: string };

/** A way the person is choosing on a card, before any press applies it. */
export type ToolChoice = { way: ToolWay; version: string; file: string };

/** A switch of git asked about before it applies: the press, and what the two gits answered. */
export type GitAsking = { use: ToolUse; reading: boolean; answer?: GitSwitchShown; problem?: string };

/** The list built in, as the driver names it in a sentence (`ToolResources.BuiltIn`). */
const BUILT_IN = 'the built-in list';

/** The choice a card opens on: the way set, at the version or the file set, else the first a person would pick. */
export function choiceOf(tool: ToolShown): ToolChoice {
  return {
    way: tool.way ?? 'system',
    version: tool.version ?? tool.downloaded[0]?.version ?? tool.newest ?? tool.offered[0]?.version ?? '',
    file: tool.file ?? '',
  };
}

/** Whether a choice differs from the way set, so a press would change what runs. */
export function changes(tool: ToolShown, choice: ToolChoice): boolean {
  if (choice.way !== (tool.way ?? 'system')) return true;
  if (choice.way === 'managed') return choice.version !== (tool.version ?? '');
  if (choice.way === 'file') return choice.file.trim() !== (tool.file ?? '');
  return false;
}

/**
 * One tool's card in Settings → Tools (TOOLS7, D121 §4.1): what Daoris needs it for, how it is run as one choice of
 * three, the file it runs or why none, and its version. *Managed* adds the versions downloaded and those the lists name
 * for this machine, with *Download*, *Use this version* and *Delete*; *Custom* adds the file with *Browse…*. Props in,
 * presses out: the domain holds the list, the presses and the choice, and the running action's console comes in as
 * `console`.
 *
 * @remarks
 * **Nothing applies on choosing.** A segment shows a way's controls; a press applies it. So a switch of git is said
 * before it applies (§4.1): for git, a press asks first (`onAsk`), and `asking` carries the two gits' answers, with the
 * switch confirmed beside *Never mind*. A version of git not yet downloaded is downloaded first, since what it changes
 * is asked of it.
 *
 * *Delete* asks once (D41 §4): its first press opens a sentence saying what the second does.
 */
export function ToolCard({
  tool, choice, busy = false, asking, console, onChoice, onUse, onAsk, onDownload, onStop, onDelete, onBrowse,
}: {
  tool: ToolShown;
  choice: ToolChoice;
  /** A press of this card on its way. */
  busy?: boolean;
  /** Git's switch asked about, while it is. */
  asking?: GitAsking | null;
  /** The running action's console, which an organism holds. */
  console?: ReactNode;
  onChoice: (choice: ToolChoice) => void;
  onUse: (use: ToolUse) => void;
  /** Git's: ask what a switch changes before it applies; null puts the question away. Absent applies at once. */
  onAsk?: (use: ToolUse | null) => void;
  onDownload: (version: string) => void;
  onStop: () => void;
  onDelete: (version: string) => void;
  /** The system's file picker, where this window has one. */
  onBrowse?: () => void;
}) {
  const { t } = useTranslation();
  const [deleting, setDeleting] = useState<string | null>(null);
  const running = tool.running ?? null;
  const acting = busy || running !== null;
  const saved = tool.way ?? 'system';
  const press = (use: ToolUse) => (onAsk ? onAsk(use) : onUse(use));

  const chosenHere = tool.downloaded.find((each) => each.version === choice.version);
  const chosenOffered = tool.offered.find((each) => each.version === choice.version);
  const versions = [
    ...tool.downloaded.map((each) => ({
      value: each.version,
      label: t(each.inUse ? 'settings.tools.option.inUse' : 'settings.tools.option.downloaded', { version: each.version }),
    })),
    ...tool.offered.filter((each) => !each.downloaded).map((each) => ({
      value: each.version,
      label: t('settings.tools.option.offered', { version: each.version, size: size(each.size) }),
    })),
  ];
  const listName = (list: string) => (list === BUILT_IN ? t('settings.tools.locations.builtInName') : list);
  const changing = changes(tool, choice);
  // The system's way names what it would run before the press, and what it runs once it is set.
  const systemPath = changing ? tool.system : tool.resolved;
  const versionSaid = tool.asked
    ? t('settings.tools.answers', { version: tool.asked })
    : tool.askedProblem ?? undefined;

  const state = running
    ? <Pill tone="taken">{t('settings.tools.status.downloading')}</Pill>
    : tool.refused || (saved === 'system' && !tool.resolved)
      ? <Pill tone={tool.refused ? 'declined' : 'neutral'}>{t('settings.tools.status.notFound')}</Pill>
      : <Pill>{saved === 'managed'
        ? t('settings.tools.status.managed', { version: tool.version })
        : t(`settings.tools.status.${saved}`)}</Pill>;

  return (
    <Card id={`settings-tool-${tool.tool}`} className="mt-3.5 scroll-mt-3">
      <CardHeader title={tool.name} aside={state} />
      <Prose className="mt-1 text-small">{t(`settings.tools.why.${tool.tool}`)}</Prose>

      {/* The way set cannot run, in the driver's own words: it never falls back to PATH (D121 §2.3). */}
      {tool.refused && tool.problem && (
        <p className="mt-2 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
          <Inline text={tool.problem} />
        </p>
      )}

      <div className="mt-2">
        <SettingRow
          label={t('settings.tools.program')}
          hint={`\`daoris tool use ${tool.tool} system|managed|file\``}
          control={(
            <Segmented
              label={t('settings.tools.programOf', { name: tool.name })}
              value={choice.way}
              options={[
                { value: 'system', label: t('settings.tools.use.system') },
                { value: 'managed', label: t('settings.tools.use.managed') },
                { value: 'file', label: t('settings.tools.use.file') },
              ]}
              onChange={(way) => { onAsk?.(null); onChoice({ ...choice, way }); }}
            />
          )}
        />

        {choice.way === 'system' && (
          <SettingRow
            label={t('settings.tools.file')}
            hint={!systemPath ? t('settings.tools.systemNone', { name: tool.name }) : changing ? undefined : versionSaid}
            control={systemPath
              ? <PathText path={systemPath} className="text-small text-ink-faint" />
              : <span className="text-small text-ink-faint">{t('settings.tools.status.notFound')}</span>}
          >
            {changing && !asking && (
              <div className="flex justify-end">
                <Button variant="primary" disabled={acting} onClick={() => press({ action: 'system' })}>
                  {t('settings.tools.useSystem')}
                </Button>
              </div>
            )}
          </SettingRow>
        )}

        {choice.way === 'file' && (
          <SettingRow
            label={t('settings.tools.file')}
            hint={!changing && versionSaid ? versionSaid : t('settings.tools.fileHint')}
          >
            <form
              className="flex min-w-0 flex-wrap items-center gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                const file = choice.file.trim();
                if (file) press({ action: 'file', file });
              }}
            >
              <input
                aria-label={t('settings.tools.fileOf', { name: tool.name })}
                value={choice.file}
                onChange={(event) => { onAsk?.(null); onChoice({ ...choice, file: event.target.value }); }}
                placeholder={t('settings.tools.filePlaceholder')}
                spellCheck={false}
                className="min-w-0 flex-1 basis-64 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
              />
              {onBrowse && <Button variant="ghost" disabled={acting} onClick={onBrowse}>{t('settings.tools.browse')}</Button>}
              {changing && !asking && (
                <Button variant="primary" type="submit" disabled={acting || !choice.file.trim()}>
                  {t('settings.tools.useFile')}
                </Button>
              )}
            </form>
          </SettingRow>
        )}

        {choice.way === 'managed' && (
          <SettingRow
            label={t('settings.tools.version')}
            hint={tool.licence || tool.source
              ? t('settings.tools.licence', {
                licence: tool.licence?.id ?? t('settings.tools.licenceUnknown'),
                hosts: (chosenOffered?.hosts ?? []).join(', ') || tool.source || '',
              })
              : undefined}
            control={versions.length > 0 && (
              <SelectField
                ariaLabel={t('settings.tools.versionOf', { name: tool.name })}
                value={choice.version}
                onChange={(version) => { onAsk?.(null); setDeleting(null); onChoice({ ...choice, version }); }}
                options={versions}
                disabled={acting}
              />
            )}
          >
            {versions.length === 0
              ? <Prose className="text-small"><Inline text={t('settings.tools.noVersions', { name: tool.name })} /></Prose>
              : (
                <div className="flex flex-wrap items-center justify-end gap-2">
                  {/* Which lists name the version chosen (§3.7): the person's first, then the one built in. */}
                  {chosenOffered && (
                    <span className="mr-auto text-small text-ink-faint">
                      {t('settings.tools.namedBy', { lists: chosenOffered.lists.map(listName).join(', ') })}
                    </span>
                  )}
                  {!chosenHere && choice.version && (
                    <Button disabled={acting} onClick={() => onDownload(choice.version)}>{t('settings.tools.download')}</Button>
                  )}
                  {/* Git's switch is asked of the git it would run, so its version is downloaded first. */}
                  {changing && choice.version && !asking && (!onAsk || chosenHere) && (
                    <Button
                      variant="primary"
                      disabled={acting}
                      onClick={() => press({ action: 'managed', version: choice.version })}
                    >
                      {t('settings.tools.useVersion')}
                    </Button>
                  )}
                  {chosenHere && !chosenHere.inUse && deleting === null && (
                    <Button variant="ghost" disabled={acting} onClick={() => setDeleting(chosenHere.version)}>
                      {t('settings.tools.delete')}
                    </Button>
                  )}
                </div>
              )}
            {onAsk && !chosenHere && choice.version && changing && (
              <Prose className="mt-1.5 text-small">{t('settings.tools.gitFirst')}</Prose>
            )}
            {tool.refusedVersions.length > 0 && (
              <Prose className="mt-1.5 text-small">
                {t('settings.tools.refusedVersions', { versions: tool.refusedVersions.map((each) => each.version).join(', ') })}
              </Prose>
            )}
            {deleting !== null && (
              <div
                role="group"
                aria-label={t('settings.tools.deleteTitle', { name: tool.name, version: deleting })}
                className="mt-2 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
              >
                <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">
                  {t('settings.tools.deleteSays', { name: tool.name, version: deleting })}
                </span>
                <Button
                  variant="danger"
                  disabled={acting}
                  onClick={() => {
                    const version = deleting;
                    setDeleting(null);
                    onDelete(version);
                  }}
                >
                  {t('settings.tools.deleteConfirm')}
                </Button>
                <Button variant="ghost" onClick={() => setDeleting(null)}>{t('common.cancel')}</Button>
              </div>
            )}
          </SettingRow>
        )}

        {/* The managed version in use: the file it runs, and the version that file answers. */}
        {choice.way === 'managed' && !changing && tool.resolved && (
          <SettingRow
            label={t('settings.tools.file')}
            hint={versionSaid}
            control={<PathText path={tool.resolved} className="text-small text-ink-faint" />}
          />
        )}

        {asking && <GitSwitch asking={asking} busy={acting} onConfirm={() => onUse(asking.use)} onCancel={() => onAsk?.(null)} />}

        {running && (
          <div className="mt-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="min-w-0 flex-1 text-small text-ink-soft">
                {t('settings.tools.running', { name: tool.name, version: running.version ?? '' })}
              </span>
              <Button variant="danger" onClick={onStop}>{t('settings.tools.stop')}</Button>
            </div>
            {console}
          </div>
        )}
      </div>
    </Card>
  );
}

/**
 * What a switch of git changes, said before it applies (§4.1): the checkout keys the two gits read differently, each
 * as it is now and as it would be, and the switch confirmed beside *Never mind*. Line endings that differ between the
 * person's git and Daoris's in one checkout would show every file changed.
 */
export function GitSwitch({ asking, busy = false, onConfirm, onCancel }: {
  asking: GitAsking;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const answer = asking.answer;
  const unset = t('settings.tools.switch.unset');
  const problem = asking.problem ?? answer?.now.problem ?? answer?.then.problem ?? null;

  return (
    <div
      role="group"
      aria-label={t('settings.tools.switch.title')}
      className="mt-2 rounded-control border border-line bg-sunken px-3 py-2.5"
    >
      <p className="m-0 text-small font-semibold text-ink">{t('settings.tools.switch.title')}</p>
      {asking.reading && <p className="m-0 mt-1 text-small text-ink-faint">{t('settings.tools.switch.reading')}</p>}
      {!asking.reading && problem && (
        <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('settings.tools.switch.unknown', { problem })} /></p>
      )}
      {!asking.reading && !problem && answer && (answer.same || answer.keys.length === 0) && (
        <p className="m-0 mt-1 text-small text-ink-soft">{t('settings.tools.switch.same')}</p>
      )}
      {!asking.reading && !problem && answer && answer.keys.length > 0 && (
        <>
          <p className="m-0 mt-1 text-small text-ink-soft">{t('settings.tools.switch.differs')}</p>
          <ul className="m-0 mt-1.5 list-none p-0">
            {answer.keys.map((key) => (
              <li key={key.key} className="text-small text-ink">
                <code className="font-mono">{key.key}</code>
                {' '}
                {t('settings.tools.switch.key', { now: key.now ?? unset, then: key.then ?? unset })}
              </li>
            ))}
          </ul>
        </>
      )}
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <Button variant="primary" disabled={busy || asking.reading} onClick={onConfirm}>{t('settings.tools.switch.confirm')}</Button>
        <Button variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}

/**
 * The resource locations (§3.3): the person's in order, each with what vouches for it, when it was fetched and what it
 * names here, then the list built in, which cannot be removed; *Look for updates* for all of them, and *Add location…*.
 * Removing one narrows what is offered, and a version already downloaded stays.
 */
export function ToolLocations({
  locations, builtIn, looks, looking = false, busy = false, onLook, onAdd, onRemove,
}: {
  locations: ToolListShown[];
  builtIn: ToolListShown;
  /** The last look's answers, by address. */
  looks?: ToolLookShown[] | null;
  looking?: boolean;
  busy?: boolean;
  onLook: () => void;
  onAdd: (address: string) => void;
  onRemove: (address: string) => void;
}) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [address, setAddress] = useState('');

  const said = (list: ToolListShown) => {
    if (!list.exists) {
      return list.address ? t('settings.tools.locations.never') : t('settings.tools.locations.noBuiltIn');
    }
    const parts = [
      list.integrity === 'built in' ? t('settings.tools.status.builtIn')
        : list.integrity === 'this machine' ? t('settings.tools.locations.thisMachine')
          : list.integrity ?? '',
      list.fetched ? t('settings.tools.locations.fetched', { ago: ago(list.fetched) }) : null,
      t('settings.tools.locations.names', { count: list.names.length }),
    ];
    return parts.filter(Boolean).join(' · ');
  };

  const row = (list: ToolListShown, key: string) => {
    const look = list.address ? looks?.find((each) => each.address === list.address) : undefined;
    return (
      <SettingRow
        key={key}
        label={list.address
          ? <span className="break-all font-mono text-small">{list.address}</span>
          : t('settings.tools.locations.builtIn')}
        hint={said(list)}
        control={list.address && (
          <Button variant="ghost" disabled={busy} onClick={() => onRemove(list.address!)}>
            {t('settings.tools.locations.remove')}
          </Button>
        )}
      >
        {look && (
          <p className="m-0 flex flex-wrap items-center gap-2 text-small text-ink-soft">
            <Pill tone={look.outcome === 'fetched' ? 'done' : 'declined'}>{t(`settings.tools.look.${look.outcome}`)}</Pill>
            {look.added.length > 0 && <span>{t('settings.tools.look.added', { names: look.added.join(', ') })}</span>}
            {look.dropped.length > 0 && <span>{t('settings.tools.look.dropped', { names: look.dropped.join(', ') })}</span>}
          </p>
        )}
        {/* What could not be fetched or read, in the driver's own words: the address and the reason are the person's to act on. */}
        {look && look.outcome !== 'fetched' && (
          <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={look.sentence} /></p>
        )}
        {list.problem && (
          <p className="m-0 mt-1 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-small text-ink-soft">
            <Inline text={list.problem} />
          </p>
        )}
      </SettingRow>
    );
  };

  return (
    <Card id="settings-tool-locations" className="mt-3.5 scroll-mt-3">
      <CardHeader
        title={t('settings.tools.locations.title')}
        // A look fetches the person's locations; the list built in is the install's, so with none there is nothing
        // to look at, and the sentence below says what adds one.
        aside={locations.length > 0 && (
          <Button disabled={busy || looking} onClick={onLook}>
            {t(looking ? 'settings.tools.looking' : 'settings.tools.look')}
          </Button>
        )}
      />
      <Prose className="mt-1 text-small">
        <Inline text={t(locations.length > 0 ? 'settings.tools.locations.hint' : 'settings.tools.locations.none')} />
      </Prose>
      <div className="mt-2">
        {locations.map((list, at) => row(list, `${at}:${list.address}`))}
        {row(builtIn, 'built-in')}
      </div>
      {adding
        ? (
          <form
            className="mt-2 flex flex-wrap items-center gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              const typed = address.trim();
              if (!typed) return;
              onAdd(typed);
              setAddress('');
              setAdding(false);
            }}
          >
            <input
              autoFocus
              aria-label={t('settings.tools.locations.address')}
              value={address}
              onChange={(event) => setAddress(event.target.value)}
              placeholder={t('settings.tools.locations.placeholder')}
              spellCheck={false}
              className="min-w-0 flex-1 basis-64 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink outline-none placeholder:text-ink-faint"
            />
            <Button type="submit" disabled={busy || !address.trim()}>{t('settings.tools.locations.save')}</Button>
            <Button variant="ghost" onClick={() => { setAddress(''); setAdding(false); }}>{t('common.cancel')}</Button>
          </form>
        )
        : (
          <div className="mt-2">
            <Button variant="ghost" disabled={busy} onClick={() => setAdding(true)}>{t('settings.tools.locations.add')}</Button>
          </div>
        )}
    </Card>
  );
}

/** What a tool's action's end means to the person, said once (§3.6): downloaded, switched, stopped, or refused and why. */
export function endSaid(t: (key: string, values?: Record<string, unknown>) => string, end: ToolEndShown): string {
  const named = { name: end.name, version: end.version ?? '' };
  if (end.stopped) return t('settings.tools.ended.stopped', named);
  if (end.nothing) return t('settings.tools.ended.nothing', { name: end.name, nothing: end.nothing });
  if (end.exitCode !== 0) {
    // A check refused it, named by its code, the typed half; with none, the tool itself failed. The driver's sentence
    // stands as it wrote it either way.
    return end.check
      ? t('settings.tools.ended.refused', { ...named, problem: end.problem ?? '', check: end.check })
      : t('settings.tools.ended.failed', { ...named, problem: end.problem ?? '' });
  }
  return t(end.action === 'download' ? 'settings.tools.ended.downloaded' : 'settings.tools.ended.used', named);
}
