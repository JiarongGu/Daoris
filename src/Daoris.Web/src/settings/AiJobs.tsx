import { useTranslation } from 'react-i18next';
import { Card, Pill, Prose, SelectField, SettingRow } from '../ui';
import type { StartWiring } from '../map/wiring';

/** What the service says answers search and convergence (D24) — its own words, verbatim. */
export type SearchTier = { tier: string; note?: string | null; semantic: boolean };

/** The intake on THIS machine (INT4b): which agent answers an ask, and as whom. Desktop only. */
export type IntakeJob = {
  /** The agent `driver.json` names for it, or null — declarations only. */
  adapter: string | null;
  /** What the machine can name: each way in it has, by the id a terminal types, as a person knows it. */
  agents: { value: string; label: string }[];
  /** What an intake in each circle would run on — the driver's own answer, its intake rows. */
  starts: StartWiring[];
  /**
   * What a person calls this account of this tool — the roster's to know, so it arrives as a
   * function. No profile is the tool's own home, which the roster names too (UX5 U53).
   */
  nameOf: (owner: string, profile?: string | null) => string;
  /** Name an agent, or null to answer by declarations only. */
  onChange: (adapter: string | null) => void;
  busy?: boolean;
};

/** Off, as the control's value. The terminal spells it the same way (`daoris driver intake off`). */
const OFF = 'off';

/**
 * Daoris's own AI (AGT6): each job Daoris may use a model for, the tier that answers it now, and how
 * to change it.
 *
 * @remarks
 * Written from the owner's *"whats the mean 'ai' setup for daoris itself (so some task we can use ai
 * to support)"* (2026-09-23). The answer is two jobs, and each is stated the way `model-decoupling`
 * asks: what it does with no model, which tier is answering, and where the choice is made.
 *
 * **The search tier is the service's sentence, never a claim of this page's** (D24): the pill and the
 * note are what `/api/status` said, verbatim, and before it has said anything the page claims nothing.
 * How to change it is a sentence and not a control, because the model is read from the service's
 * environment when it starts — a control here would be a second source for a choice the deployment
 * makes, and on a browser a promise it could not keep.
 *
 * **The intake is this machine's** (`driver.json`), so it is absent without one — a browser is given
 * no intake at all, never a disabled one (D47 §4). Its control is the screen's half of `daoris driver
 * intake <agent>|off` (D50), and which account an intake runs as is the driver's answer per circle,
 * read rather than recomputed, so the page cannot name an account the loop would not take.
 */
export function AiJobs({ search, intake }: { search?: SearchTier; intake?: IntakeJob }) {
  const { t } = useTranslation();

  return (
    // No title of its own: the domain list names it (D75), and a lone card would say it twice.
    <Card className="mt-3.5">
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.body')}</Prose>

      <SettingRow
        label={t('settings.ai.search.label')}
        hint={t('settings.ai.search.hint')}
        why={t('settings.ai.search.why')}
        control={search
          ? <Pill>{search.tier}</Pill>
          : <span className="text-small text-ink-faint">{t('settings.ai.unknown')}</span>}
      >
        {search?.note && <p className="m-0 max-w-prose text-small text-ink-soft">{search.note}</p>}
      </SettingRow>

      {intake && <Intake intake={intake} />}
    </Card>
  );
}

function Intake({ intake }: { intake: IntakeJob }) {
  const { t } = useTranslation();
  // What is in effect is shown as itself, even when this screen would not offer it — a name typed at
  // a terminal, a plugin's door since removed. Reading "off" over a machine that is not would be the
  // control lying about the file it edits.
  const agents = intake.adapter && !intake.agents.some((agent) => agent.value === intake.adapter)
    ? [...intake.agents, { value: intake.adapter, label: intake.adapter }]
    : intake.agents;

  return (
    <SettingRow
      label={t('settings.ai.intake.label')}
      hint={t('settings.ai.intake.hint')}
      why={t('settings.ai.intake.why')}
      control={(
        <SelectField
          value={intake.adapter ?? OFF}
          onChange={(value) => {
            if (intake.busy) return;
            intake.onChange(value === OFF ? null : value);
          }}
          options={[{ value: OFF, label: t('settings.ai.intake.off') }, ...agents]}
          ariaLabel={t('settings.ai.intake.agent')}
        />
      )}
    >
      <p className="m-0 max-w-prose text-small text-ink-soft">
        {intake.adapter
          ? t('settings.ai.intake.tierOn', { agent: intake.adapter })
          : t('settings.ai.intake.tierOff')}
      </p>

      {/* As whom, per circle — the driver's own resolution for the intake, the same one a start in
          that circle reads. One line each: the whole wiring is under *What a start runs on*. */}
      {intake.adapter && intake.starts.length > 0 && (
        <ul className="m-0 mt-1.5 grid list-none gap-1 p-0">
          {intake.starts.map((start) => (
            <li
              key={start.workspace}
              aria-label={t('wiring.rowIntake', { workspace: start.workspace })}
              className="grid gap-1 text-small"
            >
              <span className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                <span className="font-mono font-semibold text-ink">{start.workspace}</span>
                {start.refusal ? (
                  <Pill>{t('wiring.held')}</Pill>
                ) : (
                  <>
                    <span className="text-ink-faint">{t('settings.ai.intake.runsAs')}</span>
                    <span className="text-ink wrap-anywhere">
                      {intake.nameOf(start.owner, start.profile)}
                    </span>
                    <span className="text-meta text-ink-faint">{t(`wiring.accountFrom.${start.profileFrom}`)}</span>
                  </>
                )}
              </span>
              {/* The driver's sentence, verbatim (D46 §3): it names what to run. */}
              {start.refusal && (
                <p className="m-0 max-w-prose whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-ink-soft">
                  {start.refusal}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </SettingRow>
  );
}
