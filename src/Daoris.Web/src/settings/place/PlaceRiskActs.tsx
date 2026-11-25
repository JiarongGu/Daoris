import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, Segmented, SettingRow } from '../../design/ui';
import { holderSaid } from './PlaceCast';

/** An act's class at a place: nobody's yes, a role's, or the owner's own. */
export type RiskClass = 'none' | 'role' | 'owner';

const CLASSES: readonly RiskClass[] = ['none', 'role', 'owner'];

/** One kind of act at a place as `RISK_TABLE` answers it. */
export type RiskActRowView = {
  kind: string;
  class: string;
  /** The role that answers a `role` row. */
  by?: string | null;
  /** The rule's place (`repository/<name>`, `workspace/<name>`, `machine`) where a row decides, else `seed`, `fixed` or `unreadable`. */
  source: string;
  reason: string;
  /** The holder the place's cast gives the answering role. */
  answers?: { role: string; holder: string } | null;
  /** The last seven days of the judge's passes and asks for a yes at the place. */
  passes: number;
  gates: number;
  locked: boolean;
};

/** One wait a pending change opens or releases or moves to another answerer. */
export type RiskShiftView = { place: string; act: string; mode: string; from: string; to: string; effect: string };

/** A place's act table, and the waits the pending change would open or release. */
export type RiskActsView = {
  place: string;
  mode: string;
  revision: string;
  fault?: string | null;
  unknown: string[];
  rows: RiskActRowView[];
  shifts: RiskShiftView[];
};

/** One edit of a row as the driver takes it: no class takes the row away, so the next place or the seed decides. */
export type RiskActEdit = { place: string; act: string; class: string | null; by?: string | null; mode?: string | null };

/** The change the owner's press sends: the edits and the revision of the policy the table read. */
export type RiskActsChange = { edits: RiskActEdit[]; revision: string };

/** The bridge's calls the act table reads and writes through, handed down by the organism that holds the bridge. */
export type RiskActsBridge = {
  useTable: (workspace: string | null, repository: string | null, edit: RiskActEdit | null) => { data?: RiskActsView };
  useSet: () => { mutate: (change: RiskActsChange, options?: { onSuccess?: () => void }) => void; isPending: boolean };
};

/** A class chosen and not yet saved. */
export type PendingAct = { kind: string; class: string };

/** Where a row's rule comes from, in the reader's words. */
const fromSaid = (t: TFunction, row: RiskActRowView) =>
  ['seed', 'fixed', 'unreadable'].includes(row.source) ? t(`settings.risk.acts.from.${row.source}`) : t('settings.risk.acts.from.row', { place: row.source });

/**
 * **A place's act table**: per kind of act its class here, the rule it comes from, who answers by the cast and seven days of passes and holds;
 * `policy.change` is shown fixed. Choosing a class sends nothing: the waits the change would open or release are shown first, and *Save* is the
 * owner's own press. Props only.
 */
