import { useTranslation } from 'react-i18next';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId, HelpMode } from '../help/access/wired';
import { Card, Pill, Prose, SettingList, SettingRow } from '../design/ui';
import { ConfiguredAccess } from '../help/access/ConfiguredAccess';
import { FullAccessAsk, HelpModeChip } from '../help/access/HelpModePicker';
import { type Answered, Refused } from '../work/InlineConfirm';

/** What the service says answers search and convergence (D24) — its own words, verbatim. */
export type SearchTier = { tier: string; note?: string | null; semantic: boolean };

/**
 * Ask Daoris's access on THIS machine (HELPMODE1b, D183 point 2): the mode each conversation's next turn takes (ACCESSSWAP1), chosen
 * by the panel's chip's rule, which the domain holds: restricted at once, full access only on its confirmation.
 */
export type HelpAccessJob = {
  mode: HelpMode;
  /** Full access picked, and asked before it is set. */
  confirming: boolean;
  /** The servers full access would add, by name; null while they are being read. */
  adds: readonly string[] | null;
  busy?: boolean;
  /** A switch refused, said under the chip (the UI/UX design R3); full access's refusal is said inside its ask. */
  refusal?: string | null;
  onPick: (mode: HelpMode) => void;
  onEnable: (answered: Answered) => void;
  onCancel: () => void;
};

/**
 * Restricted mode's configured access on THIS machine (HELPMODE1c1, D183 point 1; the modes design §4.1, §5): the agent the
 * grants are held on, what is chosen, and each choice handed up.
 */
export type HelpGrantsJob = {
  agent: HelpGrantAgent;
  grants: readonly HelpGrantChoice[];
  /** The grants whose operations show from the start. */
  opened?: readonly HelpGrantId[];
  busy?: boolean;
  /** The operations the driver acts on, for each grant it serves (HELPMODE1c2b); a grant not named is drawn as the mock. */
  built?: Partial<Record<HelpGrantId, readonly string[]>>;
  onChoose: (grant: HelpGrantId, operation: string, chosen: boolean) => void;
  /** A grant set on a plugin's own tab (the browser's, HELPMODE1c3): its door, leading to that tab. */
  onManage?: (grant: HelpGrantId, plugin: string | null) => void;
};

/**
 * Daoris's own AI (AGT6): each job Daoris may use a model for, the tier that answers it now, and how
 * to change it.
 *
 * @remarks
 * What Daoris itself uses a model for. The answer is two jobs, and each is stated the way `model-decoupling`
 * asks: what it does with no model, which tier is answering, and where the choice is made.
 *
 * **The search tier is the service's sentence, never a claim of this page's** (D24): the pill and the
 * note are what `/api/status` said, verbatim, and before it has said anything the page claims nothing.
 * How to change it is a sentence and not a control, because the model is read from the service's
 * environment when it starts — a control here would be a second source for a choice the deployment
 * makes, and on a browser a promise it could not keep.
 *
 * **Intake and Ask Daoris are actors** (MODELROLE1d, D205 §7): their cards folded into their rows on *Actors*, which is this
 * machine's (`driver.json`), so a browser is given neither (D47 §4). Ask Daoris's access stays here, this machine's too.
 */
export function AiJobs({ search, access, grants }: {
  search?: SearchTier; access?: HelpAccessJob;
  /** Restricted mode's configured access, drawn under the access row while restricted (HELPMODE1c1). */
  grants?: HelpGrantsJob;
}) {
  const { t } = useTranslation();

  return (
    // No title of its own: the main area's header names the domain (FRAME1g), and a lone card would say it twice.
    <Card>
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.body')}</Prose>

      {/* The card's rows on one label track (R1, UIFIT1): each job's value beside its words. */}
      <SettingList>
      <SettingRow
        label={t('settings.ai.search.label')}
        hint={t('settings.ai.search.hint')}
        why={t('settings.ai.search.why')}
        control={search
          ? <Pill>{search.tier}</Pill>
          : <span className="text-small text-ink-faint">{t('settings.ai.unknown')}</span>}
      >
        {search?.note && <p className="m-0 text-small text-ink-soft">{search.note}</p>}
      </SettingRow>

      {access && <HelpAccess access={access} grants={grants} />}
      </SettingList>
    </Card>
  );
}

/**
 * Ask Daoris's access (HELPMODE1b, D183 point 2; the modes design §5): the panel's own chip, beside its agent, this machine's choice
 * for every workspace. Full access's confirmation opens under the row, where it has the row's width, and so does restricted mode's
 * configured access while restricted (HELPMODE1c1, design §5's sketch): full access hands everything, so it has nothing to grant.
 */
function HelpAccess({ access, grants }: { access: HelpAccessJob; grants?: HelpGrantsJob }) {
  const { t } = useTranslation();
  return (
    <SettingRow
      label={t('settings.ai.helpMode.label')}
      hint={t('settings.ai.helpMode.hint')}
      why={t('settings.ai.helpMode.why')}
      control={<HelpModeChip selected={access.mode} busy={access.busy} onPick={access.onPick} />}
    >
      <p className="m-0 text-small text-ink-soft">
        {t(access.mode === 'full' ? 'settings.ai.helpMode.tierFull' : 'settings.ai.helpMode.tierRestricted')}
      </p>
      {access.refusal && <Refused className="mt-2" sentence={access.refusal} />}
      {access.confirming && (
        <div className="mt-2">
          <FullAccessAsk adds={access.adds} onEnable={access.onEnable} onClose={access.onCancel} />
        </div>
      )}
      {grants && access.mode === 'restricted' && (
        <div className="mt-3">
          <ConfiguredAccess
            agent={grants.agent}
            grants={grants.grants}
            opened={grants.opened}
            busy={grants.busy}
            built={grants.built}
            onChoose={grants.onChoose}
            onManage={grants.onManage}
          />
        </div>
      )}
    </SettingRow>
  );
}
