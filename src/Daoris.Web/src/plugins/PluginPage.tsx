import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Inline, PathText, Pill, Prose } from '../design/ui';
import { type KitPoint, type PluginTrialResult, pointGloss, TrialReport } from './PluginKit';
import { PluginSourceLine, PluginUpdatePlan, type PluginUpdatePlanShown, updatable } from './PluginUpdate';
import { type Answered, InlineConfirm } from '../work/InlineConfirm';
import { PageHead, PageSection, ViewMain } from '../work/ViewMain';
import { type HelpServerShown, type PluginShown, pluginState, type SkillShown, speaks, updateWaits } from './catalog';
import { PluginIcon } from './PluginIcon';
import { STATE_WORD } from './PluginList';

// The main area's page header and its sections are every page's since FRAME1d (`work/ViewMain`).
export { PageHead, PageSection };

/**
 * A warn-railed sentence a plugin's page leads with: the driver's own, verbatim, since it is content (D119 §3.2). Like
 * every block of a page it takes the column's width (D140 §2, D141).
 */
export function Lead({ text }: { text: string }) {
  return (
    <p className="m-0 mb-4 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
      <Inline text={text} />
    </p>
  );
}

/**
 * A paragraph of the detail, in the small face, at the column's width: no paragraph keeps a measure of its own (D140 §2,
 * from the install: a 65ch paragraph beside a full-width line wrapped one page at two edges; D141 for every page).
 */
export function Body({ className, children }: { className?: string; children: ReactNode }) {
  return <p className={cn('m-0 text-small text-ink-soft', className)}>{children}</p>;
}

/** Each declared point, in the mono face, with what kind of question it is in the kit's words (D119 §3.2). */
export function PointRows({ points, kitPoints }: { points: string[]; kitPoints?: KitPoint[] | null }) {
  const { t } = useTranslation();
  return (
    <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
      {points.map((point) => {
        const known = kitPoints?.find((each) => each.name === point);
        return (
          <li key={point} className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5 text-small">
            <code className="font-mono text-meta text-ink">{point}</code>
            {known?.kind && <span className="text-ink-soft">{pointGloss(t, known)}</span>}
          </li>
        );
      })}
    </ul>
  );
}

/** The person's presses on what a plugin's server offers Ask Daoris (HELPPLUG1a's tightening, D170's note). */
export type HelpPress = 'help-approve' | 'help-reads' | 'help-withdraw';

/**
 * **What a plugin offers Ask Daoris** (HELPPLUG1a, D170 point 3 and its note): a line saying plainly that a plugin is code that
 * runs as the person, then each server in the mono face with the person's approval as it stands, what it reads with, what it
 * writes outside Daoris with and which of those writes they accepted, the driver's sentence for one refused, and the presses:
 * *Approve* (its exact tools, writes included) and *Approve its reads only* while it waits, *Withdraw* once approved. None for
 * a refused one, and none on an offer's page, which nothing here has installed. Nothing where it offers nothing.
 */
