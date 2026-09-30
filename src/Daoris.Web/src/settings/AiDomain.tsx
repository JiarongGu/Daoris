import { useTranslation } from 'react-i18next';
import { useRegistry, useStatus } from '../queries';
import { useDriver, useHarnesses, useSetHelper, useSetIntake, useStarts } from '../shell';
import { doorLabel } from '../tools';
import { failure, type Notify } from '../ui';
import { workspacesOf } from '../workspaces';
import { AiJobs, type SearchTier } from './AiJobs';
import { namer } from './namer';

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

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
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
        }
        : undefined}
    />
  );
}
