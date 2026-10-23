import { useTranslation } from 'react-i18next';
import { Pill } from '../../design/ui';
import type { StartWiring } from '../../map/wiring';

/**
 * What Intake's row says under its order (INT4b; MODELROLE1d, D205 §7: the Intake card folded into its actor row): what an ask
 * the declarations leave does, and, per circle, the account an intake there runs as, the driver's own resolution, the same one a
 * start in that circle reads. One line each: the whole wiring is under *What a start runs on*.
 *
 * A molecule: every state is reached by its props.
 */
export function IntakeAbout({ agent, starts, nameOf }: {
  /** Intake's first agent, or null where it is off. */
  agent: string | null;
  /** What an intake in each circle would run on: the driver's own answer, its intake rows. */
  starts: readonly StartWiring[];
  /** What a person calls this account of this tool: the roster's to know (UX5 U53). */
  nameOf: (owner: string, profile?: string | null) => string;
}) {
  const { t } = useTranslation();
  return (
    <>
      <p className="m-0 text-small text-ink-soft">
        {agent ? t('settings.ai.intake.tierOn', { agent }) : t('settings.ai.intake.tierOff')}
      </p>
      {agent && starts.length > 0 && (
        <ul className="m-0 mt-1.5 grid list-none gap-1 p-0">
          {starts.map((start) => (
            <li key={start.workspace} aria-label={t('wiring.rowIntake', { workspace: start.workspace })} className="grid gap-1 text-small">
              <span className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                <span className="font-mono font-semibold text-ink">{start.workspace}</span>
                {start.refusal ? (
                  <Pill>{t('wiring.held')}</Pill>
                ) : (
                  <>
                    <span className="text-ink-faint">{t('settings.ai.intake.runsAs')}</span>
                    <span className="text-ink wrap-anywhere">{nameOf(start.owner, start.profile)}</span>
                    <span className="text-meta text-ink-faint">{t(`wiring.accountFrom.${start.profileFrom}`)}</span>
                  </>
                )}
              </span>
              {/* The driver's sentence, verbatim (D46 §3): it names what to run. */}
              {start.refusal && (
                <p className="m-0 whitespace-pre-wrap border-l-[3px] border-warn pl-2.5 text-ink-soft">{start.refusal}</p>
              )}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

/**
 * What Ask Daoris's row says under its order (HELP1, D89; MODELROLE1d: its card folded into its actor row): on an agent it reads
 * and proposes, or with full access acts itself; off, it offers its starters, the no-agent tier (D24).
 */
export function AskDaorisAbout({ agent, full = false }: { agent: string | null; full?: boolean }) {
  const { t } = useTranslation();
  return (
    <p className="m-0 text-small text-ink-soft">
      {agent ? t(full ? 'settings.ai.helper.tierOnFull' : 'settings.ai.helper.tierOn', { agent }) : t('settings.ai.helper.tierOff')}
    </p>
  );
}
