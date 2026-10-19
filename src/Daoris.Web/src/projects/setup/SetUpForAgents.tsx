import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Prose } from '../../design/ui';
import { PageSection } from '../../work/ViewMain';

/**
 * A set-up's plan as the driver answers it (LAYOUT8; `SETUP_PLAN`, and Ask Daoris's `setup` card): the twin of
 * `daoris-driver setup <repository> --plan`. What was read on the repository's line, which setup it asks, the agent that carries
 * it and how its work lands, the versions the tools a child finds answered, the rule the press adds, the quest's title and body,
 * and every refusal with its code. Names and versions only. The bridge leaves a null out.
 */
export type SetupShown = {
  repository: string;
  workspace?: string | null;
  line?: string | null;
  /** The line's commit every fact was read at, to twelve characters. */
  commit?: string | null;
  /** `agents`, `.claude`, `its own` or `none`, as the driver names the layout. */
  layout?: string | null;
  adopted?: boolean | null;
  declares?: boolean | null;
  asks?: 'whole' | 'move' | 'declare' | null;
  agent?: string | null;
  lands?: 'merge' | 'branch' | null;
  /** The plugin a branch is handed to, where the landing rule names one. */
  landsWith?: string | null;
  node?: string | null;
  daoris?: string | null;
  rules: string[];
  title?: string | null;
  body?: string | null;
  /** Every refusal that applies, at once: a code a screen keys on and the driver's sentence naming the door that answers it. */
  refusals: { code: string; sentence: string }[];
  pressable: boolean;
};

/** What the press came to (`SETUP_PRESS`): the service's sentence, the ask and quest it made, the rules it added, and its plan. */
export type SetupPressed = {
  published: boolean;
  message: string;
  askId?: string | null;
  questId?: string | null;
  added: string[];
  plan: SetupShown;
};

/** One fact of the plan: its label in the label column, its value beside it, as a repository's page lays its facts out. */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-meta text-ink-faint">{label}</dt>
      <dd className="m-0 min-w-0 break-words text-small text-ink-soft">{children}</dd>
    </>
  );
}

/**
 * **What a set-up would ask, before the press** (LAYOUT8, D117 §6.5): what was read on the line, the setup it asks, who carries
 * it and how it lands, the tools, the rule it adds and the quest's whole text, folded under its title. Drawn the same on the
 * repository's page and on Ask Daoris's card. Chrome translates; every name, version, rule and the quest's text is content.
 */
export function SetupPlanFacts({ plan }: { plan: SetupShown }) {
  const { t } = useTranslation();
  // A version is the program's own answer, set as code; none is said in the page's words.
  const found = (version?: string | null) => (version ? `\`${version}\`` : t('projects.setUp.notFound'));
  return (
    <div className="mt-2 flex flex-col gap-2">
      <dl aria-label={t('projects.setUp.facts', { repository: plan.repository })}
        className="m-0 grid grid-cols-[max-content_1fr] items-baseline gap-x-4 gap-y-1.5">
        {plan.line && (
          <Fact label={t('projects.setUp.read')}>
            <Inline text={t('projects.setUp.at', { line: plan.line, commit: plan.commit ?? '' })} />
          </Fact>
        )}
        {plan.layout && (
          <Fact label={t('projects.setUp.layout')}>
            <Inline text={`\`${plan.layout}\``} />
            {' · '}{t(plan.adopted ? 'projects.setUp.adopted' : 'projects.setUp.notAdopted')}
            {' · '}{t(plan.declares ? 'projects.setUp.declares' : 'projects.setUp.declaresNothing')}
          </Fact>
        )}
        {plan.asks && <Fact label={t('projects.setUp.asksLabel')}>{t(`projects.setUp.asks.${plan.asks}`)}</Fact>}
        {plan.agent && <Fact label={t('projects.setUp.agent')}><Inline text={`\`${plan.agent}\``} /></Fact>}
        {plan.lands && (
          <Fact label={t('projects.setUp.lands')}>
            <Inline text={plan.lands === 'branch'
              ? plan.landsWith
                ? t('projects.setUp.landsBranchWith', { plugin: plan.landsWith })
                : t('projects.setUp.landsBranch')
              : t('projects.setUp.landsMerge')}
            />
          </Fact>
        )}
        <Fact label={t('projects.setUp.tools')}>
          <Inline text={t('projects.setUp.versions', { node: found(plan.node), daoris: found(plan.daoris) })} />
        </Fact>
        <Fact label={t('projects.setUp.rule')}>
          <ul aria-label={t('projects.setUp.rule')} className="m-0 flex list-none flex-wrap gap-x-2 gap-y-0.5 p-0">
            {plan.rules.map((rule) => <li key={rule} className="m-0 font-mono text-meta">{rule}</li>)}
          </ul>
        </Fact>
      </dl>
      {plan.title && (
        <details className="m-0 text-small">
          <summary className="cursor-pointer text-ink">
            {t('projects.setUp.quest')}: <span className="text-ink-soft">{plan.title}</span>
          </summary>
          {/* The quest's own words, as the repository's session will read them: content, never translated. */}
          <pre className="m-0 mt-1 max-h-80 overflow-auto whitespace-pre-wrap break-words rounded-control bg-raised px-3 py-2 font-mono text-meta text-ink-soft">
            {plan.body}
          </pre>
        </details>
      )}
    </div>
  );
}

