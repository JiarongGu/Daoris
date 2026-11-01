import { type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { refusedNoRoute } from '../../bridge/shell';
import { useRoleStructureChain } from '../../bridge/driver';
import type { RoleStructure, StructurePlaceAsk, StructureWork } from '../../data/wiring';
import { Button, Icon, type Notify, SettingList, SettingRow } from '../../design/ui';
import type { Authored } from '../../locales/authored';
import { InlineConfirm } from '../../work/InlineConfirm';
import { AppointedRoles } from '../roles/AppointedRoles';
import { type Editing, editingOfLine, entryText, type Line, mayEditLine, nameFields, newRoleEditing, placeText, roleName, RolesCard } from '../roles/Roles';
import { useRoleEditing } from '../roles/RolesView';
import { stateText } from '../roles/StructureSections';

type Kind = 'machine' | 'workspace' | 'repository';

const kindOf = (place: StructurePlaceAsk): Kind => (place.repository ? 'repository' : place.workspace ? 'workspace' : 'machine');

/** The place a role's scope names is this one: this machine's own roles, or a workspace's or repository's by its name. */
const placedHere = (place: StructurePlaceAsk, scope: { kind: string; id?: string }): boolean => {
  const kind = kindOf(place);
  return scope.kind === kind && (kind === 'machine' || scope.id === (kind === 'repository' ? place.repository : place.workspace));
};

/** The states of a first choice that stop the work as it stands (SETUPUX1l): the rest read as the parent's would. */
const READS_FINE = new Set(['ready', 'unknown', 'not-read']);

/** Whether a work needs a look: its starts hold, a part of it did not read, or its first choice cannot run. */
const needsLook = (work: StructureWork): boolean => !!work.held || !!work.choicesFailed || work.choices.length === 0 || !READS_FINE.has(work.choices[0]!.state.code);

/** A work's first choice as one line: the agent, model and effort in the tool's words, and the last reading it rests on. */
function ChoiceLine({ work }: { work: StructureWork }) {
  const { t } = useTranslation();
  const first = work.choices[0];
  if (!first) return <p className="m-0 text-small text-ink-faint wrap-anywhere">{work.held ?? t('agents.setup.roles.noChoice')}</p>;
  return (
    <p className="m-0 text-small text-ink-soft wrap-anywhere">
      {entryText(t, { agent: first.agent, model: first.model, effort: first.effort })}
      {' · '}
      {stateText(t, first.state)}
    </p>
  );
}

/**
 * **A place's Roles section** (AGENTSPLACE1g1, the Agents-place design §3): one row per work, read from `ROLE_STRUCTURE` for the place's
 * chain alone: the role taking it, where that role is placed, its first choice and that choice's last reading, with the appointed roles
 * of the place under them.
 *
 * @remarks
 * - **A row taken from a parent offers *Give \<place\> its own…***, and a role placed here *Edit* and *Back to \<parent\>'s*, which is the
 *   role's removal asked in an `InlineConfirm` that names the role the work falls back to. All three are the role editor of All roles
 *   (`RolesCard`'s editor) and `SET_ROLE`: the place is filled and fixed, the id is made, the page judges nothing.
 * - **It reads the last readings alone** (D150 point 5): the chain's read probes no account.
 */
export function PlaceRoles({ place, notify, onSource }: { place: StructurePlaceAsk; notify: Notify; onSource?: SourceDoor }) {
  const editor = useRoleEditing(notify);
  const chain = useRoleStructureChain(place);
  const read = chain.data ? 'ok' : chain.isError ? (refusedNoRoute(chain.error) ? 'notOffered' : 'failed') : 'loading';
  return <PlaceRolesView place={place} read={read} answer={chain.data ?? null} editor={editor} appointed={<AppointedRoles place={place} />} onSource={onSource} />;
}

/** The door from a role's source mark to the place that sets it, at the same work (SETUPUX1h): its workspace's page, or this machine's. */
export type SourceDoor = (scope: { kind: 'workspace' | 'machine'; id?: string }, work: string) => void;

/** What the section needs of the role editor's hook: the molecule reads no bridge. */
export type PlaceRoleEditor = Pick<ReturnType<typeof useRoleEditing>, 'agents' | 'offers' | 'staffing' | 'catalogue' | 'read' | 'busy' | 'onSet' | 'onSetActor'>;

/** The section drawn from its reads, and every press out (`SET_ROLE`, `SET_ACTOR`): a molecule, so a story reaches every state by props. */
export function PlaceRolesView({ place, read, answer, editor, appointed, onSource }: {
  place: StructurePlaceAsk;
  /** Told where a role placed above the place is pressed: its source mark is a door to the place that sets it. */
  onSource?: SourceDoor;
  /** The chain's read: its rows, not read yet, not offered by this shell, or failed. */
  read: 'ok' | 'loading' | 'notOffered' | 'failed';
  answer: RoleStructure | null;
  editor: PlaceRoleEditor;
  /** The place's appointed roles, drawn under the works. */
  appointed: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState<{ work: string; editing: Editing } | null>(null);
  const [backing, setBacking] = useState<string | null>(null);
  const [unfolded, setUnfolded] = useState(false);
  // A draft's key is the editor's own; these count down from -1, as *Add a role here*'s does, so they never meet the card's own count up.
  const keys = useRef(0);
  const nextKey = () => --keys.current;

  const kind = kindOf(place);
  const here = place.repository ?? place.workspace ?? '';
  const placeName = kind === 'machine' ? t('agents.setup.machine') : here;
  const parentName = kind === 'repository' ? place.workspace ?? t('agents.setup.machine') : t('agents.setup.machine');
  const { catalogue, staffing } = editor;
  const kept = catalogue?.kept === true;
  const mine = answer?.places[0];
  const parent = kind === 'machine' ? undefined : answer?.places[1];
  const name = (role: { id: string; name?: Authored | null }) => roleName(t, i18n.language, { ...role, seeded: false });
  const workName = (work: string) => t(`settings.ai.roles.work.${work}`, { defaultValue: work });
  const lineOf = (id: string): Line => ({
    id, row: catalogue?.roles.find((each) => each.id === id) ?? null, actor: staffing.find((each) => each.id === id) ?? null,
  });

  const give = (work: StructureWork) => {
    const base = newRoleEditing(catalogue?.works ?? [], editor.agents, kind === 'machine' ? null : { kind, id: here }, nextKey());
    const beside = (text: string) => (text ? `${text} · ${placeName}` : '');
    const named = work.role ? nameFields(work.role.name, '') : { name: workName(work.work), nameIn: [] };
    const choices = work.choices.filter((choice) => /^[1-4]$/.test(choice.from));
    setOpen({
      work: work.work,
      editing: {
        ...base, work: work.work, takes: [work.work], name: beside(named.name) || (named.nameIn.length > 0 ? '' : beside(workName(work.work))),
        nameIn: named.nameIn.map(([tag, text]): [string, string] => [tag, beside(text)]),
        drafts: choices.length > 0
          ? choices.map((choice) => ({ key: nextKey(), agent: choice.agent, model: choice.model ?? '', effort: choice.effort ?? '' }))
          : base.drafts,
      },
    });
  };
  const edit = (work: string, id: string) => {
    const line = lineOf(id);
    setOpen({
      work,
      editing: editingOfLine(line, { kept, works: catalogue?.works ?? [], agents: editor.agents, name: line.row ? name(line.row) : id, key: nextKey }),
    });
  };

  // A row is a work this place sets or that differs from the parent's, or that needs a look; a role taking several is one row's worth, so
  // its works stand together. The rest fold. This machine has no parent, so it folds nothing.
  const reasons = (work: StructureWork): boolean => {
    if (!parent) return true;
    return (work.role !== undefined && placedHere(place, work.role.scope)) || work.role?.id !== parent.works.find((each) => each.work === work.work)?.role?.id || needsLook(work);
  };
  const withRole = (work: StructureWork) => (work.role ? (mine?.works ?? []).filter((each) => each.role?.id === work.role!.id) : [work]);
  const shown = (mine?.works ?? []).filter((work) => withRole(work).some(reasons));
  const folded = (mine?.works ?? []).filter((work) => !shown.includes(work));
  const foldedOpen = unfolded || folded.some((work) => work.work === open?.work);

  const row = (work: StructureWork): ReactNode => {
    const role = work.role;
    const mineHere = role ? placedHere(place, role.scope) : false;
    const label = workName(work.work);
    const line = role ? lineOf(role.id) : null;
    const parentRole = parent?.works.find((each) => each.work === work.work)?.role;
    const alongside = role ? withRole(work).filter((each) => each.work !== work.work).map((each) => workName(each.work)).join(t('settings.ai.actors.and')) : '';
    const asking = backing === work.work && role !== undefined;
    const busy = editor.busy;
    return (
      <SettingRow
        key={work.work}
        label={<span data-place-row={work.work} tabIndex={-1} className="outline-none focus-visible:outline-2 focus-visible:outline-accent">{label}</span>}
        control={!asking && !(open?.work === work.work) && (
          <>
            <span className="text-body text-ink wrap-anywhere">
              {role ? `${name(role)} · ` : t('agents.setup.roles.noRole')}
              {role && (onSource && !mineHere && (role.scope.kind === 'workspace' || role.scope.kind === 'machine')
                ? (
                  <Button
                    variant="ghost" className="px-0 py-0 align-baseline text-body"
                    onClick={() => onSource({ kind: role.scope.kind as 'workspace' | 'machine', id: role.scope.id }, work.work)}
                  >
                    {placeText(t, role.scope.kind, role.scope.id)}
                  </Button>
                )
                : placeText(t, role.scope.kind, role.scope.id))}
            </span>
            {role && mineHere && line && mayEditLine(line, kept) && (
              <Button variant="ghost" disabled={busy || open !== null} aria-label={t('agents.setup.roles.editOf', { work: label })} onClick={() => edit(work.work, role.id)}>
                {t('settings.ai.actors.edit')}
              </Button>
            )}
            {role && mineHere && kind !== 'machine' && (
              <Button
                variant="ghost" disabled={busy || !kept || open !== null}
                aria-label={t('agents.setup.roles.backOf', { work: label, parent: parentName })} onClick={() => setBacking(work.work)}
              >
                {t('agents.setup.roles.back', { parent: parentName })}
              </Button>
            )}
            {!mineHere && (
              <Button
                variant="ghost" disabled={busy || !kept || open !== null}
                aria-label={t('agents.setup.roles.giveOf', { work: label, place: placeName })} onClick={() => give(work)}
              >
                {t('agents.setup.roles.give', { place: placeName })}
              </Button>
            )}
          </>
        )}
      >
        {asking && role && (
          <InlineConfirm
            label={t('agents.setup.roles.backAsk', { work: label, parent: parentName })}
            says={`${parentRole
              ? t('settings.ai.roles.removeFallback', {
                role: name(role), work: label, fallback: name(parentRole), place: placeText(t, parentRole.scope.kind, parentRole.scope.id),
              })
              : t('settings.ai.roles.removeNone', { role: name(role), work: label })}${alongside ? ` ${t('agents.setup.roles.alsoFalls', { works: alongside })}` : ''}`}
            meanIt={t('agents.setup.roles.backMove', { parent: parentName })}
            busy={busy}
            onConfirm={(answered) => editor.onSet({ change: 'remove', id: role.id, revision: role.revision }, answered)}
            onClose={() => setBacking(null)}
          />
        )}
        {!asking && open?.work !== work.work && <ChoiceLine work={work} />}
        {!asking && open?.work !== work.work && alongside && <p className="m-0 text-small text-ink-faint wrap-anywhere">{t('agents.setup.roles.shared', { works: alongside })}</p>}
        {open?.work === work.work && (
          <RolesCard
            editorOnly
            consequence={open.editing.isNew
              ? t('agents.setup.roles.thenGive', { place: placeName, works: open.editing.takes.map(workName).join(t('settings.ai.actors.and')) })
              : undefined}
            catalogue={catalogue} read={editor.read} staffing={staffing} agents={editor.agents} offers={editor.offers} busy={busy}
            onSet={editor.onSet} onSetActor={editor.onSetActor}
            editor={open.editing} onEditor={(next) => setOpen(next ? { work: work.work, editing: next } : null)}
          />
        )}
      </SettingRow>
    );
  };

  return (
    <div className="grid gap-3">
      {read === 'loading' && <p className="m-0 text-small text-ink-soft">{t('agents.roles.reading')}</p>}
      {read === 'notOffered' && <p className="m-0 text-small text-ink-soft">{t('agents.roles.notOffered')}</p>}
      {read === 'failed' && <p role="alert" className="m-0 text-small text-ink-soft">{t('agents.roles.readFailed')}</p>}
      {answer?.catalogue && <p role="alert" className="m-0 text-small text-ink-soft">{t('agents.roles.catalogueFailed', { why: answer.catalogue.why })}</p>}
      {catalogue && !kept && <p className="m-0 text-small text-ink-soft">{t('settings.ai.roles.unkept')}</p>}
      {mine && shown.length > 0 && <SettingList>{shown.map(row)}</SettingList>}
      {mine && folded.length > 0 && (
        <>
          <button
            type="button" aria-expanded={foldedOpen} onClick={() => setUnfolded(!foldedOpen)}
            className="flex w-full items-start gap-1.5 rounded-control border-0 bg-transparent p-0 text-left text-small text-ink-soft hover:text-ink"
          >
            <Icon name={foldedOpen ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
            <span className="min-w-0 wrap-anywhere">
              {t('agents.setup.roles.more', { count: folded.length, parent: parentName, works: folded.map((work) => workName(work.work)).join(', ') })}
            </span>
          </button>
          {foldedOpen && <SettingList>{folded.map(row)}</SettingList>}
        </>
      )}
      <div className="grid gap-2">
        <h3
          data-place-row="appointed" tabIndex={-1}
          className="m-0 text-small font-medium text-ink-soft outline-none focus-visible:outline-2 focus-visible:outline-accent"
        >
          {t('agents.setup.roles.appointed')}
        </h3>
        {appointed}
      </div>
    </div>
  );
}
