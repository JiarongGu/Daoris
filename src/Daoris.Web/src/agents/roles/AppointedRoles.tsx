import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { standingOf, useDriver, useSetAppointment } from '../../bridge/shell';
import { canBeAsked } from '../../data/api';
import type { StructurePlaceAsk } from '../../data/wiring';
import { useRegistry } from '../../data/queries';
import { workspaceOf, workspacesOf } from '../../data/workspaces';
import { sentence } from '../../locales/format';
import { appointmentSaid, stewardAppointmentSaid } from '../../person/standing';
import type { SupervisorSetup } from '../../projects/setup/SupervisorRow';
import type { StewardSetup } from '../../settings/wiring/WiringSteward';
import { AppointedList } from './AppointedList';

/** What an appointment's last press here came to, by its scope. */
type Pressed = { started: boolean; refused: string | null };

const same = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

/**
 * **The Appointed roles section** (COMPANYVIEW1e, the company-view design §3.2, §5): the steward for This Daoris, the supervisor of each
 * workspace and of each repository Setup drew it for (driven, or with an appointment on record). Each row is the molecule its home drew
 * (`WiringSteward`, `SupervisorRow`), over `SET_APPOINTMENT`; appointing is the owner's press alone (D208 point 1).
 *
 * @remarks
 * It reads `STATE` and the registry the frame already holds and asks nothing as it opens (D150 point 5). A role this build does not
 * offer is drawn as its home drew it, saying so, with nothing to press: both are dark today (`StandingConfirmation.cs:330`, `:336`).
 */
export function AppointedRoles({ place }: {
  /**
   * One place's appointments alone (AGENTSPLACE1g1): this machine's steward, a workspace's or a repository's supervisor, each a dark role
   * one quiet line. Absent, every appointment, as Appointed roles draws them.
   */
  place?: StructurePlaceAsk;
} = {}) {
  const { t } = useTranslation();
  const driver = useDriver();
  const registry = useRegistry('machine');
  const setAppointment = useSetAppointment();
  const [pressed, setPressed] = useState<Record<string, Pressed | undefined>>({});
  const standing = standingOf(driver.data?.standingConfirmation);

  if (!standing) {
    return driver.isError || driver.isSuccess
      ? <p role="status" className="m-0 text-body text-ink-soft">{t('agents.roles.appointed.unread')}</p>
      : null;
  }

  const rows = registry.data ?? [];
  const drivable = driver.data?.drivable ?? [];
  const supervising = standing.appointments.filter((each) => each.role === 'supervisor');
  const note = (key: string, next: Pressed | null) => setPressed((now) => ({ ...now, [key]: next ?? undefined }));

  /** The supervisor's molecule for one scope: `Appoint` in its ask and `Turn off`, each the owner's press (SUPERVISOR1a-ii, 1e). */
  const supervisorOf = (scope: { repository: string } | { workspace: string }): SupervisorSetup => {
    const repository = 'repository' in scope ? scope.repository : undefined;
    const workspace = 'workspace' in scope ? scope.workspace : undefined;
    const key = repository ? `repository:${repository}` : `workspace:${workspace}`;
    const registration = repository ? rows.find((each) => same(each.repository, repository)) : undefined;
    const circle = registration ? workspaceOf(registration) : '';
    return {
      offered: standing.supervisorOffered,
      appointment: repository
        ? supervising.find((each) => same(each.repository, repository)) ?? null
        : supervising.find((each) => same(each.workspace, workspace ?? '')) ?? null,
      covering: repository ? supervising.find((each) => each.workspace !== '' && same(each.workspace, circle)) ?? null : null,
      holders: standing.holders,
      started: pressed[key]?.started,
      refused: pressed[key]?.refused ?? null,
      busy: setAppointment.isPending,
      onAppoint: (answered, share, window) => {
        note(key, null);
        setAppointment.mutate({ role: 'supervisor', repository, workspace, on: true, share, window }, {
          onSuccess: (answer) => {
            if (!answer.done) return answered.refused(appointmentSaid(t, answer.why, answer.message));
            note(key, { started: true, refused: null });
            answered.done();
          },
          onError: (error) => answered.refused(sentence(error)),
        });
      },
      onTurnOff: () => {
        note(key, null);
        setAppointment.mutate({ role: 'supervisor', repository, workspace, on: false }, {
          onSuccess: (answer) => {
            if (!answer.done) note(key, { started: false, refused: appointmentSaid(t, answer.why, answer.message) });
          },
          onError: (error) => note(key, { started: false, refused: sentence(error) }),
        });
      },
    };
  };

  const steward: StewardSetup | null = standing.stewardOffered
    ? {
      appointment: standing.appointments.find((each) => each.role === 'steward') ?? null,
      offered: standing.stewardActsOffered,
      acts: standing.stewardActs.length,
      holders: standing.holders,
      started: pressed.machine?.started,
      busy: setAppointment.isPending,
      refused: pressed.machine?.refused ?? null,
      onAppoint: (answered, setup, share) => {
        note('machine', null);
        setAppointment.mutate({ role: 'steward', on: true, setup, share }, {
          onSuccess: (answer) => {
            if (!answer.done) return answered.refused(stewardAppointmentSaid(t, answer.why, answer.message));
            note('machine', { started: true, refused: null });
            answered.done();
          },
          onError: (error) => answered.refused(sentence(error)),
        });
      },
      onTurnOff: () => {
        note('machine', null);
        setAppointment.mutate({ role: 'steward', on: false }, {
          onSuccess: (answer) => {
            if (!answer.done) note('machine', { started: false, refused: stewardAppointmentSaid(t, answer.why, answer.message) });
          },
          onError: (error) => note('machine', { started: false, refused: sentence(error) }),
        });
      },
    }
    : null;

  if (place) {
    const machine = !place.workspace && !place.repository;
    if (!(machine ? standing.stewardOffered : standing.supervisorOffered)) {
      const role = t(machine ? 'settings.daoris.steward.label' : 'projects.supervisor.label');
      return <p role="status" data-appointed-dark="" className="m-0 text-small text-ink-faint">{`${role} · ${t('agents.roles.appointed.notOffered')}`}</p>;
    }
    return (
      <AppointedList
        steward={machine ? steward : null}
        machine={machine}
        workspaces={!machine && !place.repository && place.workspace ? [{ workspace: place.workspace, supervisor: supervisorOf({ workspace: place.workspace }) }] : []}
        repositories={place.repository ? [{ repository: place.repository, supervisor: supervisorOf({ repository: place.repository }) }] : []}
      />
    );
  }

  const repositories = rows
    .filter((each) => each.adopted || canBeAsked(each))
    .map((each) => each.repository)
    .filter((repository) => drivable.some((name) => same(name, repository)) || supervising.some((each) => same(each.repository, repository)));

  return (
    <AppointedList
      steward={steward}
      workspaces={workspacesOf(rows).map((workspace) => ({ workspace, supervisor: supervisorOf({ workspace }) }))}
      repositories={repositories.map((repository) => ({ repository, supervisor: supervisorOf({ repository }) }))}
    />
  );
}
