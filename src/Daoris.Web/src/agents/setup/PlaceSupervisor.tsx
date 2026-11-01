import { standingOf, useDriver } from '../../bridge/shell';
import { SettingList } from '../../design/ui';
import { SupervisorDoor } from '../../projects/setup/SupervisorDoor';
import type { SupervisorLine } from '../../projects/setup/SupervisorRow';

/** Matched without case, as the driver matches a workspace's name. */
const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/** The supervisor's appointment for a workspace (SUPERVISOR1e, D208 §5) as a line says it; none where the shell says no standing confirmation. */
export function supervisorOf(confirmation: unknown, workspace: string): SupervisorLine | null {
  const standingNow = standingOf(confirmation);
  return standingNow && {
    offered: standingNow.supervisorOffered,
    appointment: standingNow.appointments.find((each) => each.role === 'supervisor' && same(each.workspace, workspace)) ?? null,
  };
}

/**
 * **A workspace's supervisor, among its place's Roles** (SETUPUX1v, moved from the Setup tab's Supervisor section): where its appointment
 * stands in one sentence and a door to where it is appointed, which on the place's page is its own *Appointed* row. It appoints nothing, and
 * it presses nothing: the Roles section stays free of the Setup tab's hooks.
 */
export function PlaceSupervisor({ workspace, onOpenAppointed }: { workspace: string; onOpenAppointed?: () => void }) {
  const driver = useDriver();
  const supervisor = supervisorOf(driver.data?.standingConfirmation, workspace);
  if (!supervisor) return null;
  return (
    <div data-place-row="supervisor" tabIndex={-1} className="outline-none focus-visible:outline-2 focus-visible:outline-accent">
      <SettingList>
        <SupervisorDoor workspace={workspace} supervisor={supervisor} onOpen={onOpenAppointed} />
      </SettingList>
    </div>
  );
}