export function PlaceRiskActs({ name, twin, view, pending = null, busy = false, onPick, onSave, onCancel }: {
  name: string;
  twin: string;
  view: RiskActsView;
  pending?: PendingAct | null;
  busy?: boolean;
  onPick: (kind: string, riskClass: RiskClass) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  return (
    <SettingRow label={t('settings.risk.acts.label')} hint={twin} why={t('settings.risk.acts.body')}>
      {view.fault && <p role="alert" className="m-0 text-small text-ink"><Inline text={t('settings.risk.acts.fault', { problem: view.fault })} /></p>}
      <ul aria-label={t('settings.risk.acts.list', { name })} className="m-0 grid list-none gap-2 p-0">
        {view.rows.map((row) => {
          const shown = pending?.kind === row.kind ? pending.class : row.class;
          return (
            <li key={row.kind} data-act={row.kind} className="grid min-w-0 gap-0.5 text-small">
              <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1">
                <code className="font-mono text-ink">{row.kind}</code>
                {row.locked || view.fault ? (
                  <span className="text-ink-soft">{row.locked ? t('settings.risk.acts.locked') : t(`settings.risk.acts.class.${row.class}`)}</span>
                ) : (
                  <Segmented
                    label={t('settings.risk.acts.choice', { act: row.kind })}
                    value={shown}
                    options={CLASSES.map((each) => ({ value: each, label: t(`settings.risk.acts.class.${each}`) }))}
                    onChange={(chosen) => {
                      if (busy || chosen === shown) return;
                      onPick(row.kind, chosen as RiskClass);
                    }}
                  />
                )}
              </div>
              <p className="m-0 text-ink-soft">
                <Inline
                  text={[
                    fromSaid(t, row),
                    row.answers ? t('settings.risk.acts.answers', { role: row.answers.role, holder: holderSaid(t, row.answers.holder) }) : '',
                    t('settings.risk.acts.week', { passed: row.passes, asked: row.gates }),
                  ].filter(Boolean).join(t('projects.setup.sentenceJoin'))}
                />
              </p>
            </li>
          );
        })}
      </ul>
      {view.unknown.length > 0 && <p className="m-0 text-small text-ink-soft">{t('settings.risk.acts.unknown', { rows: view.unknown.join(', ') })}</p>}
      {pending && (
        <div role="status" className="grid gap-1 text-small text-ink">
          {view.shifts.length === 0
            ? <p className="m-0">{t('settings.risk.acts.none', { act: pending.kind })}</p>
            : view.shifts.map((shift) => (
              <p key={`${shift.act}:${shift.mode}:${shift.place}`} className="m-0">
                {t('settings.risk.acts.shift', {
                  act: shift.act,
                  mode: t(`settings.risk.acts.mode.${shift.mode}`, { defaultValue: shift.mode }),
                  from: t(`settings.risk.acts.class.${shift.from}`, { defaultValue: shift.from }),
                  to: t(`settings.risk.acts.class.${shift.to}`, { defaultValue: shift.to }),
                  effect: t(`settings.risk.acts.effect.${shift.effect}`, { defaultValue: shift.effect }),
                })}
              </p>
            ))}
          <div className="flex gap-2">
            <Button variant="primary" disabled={busy} onClick={onSave}>{t('settings.risk.acts.save')}</Button>
            <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('settings.risk.acts.cancel')}</Button>
          </div>
        </div>
      )}
    </SettingRow>
  );
}

/** The key of the place the table is read at: a repository's, else its workspace's, else the machine's. */
const placeKey = (workspace: string | null, repository: string | null) =>
  repository ? `repository/${repository}` : workspace ? `workspace/${workspace}` : 'machine';

/**
 * **A place's act table with its reads and its press**, held by the organism that holds the bridge: the table is read with the pending change so
 * the waits it opens or releases are shown before it is saved, and *Save* sends it at the revision the table read.
 */
export function PlaceRiskActsSection({ bridge, workspace, repository }: { bridge: RiskActsBridge; workspace: string | null; repository: string | null }) {
  const { t } = useTranslation();
  const [pending, setPending] = useState<PendingAct | null>(null);
  const place = placeKey(workspace, repository);
  const edit: RiskActEdit | null = pending ? { place, act: pending.kind, class: pending.class } : null;
  const { data } = bridge.useTable(workspace, repository, edit);
  const set = bridge.useSet();
  if (!data) return null;
  const name = repository ?? workspace ?? t('settings.risk.acts.machine');
  const twin = t('settings.risk.acts.twin', { place });
  return (
    <PlaceRiskActs
      name={name}
      twin={twin}
      view={data}
      pending={pending}
      busy={set.isPending}
      onPick={(kind, riskClass) => setPending({ kind, class: riskClass })}
      onCancel={() => setPending(null)}
      onSave={() => {
        if (!edit) return;
        set.mutate({ edits: [edit], revision: data.revision }, { onSuccess: () => setPending(null) });
      }}
    />
  );
}