export function HelpServers({ plugin, servers, onHelp }: {
  /** The plugin's id, which a press names; absent on an offer's page. */
  plugin?: string;
  servers?: HelpServerShown[];
  onHelp?: (id: string, server: string, action: HelpPress) => void;
}) {
  const { t } = useTranslation();
  if (!servers?.length) return null;
  return (
    <PageSection title={t('plugin.section.help')}>
      <Body className="mb-2">{t('plugin.help.trusted')}</Body>
      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {servers.map((server) => {
          const writes = server.writes ?? [];
          const accepted = writes.filter((tool) => server.accepted?.includes(tool));
          const asked = writes.filter((tool) => !accepted.includes(tool));
          const state = server.state ?? 'waits';
          return (
            <li key={server.name} className="flex flex-col gap-0.5 text-small text-ink-soft">
              <span className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
                <code className="font-mono text-meta text-ink">{server.name}</code>
                <span>{t(`plugin.help.state.${state}`)}</span>
              </span>
              {server.allow.length > 0 && <span>{t('plugin.help.reads', { tools: server.allow.join(', ') })}</span>}
              {state === 'approved' && accepted.length > 0 && <span>{t('plugin.help.writesAccepted', { tools: accepted.join(', ') })}</span>}
              {state === 'approved' && asked.length > 0 && <span>{t('plugin.help.writesAsked', { tools: asked.join(', ') })}</span>}
              {state !== 'approved' && writes.length > 0 && <span>{t('plugin.help.writes', { tools: writes.join(', ') })}</span>}
              {server.allow.length === 0 && writes.length === 0 && <span>{t('plugin.help.callsNone')}</span>}
              {state === 'refused' && server.refusal && <span><Inline text={server.refusal} /></span>}
              {plugin && onHelp && state !== 'refused' && (
                <span className="mt-1 flex flex-wrap gap-2">
                  {state === 'approved' ? (
                    <Button variant="ghost" aria-label={t('plugin.help.withdrawNamed', { name: server.name })} onClick={() => onHelp(plugin, server.name, 'help-withdraw')}>
                      {t('plugin.help.withdraw')}
                    </Button>
                  ) : (
                    <>
                      <Button aria-label={t('plugin.help.approveNamed', { name: server.name })} onClick={() => onHelp(plugin, server.name, 'help-approve')}>
                        {t('plugin.help.approve')}
                      </Button>
                      {writes.length > 0 && (
                        <Button variant="ghost" onClick={() => onHelp(plugin, server.name, 'help-reads')}>{t('plugin.help.approveReads')}</Button>
                      )}
                    </>
                  )}
                </span>
              )}
            </li>
          );
        })}
      </ul>
      <Body className="mt-2">{t('plugin.help.body')}</Body>
    </PageSection>
  );
}

/** Who a skill is handed to, by the words the page says it in (HELPPLUG1b). */
function skillTo(to: string[]): string {
  const sessions = to.includes('sessions');
  const help = to.includes('help');
  return sessions && help ? 'plugin.skills.to.both' : help ? 'plugin.skills.to.help' : 'plugin.skills.to.sessions';
}

/**
 * **The skills a plugin hands** (HELPPLUG1b, D170 point 4): each in the mono face with who it is handed to, the driver's own
 * sentence under one it does not hand, and a line saying each is copied into a session as it starts and never into a
 * repository. None where it hands no skill.
 */
