import { useTranslation } from 'react-i18next';
import { Button, Inline, PathText, Pill, Prose } from '../ui';
import { type KitPoint, type PluginTrialResult, TrialReport } from '../settings/PluginKit';
import { PluginSourceLine, PluginUpdatePlan, type PluginUpdatePlanShown, updatable } from '../settings/PluginUpdate';
import { PageHead, PageSection, ViewMain } from '../work/ViewMain';
import { type PluginShown, pluginState, speaks } from './catalog';
import { STATE_WORD } from './PluginList';

// The main area's page header and its sections are every page's since FRAME1d (`work/ViewMain`).
export { PageHead, PageSection };

/** A warn-railed sentence a plugin's page leads with: the driver's own, verbatim, since it is content (D119 §3.2). */
export function Lead({ text }: { text: string }) {
  return (
    <p className="m-0 mb-4 max-w-prose border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
      <Inline text={text} />
    </p>
  );
}

/** Each declared point, in the mono face, with what kind of question it is in the kit's words (D119 §3.2). */
export function PointRows({ points, kitPoints }: { points: string[]; kitPoints?: KitPoint[] | null }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
      {points.map((point) => {
        const kind = kitPoints?.find((known) => known.name === point)?.kind;
        return (
          <li key={point} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-small">
            <code className="font-mono text-meta text-ink">{point}</code>
            {kind && <span className="text-ink-soft">{t(`plugin.kit.kind.${kind}`, { defaultValue: kind })}</span>}
          </li>
        );
      })}
    </ul>
  );
}

/** Names in the mono face, one a line: the agents a plugin declares, the servers an offer hands. */
export function NameRows({ names }: { names: string[] }) {
  return (
    <ul className="m-0 flex list-none flex-col gap-1 p-0">
      {names.map((name) => <li key={name}><code className="font-mono text-meta text-ink">{name}</code></li>)}
    </ul>
  );
}

/**
 * **A plugin's page** (D119 §3.2) on today's answers (PLUGUI1b): a header with its switch, *Try*, *Update…* and
 * *Remove…*, then Points, Agents, Tests, Data folder and Source, and its terminal twins at its foot.
 *
 * @remarks
 * **A molecule**: every state is reached by its props, the ask and the plan included, and every press goes out.
 *
 * - **An absent act is absent, never disabled.** *Try* only for a plugin that speaks at a point, *Update…* only for one
 *   with a source record. None is loud: the plan's *Update now* is the page's one primary control (PLUGUI1c makes the
 *   plan, which moves here then, wear it).
 * - ***Remove…* asks once** (PLUG10 P8, D41 §4): its first press opens a sentence under the header saying what the
 *   second does and where what the plugin kept stays, beside *Remove plugin* and *Never mind*.
 * - **A refused plugin's page leads with the driver's sentence**, verbatim; it is taken nowhere, so it has nothing to try.
 * - **Health's lines, Servers and Activity wait on the host's answers** (PLUGUI1d–f): today's `PLUGINS` says none of them.
 */
