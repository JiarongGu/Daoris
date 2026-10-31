import { useTranslation } from 'react-i18next';
import { useStatus } from '../data/queries';
import { helpModeOf, useDriver, useHarnesses, usePlugins, useSetHelpGrant, useSetHelpMode } from '../bridge/shell';
import type { HelpGrantChoice, HelpGrantId } from '../help/access/wired';
import { doorLabel } from '../agents/tools';
import { Button, Card, CardHeader, CardStack, failure, Prose, type Notify } from '../design/ui';
import { sentence } from '../locales/format';
import { useModeChoice } from '../help/access/HelpModePicker';
import { fullAdds } from '../help/access/modes';
import { AiJobs, type SearchTier } from './AiJobs';
import type { StarterDoor } from '../help/starters';

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
export function AiDomain({ attached, notify, onGo }: { attached: boolean; notify: Notify; onGo?: (door: StarterDoor) => void }) {
  const status = useStatus();
  const search: SearchTier | undefined = status.data
    ? { tier: status.data.tier, note: status.data.note, semantic: status.data.semantic }
    : undefined;

  return attached ? <MachineAi search={search} notify={notify} onGo={onGo} /> : <AiJobs search={search} />;
}

function MachineAi({ search, notify, onGo }: { search?: SearchTier; notify: Notify; onGo?: (door: StarterDoor) => void }) {
  const { t } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  // Ask Daoris's access (HELPMODE1b, D183 point 2), by the panel chip's rule: restricted at once, full access on its confirmation,
  // which names the servers the plugins offer it and wait for an approval, read only while it asks. A refusal is said where it was
  // pressed (ACCESSASK1, the UI/UX design R3): inside the ask, or under the chip, never a toast.
  const setHelpMode = useSetHelpMode();
  const helpMode = helpModeOf(driver.data?.helpMode);
  const choice = useModeChoice(helpMode, (mode, answered) => setHelpMode.mutate({ mode }, {
    onSuccess: () => {
      notify(t(mode === 'full' ? 'settings.ai.helpMode.full' : 'settings.ai.helpMode.restricted'));
      answered.done();
    },
    onError: (error) => answered.refused(sentence(error)),
  }));
  const plugins = usePlugins({ enabled: choice.confirming });
  const setHelpGrant = useSetHelpGrant();

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // Restricted mode's configured access (HELPMODE1c2b): the grants the driver serves, as it holds them, each press one operation
  // over SET_HELP_GRANT; one it does not act on is never sent. Housekeeping, the chosen settings (HELPMODE1c3) and the checkouts
  // (HELPMODE1c4, each readable checkout the driver serves turned off or on by its repository's name) are chosen here; and the
  // browser's is its plugin's approval, drawn read only, its door that plugin's tab (the modes design §4.1's decided note).
  const served = Array.isArray(driver.data?.helpGrants) ? driver.data.helpGrants : null;
  const helperDoor = harnesses.find((door) => door.harness === driver.data?.helperAdapter);
  const built: Partial<Record<HelpGrantId, readonly string[]>> = Object.fromEntries(
    (served ?? []).filter((grant) => Array.isArray(grant.built)).map((grant) => [grant.id, grant.built!]));
  const chooseGrant = (grant: HelpGrantId, operation: string, chosen: boolean) => {
    if (!built[grant]?.includes(operation)) {
      notify(t('settings.ai.grants.notYet'));
      return;
    }
    // A checkout is named by its repository, as written.
    const named = grant === 'checkouts' ? operation : t(`help.grants.op.${grant}.${operation}`);
    setHelpGrant.mutate({ grant, operation, chosen }, {
      onSuccess: () => notify(t(chosen ? 'settings.ai.grants.granted' : 'settings.ai.grants.revoked', { operation: named })),
      onError: failure(notify),
    });
  };
  return (
    <CardStack>
    <AiJobs
      search={search}
      // Ask Daoris's access: absent on a shell older than the mode, which offers no choice.
      access={driver.data && 'helpMode' in driver.data
        ? {
          mode: helpMode,
          confirming: choice.confirming,
          adds: plugins.isSuccess || plugins.isError ? fullAdds(null, plugins.data?.plugins) : null,
          busy: setHelpMode.isPending,
          refusal: choice.refusal,
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
          // Pending while a conversation running here was handed another choice, which its next turn takes (ACCESSSWAP1); the
          // browser's changed since approval, and the plugin whose tab holds it.
          grants: served.map((grant): HelpGrantChoice => ({
            id: grant.id as HelpGrantId, chosen: grant.chosen, pending: grant.pending === true, changed: grant.changed === true,
            plugin: grant.plugin ?? null,
          })),
          built,
          busy: setHelpGrant.isPending,
          onChoose: chooseGrant,
          // The plugin's own tab, or Plugins where no plugin here offers it.
          onManage: onGo ? (_grant, plugin) => onGo(plugin ? { view: 'plugins', item: plugin } : { view: 'plugins' }) : undefined,
        }
        : undefined}
    />
    {/* The roles live in the Agents place (COMPANYVIEW1b): one line here, with the door. */}
    <Card>
      <CardHeader title={t('settings.ai.roles.title')} />
      <Prose className="mb-3 mt-0 text-small text-ink-soft">{t('settings.ai.roles.moved')}</Prose>
      {onGo && <Button variant="ghost" onClick={() => onGo({ view: 'agents', agentsMode: 'roles' })}>{t('settings.ai.roles.open')}</Button>}
    </Card>
    </CardStack>
  );
}