export function SkillRows({ skills }: { skills?: SkillShown[] }) {
  const { t } = useTranslation();
  if (!skills?.length) return null;
  return (
    <PageSection title={t('plugin.section.skills')}>
      <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
        {skills.map((skill) => (
          <li key={skill.name} className="flex flex-col gap-0.5 text-small">
            <span className="flex flex-wrap items-baseline gap-x-3 gap-y-0.5">
              <code className="font-mono text-meta text-ink">{skill.name}</code>
              <span className="text-ink-soft">{t(skillTo(skill.to))}</span>
            </span>
            {skill.problem && <span className="text-ink-soft"><Inline text={skill.problem} /></span>}
          </li>
        ))}
      </ul>
      <Body className="mt-1.5">{t('plugin.skills.body')}</Body>
    </PageSection>
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
 * - **Its icon leads the header** (D140 §2), its own or its monogram, *update available* sits beside its state where an
 *   update waits, and its Source says why a declared icon is not drawn.
 */
export function PluginPage({
  plugin, kitPoints, canTry = false, trial, trying = false, plan, updating = false, acting = false, asking = false,
  onSwitch, onHelp, onTry, onAskUpdate, onApplyUpdate, onCancelUpdate, onAskRemove, onRemove, onCancelRemove,
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
  /** The person's approval of what a server offers Ask Daoris, or its withdrawal (D170's note); none offers no press. */
  onHelp?: (id: string, server: string, action: HelpPress) => void;
  onTry: (id: string) => void;
  onAskUpdate: (id: string) => void;
  onApplyUpdate: (id: string) => void;
  onCancelUpdate: () => void;
  onAskRemove: (id: string) => void;
  /** The second press, told how the removal ended: a refusal is said inside the ask (UXFIX2b1). */
  onRemove: (id: string, answered: Answered) => void;
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
  // A right-click on the page offers its header's acts, as its buttons press them, then its id (CTX1, D138 §4).
  const menu = {
    label: plugin.name || plugin.id,
    acts: [
      {
        id: 'switch', label: t(plugin.enabled ? 'plugin.disable' : 'plugin.enable'), icon: 'plug' as const, disabled: acting,
        onSelect: () => onSwitch(plugin.id, plugin.enabled ? 'disable' : 'enable'),
      },
      ...(tryable ? [{ id: 'try', label: t('plugin.kit.try'), icon: 'execute' as const, disabled: trying, onSelect: () => onTry(plugin.id) }] : []),
      ...(updatable(plugin.source)
        ? [{ id: 'update', label: t('plugin.update.ask'), icon: 'refresh' as const, disabled: updating, onSelect: () => onAskUpdate(plugin.id) }]
        : []),
      ...(!asking ? [{ id: 'remove', label: t('plugin.forget'), icon: 'remove' as const, disabled: acting, onSelect: () => onAskRemove(plugin.id) }] : []),
      { id: 'copy', label: t('contextMenu.act.copyPlugin'), icon: 'copy' as const, copy: plugin.id },
    ],
  };

  const head = (
    <PageHead
      icon={<PluginIcon id={plugin.id} name={plugin.name} icon={plugin.icon} size="page" dimmed={!plugin.enabled} />}
      title={plugin.name || plugin.id}
      version={plugin.version}
      pills={(
        <>
          {word && <Pill tone={word.tone}>{t(word.key)}</Pill>}
          {/* An update waits on nobody, so it is never a state: the neutral pill beside it (D119 §2, D140 §2). */}
          {updateWaits(plugin) && <Pill tone="neutral">{t('plugin.update.waits')}</Pill>}
        </>
      )}
      id={plugin.id}
      line={plugin.description}
      acts={acts}
    />
  );

  return (
    <ViewMain header={head} menu={menu}>
      {/* Status leads (platform language §4): a refused plugin's sentence comes first. */}
      {plugin.problem && <Lead text={plugin.problem} />}

      {asking && (
        <InlineConfirm
          className="mb-4"
          label={t('plugin.removeTitle', { id: plugin.id })}
          says={<Inline text={t('plugin.removeConfirm', { data: plugin.data })} />}
          meanIt={t('plugin.removeMeanIt')}
          busy={acting}
          onConfirm={(answered) => onRemove(plugin.id, answered)}
          onClose={onCancelRemove}
        />
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

      <HelpServers plugin={plugin.id} servers={plugin.helpServers} onHelp={onHelp} />
      <SkillRows skills={plugin.skills} />

      <PageSection title={t('plugin.section.tests')}>
        <h3 className="m-0 mb-1.5 text-small font-medium text-ink-soft">{t('plugin.tests.trial')}</h3>
        {trying ? (
          <Body><Inline text={t('plugin.kit.working')} /></Body>
        ) : trial ? (
          <TrialReport trial={trial} />
        ) : !speaks(plugin) ? (
          <Body>{t('plugin.tests.nothing')}</Body>
        ) : (
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
            <Body className="min-w-0 flex-1 basis-64"><Inline text={t('plugin.tests.none')} /></Body>
            {tryable && <Button variant="ghost" onClick={() => onTry(plugin.id)}>{t('plugin.kit.try')}</Button>}
          </div>
        )}
      </PageSection>

      <PageSection title={t('plugin.section.data')}>
        <PathText path={plugin.data} className="text-small text-ink-soft" />
        <Body className="mt-1">{t('plugin.data.body')}</Body>
      </PageSection>

      <PageSection title={t('plugin.section.source')}>
        <div className="flex flex-col gap-1 text-small text-ink-soft">
          <PluginSourceLine source={plugin.source ?? { kind: 'none' }} />
          <span className="flex flex-wrap items-baseline gap-x-1.5">
            <span className="text-ink-faint">{t('plugin.field.folder')}</span>
            <PathText path={plugin.folder} className="text-small" />
          </span>
          {/* Its author's to fix, and never a refusal (D140 §3.1): the reader's sentence, whole, beside its monogram. */}
          {plugin.iconProblem && <span><Inline text={t('plugin.icon.problem', { problem: plugin.iconProblem })} /></span>}
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