export function PluginPage({
  plugin, kitPoints, canTry = false, trial, trying = false, plan, updating = false, acting = false, asking = false,
  onSwitch, onTry, onAskUpdate, onApplyUpdate, onCancelUpdate, onAskRemove, onRemove, onCancelRemove,
}: {
  plugin: PluginShown;
  /** The kit's points with their kinds, which name what each point asks; an older shell sends none. */
  kitPoints?: KitPoint[] | null;
  /** This shell has the kit, so a plugin can be tried; an older one offers no *Try*. */
  canTry?: boolean;
  /** Its last trial on this page, held while Daoris is open (PLUGUI1g keeps it on the machine). */
  trial?: PluginTrialResult | null;
  /** A trial of it is running. */
  trying?: boolean;
  /** What an update would change, shown under the header after *Update…*'s first press. */
  plan?: PluginUpdatePlanShown | null;
  updating?: boolean;
  /** A switch or a removal is on its way. */
  acting?: boolean;
  /** *Remove…*'s first press was made, and the page asks. */
  asking?: boolean;
  onSwitch: (id: string, action: 'enable' | 'disable') => void;
  onTry: (id: string) => void;
  onAskUpdate: (id: string) => void;
  onApplyUpdate: (id: string) => void;
  onCancelUpdate: () => void;
  onAskRemove: (id: string) => void;
  onRemove: (id: string) => void;
  onCancelRemove: () => void;
}) {
  const { t } = useTranslation();
  const word = STATE_WORD[pluginState(plugin)];
  const tryable = canTry && speaks(plugin);

  const acts = (
    <>
      <Button disabled={acting} onClick={() => onSwitch(plugin.id, plugin.enabled ? 'disable' : 'enable')}>
        {t(plugin.enabled ? 'plugin.disable' : 'plugin.enable')}
      </Button>
      {tryable && (
        <Button disabled={trying} aria-busy={trying || undefined} aria-label={t('plugin.kit.tryNamed', { id: plugin.id })} onClick={() => onTry(plugin.id)}>
          {t('plugin.kit.try')}
        </Button>
      )}
      {updatable(plugin.source) && (
        <Button disabled={updating} aria-label={t('plugin.update.askNamed', { id: plugin.id })} onClick={() => onAskUpdate(plugin.id)}>
          {t('plugin.update.ask')}
        </Button>
      )}
      {/* While it asks, the ask under the header holds the move, so the first press is not offered twice. */}
      {!asking && <Button variant="danger" disabled={acting} onClick={() => onAskRemove(plugin.id)}>{t('plugin.forget')}</Button>}
    </>
  );

  const head = (
    <PageHead
      title={plugin.name || plugin.id}
      version={plugin.version}
      pills={word && <Pill tone={word.tone}>{t(word.key)}</Pill>}
      id={plugin.id}
      line={plugin.description}
      acts={acts}
    />
  );

  return (
    <ViewMain header={head}>
      {/* Status leads (platform language §4): a refused plugin's sentence comes first. */}
      {plugin.problem && <Lead text={plugin.problem} />}

      {asking && (
        <div
          role="group"
          aria-label={t('plugin.removeTitle', { id: plugin.id })}
          className="mb-4 flex flex-wrap items-center gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
        >
          <span className="min-w-0 flex-1 basis-64 text-small text-ink-soft">
            <Inline text={t('plugin.removeConfirm', { data: plugin.data })} />
          </span>
          <Button variant="danger" disabled={acting} onClick={() => onRemove(plugin.id)}>{t('plugin.removeMeanIt')}</Button>
          <Button variant="ghost" onClick={onCancelRemove}>{t('common.cancel')}</Button>
        </div>
      )}

      {plan && (
        <div className="mb-4">
          <PluginUpdatePlan plan={plan} busy={updating} onApply={onApplyUpdate} onCancel={onCancelUpdate} />
        </div>
      )}

      {plugin.points.length > 0 && (
        <PageSection title={t('plugin.section.points')}>
          <PointRows points={plugin.points} kitPoints={kitPoints} />
        </PageSection>
      )}

      {plugin.harnesses.length > 0 && (
        <PageSection title={t('plugin.section.agents')}>
          <NameRows names={plugin.harnesses} />
        </PageSection>
      )}

      <PageSection title={t('plugin.section.tests')}>
        <h3 className="m-0 mb-1.5 text-small font-medium text-ink-soft">{t('plugin.tests.trial')}</h3>
        {trying ? (
          <Prose className="text-small"><Inline text={t('plugin.kit.working')} /></Prose>
        ) : trial ? (
          <TrialReport trial={trial} />
        ) : !speaks(plugin) ? (
          <Prose className="text-small">{t('plugin.tests.nothing')}</Prose>
        ) : (
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
            <Prose className="text-small"><Inline text={t('plugin.tests.none')} /></Prose>
            {tryable && <Button variant="ghost" onClick={() => onTry(plugin.id)}>{t('plugin.kit.try')}</Button>}
          </div>
        )}
      </PageSection>

      <PageSection title={t('plugin.section.data')}>
        <PathText path={plugin.data} className="text-small text-ink-soft" />
        <Prose className="mt-1 text-small">{t('plugin.data.body')}</Prose>
      </PageSection>

      <PageSection title={t('plugin.section.source')}>
        <div className="flex flex-col gap-1 text-small text-ink-soft">
          <PluginSourceLine source={plugin.source ?? { kind: 'none' }} />
          <span className="flex flex-wrap items-baseline gap-x-1.5">
            <span className="text-ink-faint">{t('plugin.field.folder')}</span>
            <PathText path={plugin.folder} className="text-small" />
          </span>
        </div>
      </PageSection>

      {/* The two doors stay in sight (D50): only the terminal's verbs that exist. */}
      <div className="mt-8 flex flex-col gap-1 border-t border-line pt-3 text-meta text-ink-faint">
        <p className="m-0"><Inline text={t('plugin.page.terminal', { id: plugin.id })} /></p>
        {speaks(plugin) && <p className="m-0"><Inline text={t('plugin.page.terminalTry', { id: plugin.id })} /></p>}
      </div>
    </ViewMain>
  );
}

/** What the main area shows without a page (D118 §3b, D119 §3.2). */
export type MainNotice = 'none' | 'gone' | 'loading' | 'unanswered';

/**
 * The plugins' main area with no page to show: **nothing chosen** says how to choose and offers the list's `＋` kinds;
 * **gone** says the chosen plugin is no longer here; **loading** is skeleton rows, never the empty state; and a list that
 * has never had an answer says its sentence in place.
 */
export function PluginMainNotice({ state, actions = [], sentence, onAct }: {
  state: MainNotice;
  /** The list's `＋` kinds, offered with nothing chosen. */
  actions?: { id: string; label: string }[];
  sentence?: string;
  onAct?: (id: string) => void;
}) {
  const { t } = useTranslation();
  if (state === 'unanswered') {
    return <ViewMain><Prose><Inline text={sentence ?? ''} /></Prose></ViewMain>;
  }
  return (
    <ViewMain
      state={state}
      none={{
        icon: 'plug',
        headline: t('plugin.none.headline'),
        body: t('plugin.none.body'),
        action: actions.length > 0 && (
          <div className="flex flex-wrap justify-center gap-2">
            {actions.map((action) => <Button key={action.id} onClick={() => onAct?.(action.id)}>{action.label}</Button>)}
          </div>
        ),
      }}
      gone={{ icon: 'plug', headline: t('plugin.gone.headline'), body: t('plugin.gone.body') }}
    />
  );
}
