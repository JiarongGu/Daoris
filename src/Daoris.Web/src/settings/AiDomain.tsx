import { useTranslation } from 'react-i18next';
import { useRegistry, useStatus } from '../data/queries';
import {
  helpModeOf, useDriver, useHarnesses, usePlugins, useSetHelper, useSetHelpGrant, useSetHelpMode, useSetIntake, useStarts,
} from '../bridge/shell';
import type { HelpGrantChoice, HelpGrantId } from '../help/access/wired';
import { doorLabel } from '../agents/tools';
import { failure, type Notify } from '../design/ui';
import { workspacesOf } from '../data/workspaces';
import { useModeChoice } from '../help/access/HelpModePicker';
import { fullAdds } from '../help/access/modes';
import { AiJobs, type SearchTier } from './AiJobs';
import { namer } from './accounts/namer';

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to
 * change it — between Appearance and the machine, because it is for everyone.
 *
 * @remarks
 * Which tier answers search is the service's answer over HTTP, the one every browser is given and
 * the status bar already states (D24), so a browser sees that job too. The intake is this machine's
 * `driver.json`, so its half — and every query it needs — exists only with a shell (D47 §4): a
 * browser is given no intake at all, never a disabled one.
 */
export function AiDomain({ attached, notify }: { attached: boolean; notify: Notify }) {
  const status = useStatus();
  const search: SearchTier | undefined = status.data
    ? { tier: status.data.tier, note: status.data.note, semantic: status.data.semantic }
    : undefined;

  return attached ? <MachineAi search={search} notify={notify} /> : <AiJobs search={search} />;
}

function MachineAi({ search, notify }: { search?: SearchTier; notify: Notify }) {
  const { t } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const registry = useRegistry('machine');
  const setIntake = useSetIntake();
  const setHelper = useSetHelper();
  // The circles *What a start runs on* names, spelled the same way — so both cards read one answer.
  const workspaces = workspacesOf(registry.data ?? []);
  const answer = useStarts(workspaces);
  // Ask Daoris's access (HELPMODE1b, D183 point 2), by the panel chip's rule: restricted at once, full access on its confirmation,
  // which names the servers the plugins offer it and wait for an approval, read only while it asks.
  const setHelpMode = useSetHelpMode();
  const helpMode = helpModeOf(driver.data?.helpMode);
  const choice = useModeChoice(helpMode, (mode) => setHelpMode.mutate({ mode }, {
    onSuccess: () => notify(t(mode === 'full' ? 'settings.ai.helpMode.full' : 'settings.ai.helpMode.restricted')),
    onError: failure(notify),
  }));
  const plugins = usePlugins({ enabled: choice.confirming });
  const setHelpGrant = useSetHelpGrant();

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // Restricted mode's configured access (HELPMODE1c2b): the grants the driver serves, as it holds them, each press one operation
  // over SET_HELP_GRANT; one it does not act on is never sent. The browser, the checkouts and the settings are drawn as the mock
  // draws them until their grants are built, so a press there says so and sends nothing.
  const served = Array.isArray(driver.data?.helpGrants) ? driver.data.helpGrants : null;
  const helperDoor = harnesses.find((door) => door.harness === driver.data?.helperAdapter);
  const built: Partial<Record<HelpGrantId, readonly string[]>> = Object.fromEntries(
    (served ?? []).filter((grant) => Array.isArray(grant.built)).map((grant) => [grant.id, grant.built!]));
  const chooseGrant = (grant: HelpGrantId, operation: string, chosen: boolean) => {
    if (!built[grant]?.includes(operation)) {
      notify(t('settings.ai.grants.notYet'));
      return;
    }
    const named = t(`help.grants.op.${grant}.${operation}`);
    setHelpGrant.mutate({ grant, operation, chosen }, {
      onSuccess: () => notify(t(chosen ? 'settings.ai.grants.granted' : 'settings.ai.grants.revoked', { operation: named })),
      onError: failure(notify),
    });
  };
  // An agent a person can name here is a way in this machine HAS: one not installed would hold every
  // intake, and a choice whose only outcome is a hold is worse than none.
  const agents = harnesses
    .filter((door) => door.present)
    .map((door) => ({ value: door.harness, label: doorLabel(t, door) }));
  const starts = Array.isArray(answer.data?.starts) ? answer.data.starts : [];

  return (
    <AiJobs
      search={search}
      // An older shell has never heard of the intake: its STATE carries no field, and it gets no row.
      // Off is "" rather than null, because the bridge leaves a null out — which read as older.
      intake={driver.data && 'intakeAdapter' in driver.data
        ? {
          adapter: driver.data.intakeAdapter || null,
          agents,
          starts: starts.filter((start) => start.job === 'intake'),
          nameOf: namer(t, harnesses),
          busy: setIntake.isPending,
          onChange: (adapter) => setIntake.mutate({ adapter }, {
            onSuccess: () => notify(adapter
              ? t('settings.ai.intake.named', { agent: adapter })
              : t('settings.ai.intake.cleared')),
            onError: failure(notify),
          }),
        }
        : undefined}
      // Ask Daoris's own agent (D89): absent on a shell older than it, "" off, as the intake's.
      helper={driver.data && 'helperAdapter' in driver.data
        ? {
          adapter: driver.data.helperAdapter || null,
          agents,
          busy: setHelper.isPending,
          onChange: (adapter) => setHelper.mutate({ adapter }, {
            onSuccess: () => notify(adapter
              ? t('settings.ai.helper.named', { agent: adapter })
              : t('settings.ai.helper.cleared')),
            onError: failure(notify),
          }),
          full: driver.data.helpMode !== undefined && helpMode === 'full',
        }
        : undefined}
      // Ask Daoris's access: absent on a shell older than the mode, which offers no choice.
      access={driver.data && 'helpMode' in driver.data
        ? {
          mode: helpMode,
          confirming: choice.confirming,
          adds: plugins.isSuccess || plugins.isError ? fullAdds(null, plugins.data?.plugins) : null,
          busy: setHelpMode.isPending,
          onPick: choice.pick,
          onEnable: choice.enable,
          onCancel: choice.cancel,
        }
        : undefined}
      // Configured access: absent on a shell older than the grant, which keeps none.
      grants={served
        ? {
          // The agent Ask Daoris runs on, and whether it takes Claude's rules; with none named, nothing is unavailable on it yet.
          agent: {
            name: helperDoor ? doorLabel(t, helperDoor) : driver.data?.helperAdapter ?? '',
            rules: helperDoor ? helperDoor.takesRules === true : true,
          },
          grants: served.map((grant): HelpGrantChoice => ({ id: grant.id as HelpGrantId, chosen: grant.chosen })),
          built,
          busy: setHelpGrant.isPending,
          onChoose: chooseGrant,
        }
        : undefined}
    />
  );
}
