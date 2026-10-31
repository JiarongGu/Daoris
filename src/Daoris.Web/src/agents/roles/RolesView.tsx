import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  helpModeOf, refusedNoRoute, useActors, useAnswerStaffingFound, useDriver, useHarnesses, useRoles, useSetActor, useSetRole,
  useStaffingFound, useStarts, type RoleChangePress,
} from '../../bridge/shell';
import { useRegistry } from '../../data/queries';
import { workspacesOf } from '../../data/workspaces';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import { namer } from '../../settings/accounts/namer';
import { AskDaorisAbout, IntakeAbout } from '../../settings/ai/ActorAbout';
import { doorLabel } from '../tools';
import type { RoleSection } from '../modes';
import { type ActorChoice, type Editing, RolesCard, roleName } from './Roles';
import { RoleSectionMain } from './RoleSections';

type Answered = { done: () => void; refused: (sentence: string) => void };

/**
 * **The Roles mode of the Agents place** (COMPANYVIEW1b, the company-view design §1, §3.1): its chosen section's page and every press on
 * it. Today only Working roles is built, the catalogue and its staffing of COMPANYVIEW1a moved here from AI features.
 *
 * @remarks
 * **The mode's one owner** (D168 point 3): this hook holds the open editor and every pending press, and hands the molecule its
 * answers, so `RolesCard` imports nothing of the bridge. Each press goes out as its route takes it (`SET_ROLE`, `SET_ACTOR`,
 * `STAFFING_FOUND_ANSWER`) and the driver judges; a refusal is its sentence under the press. It is mounted only while the mode is in
 * front, so opening the place asks the driver nothing of roles (D150 point 5).
 */
export function useRolesMode({ notify }: { notify: Notify }) {
  const { t, i18n } = useTranslation();
  const driver = useDriver();
  const roster = useHarnesses();
  const registry = useRegistry('machine');
  const workspaces = workspacesOf(registry.data ?? []);
  const starts = useStarts(workspaces);
  const [editing, setEditing] = useState<Editing | null>(null);

  const harnesses = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  // An agent a person can name here is a way in this machine HAS: one not installed would hold every start, and a choice whose only
  // outcome is a hold is worse than none.
  const agents = harnesses.filter((door) => door.present).map((door) => ({ value: door.harness, label: doorLabel(t, door) }));
  const startRows = Array.isArray(starts.data?.starts) ? starts.data.starts : [];

  // Each order as the driver reads it, set through SET_ACTOR, the person's press. A change of Build's agent keeps Chat and Brainstorm,
  // still as before, on the agent they ran, written for them, and says so.
  const actors = useActors();
  const setActor = useSetActor();
  const staffing = actors.data ?? [];
  const saveActor = (actor: string, choices: ActorChoice[] | null, answered: Answered) => {
    const build = staffing.find((row) => row.id === 'build')?.choices[0]?.agent;
    const followers = staffing.filter((row) => (row.id === 'chat' || row.id === 'brainstorm') && row.state === 'before')
      .map((row) => t(`settings.ai.actors.name.${row.id}`));
    const named = t(`settings.ai.actors.name.${actor}`);
    setActor.mutate({ actor, choices }, {
      onSuccess: () => {
        notify(t(choices === null ? 'settings.ai.actors.cleared' : 'settings.ai.actors.saved', { actor: named }));
        if (actor === 'build' && build && choices?.[0] && choices[0].agent !== build && followers.length > 0) {
          notify(t('settings.ai.actors.kept', { agent: build, actors: followers.join(t('settings.ai.actors.and')) }));
        }
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // The changes of driver.json's actors another program made, which the driver kept for the person.
  const found = useStaffingFound();
  const answerFound = useAnswerStaffingFound();
  const answerStaffingFound = (id: number, answer: 'take' | 'dismiss', answered: Answered) =>
    answerFound.mutate({ found: id, answer }, {
      onSuccess: () => {
        notify(t(answer === 'take' ? 'settings.ai.actors.found.taken' : 'settings.ai.actors.found.dismissed'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });

  // The role catalogue, each change the person's press over SET_ROLE, judged by the driver's role door.
  const roles = useRoles();
  // Why there is no catalogue: a shell older than the route says so by NO_ROUTE or by answering nothing; any other refusal is a failed read.
  const read = roles.data ? 'ok' : roles.isError ? (refusedNoRoute(roles.error) ? 'notOffered' : 'failed') : roles.isSuccess ? 'notOffered' : 'loading';
  const setRole = useSetRole();
  const pressRole = (press: RoleChangePress, answered: Answered) => {
    const row = roles.data?.roles.find((each) => each.id === press.id);
    const named = press.role
      ? roleName(t, i18n.language, { id: press.id, seeded: row?.seeded ?? false, nameEn: press.role.name.en, nameZh: press.role.name.zh })
      : roleName(t, i18n.language, row ?? { id: press.id, seeded: false });
    setRole.mutate(press, {
      onSuccess: () => {
        notify(t(press.change === 'set' ? 'settings.ai.roles.saved' : press.change === 'remove' ? 'settings.ai.roles.removedSaved' : 'settings.ai.roles.defaulted', { role: named }));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    });
  };

  // Intake's and Ask Daoris's rows say what they do now under their order: Intake's per circle, the driver's own resolution.
  const first = (id: string) => staffing.find((row) => row.id === id)?.choices[0]?.agent ?? null;
  const helpMode = helpModeOf(driver.data?.helpMode);
  const about = {
    intake: {
      why: t('settings.ai.intake.why'),
      below: <IntakeAbout agent={first('intake')} starts={startRows.filter((start) => start.job === 'intake')} nameOf={namer(t, harnesses)} />,
    },
    'ask-daoris': {
      why: t('settings.ai.helper.why'),
      below: <AskDaorisAbout agent={first('ask-daoris')} full={driver.data?.helpMode !== undefined && helpMode === 'full'} />,
    },
  };

  const shown = roles.data || staffing.length > 0 || read === 'failed';
  const card = shown
    ? (
      <RolesCard
        catalogue={roles.data ?? null} read={read} staffing={staffing} agents={agents} about={about}
        busy={setRole.isPending || setActor.isPending || answerFound.isPending}
        onSet={pressRole} onSetActor={saveActor} found={found.data ?? []} onAnswerFound={answerStaffingFound}
        editor={editing} onEditor={setEditing}
      />
    )
    : null;
  return { card };
}

/** The Roles mode's main area: the chosen section, and for Working roles the catalogue and its staffing. */
export function RolesMode({ section, notify }: { section: RoleSection; notify: Notify }) {
  const { card } = useRolesMode({ notify });
  return <RoleSectionMain section={section}>{card}</RoleSectionMain>;
}