export type SetUpForAgentsProps = {
  repository: string;
  /** The plan, once asked for; null before the person asks to see it. */
  plan: SetupShown | null;
  /** Reading the plan, or reading it again. */
  reading: boolean;
  /** The press, under way. */
  pressing: boolean;
  /** What the press came to, once pressed. */
  pressed: SetupPressed | null;
  /** The bridge's sentence where the plan or the press could not be asked (a driver still coming up). */
  problem: string | null;
  /** Show the plan, or read it again after fixing what it refused. */
  onLook: () => void;
  onPress: () => void;
  onClose: () => void;
};

/**
 * **Set up for agents** (LAYOUT8, D117 §6.1, §6.5; D170 point 1): on a not adopted repository's page with a checkout here, the
 * press `daoris-driver setup <repository>` is, and its plan first, the twin of `--plan`. The press asks the repository's own
 * session, through one quest, and adds the rule its session needs; Daoris writes nothing into the repository (D32), so this is
 * the repository's own act, asked rather than done for it.
 *
 * @remarks
 * A molecule: its holder reads the plan and presses. Nothing is read until the person asks, since the plan starts git and two
 * programs. A plan the press would refuse says every refusal at once, each naming the door that answers it, and offers no press.
 */
export function SetUpForAgents({ repository, plan, reading, pressing, pressed, problem, onLook, onPress, onClose }: SetUpForAgentsProps) {
  const { t } = useTranslation();
  const shown = pressed?.plan ?? plan;
  return (
    <PageSection title={t('projects.setUp.title')}>
      <Prose>{t('projects.setUp.lead')}</Prose>
      {problem && <p role="alert" className="m-0 mt-2 text-small text-ink-danger"><Inline text={problem} /></p>}
      {pressed
        ? (
          <div role="status" className="mt-2">
            <p className={pressed.published ? 'm-0 text-small text-ink' : 'm-0 text-small text-ink-danger'}>
              <Inline text={pressed.message} />
            </p>
            {pressed.published && (
              <p className="m-0 mt-1 text-meta text-ink-soft">
                <Inline text={pressed.questId
                  ? t('projects.setUp.publishedQuest', { repository, quest: pressed.questId })
                  : t('projects.setUp.published', { repository })}
                />
              </p>
            )}
            <div className="mt-2 flex flex-wrap gap-2"><Button variant="ghost" onClick={onClose}>{t('common.close')}</Button></div>
          </div>
        )
        : shown
          ? (
            <div aria-busy={reading || pressing || undefined}>
              {shown.pressable
                ? <SetupPlanFacts plan={shown} />
                : (
                  <div className="mt-2">
                    <p className="m-0 text-small font-semibold text-ink">{t('projects.setUp.refused')}</p>
                    <ul aria-label={t('projects.setUp.refused')} className="m-0 mt-1 flex list-none flex-col gap-1 p-0">
                      {/* The driver's sentences, each naming the door that answers it, as `--plan` prints them. */}
                      {shown.refusals.map((refusal) => (
                        <li key={refusal.code} className="m-0 text-small text-ink-soft"><Inline text={refusal.sentence} /></li>
                      ))}
                    </ul>
                  </div>
                )}
              <div className="mt-2 flex flex-wrap gap-2">
                {shown.pressable
                  ? <Button variant="primary" disabled={pressing || reading} onClick={onPress}>{t('projects.setUp.press')}</Button>
                  : <Button disabled={reading} onClick={onLook}>{t('projects.setUp.again')}</Button>}
                <Button variant="ghost" disabled={pressing} onClick={onClose}>{t('common.cancel')}</Button>
              </div>
            </div>
          )
          : (
            <div className="mt-2" aria-busy={reading || undefined}>
              <Button disabled={reading} onClick={onLook}>
                {t(reading ? 'projects.setUp.reading' : 'projects.setUp.look')}
              </Button>
            </div>
          )}
    </PageSection>
  );
}
