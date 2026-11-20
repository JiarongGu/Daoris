import type { TFunction } from 'i18next';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inline, SelectField, SettingRow } from '../../design/ui';

const DEFAULT_HOLDER = 'default';

/** One role's holder at a place now, and why (`CastNow`): `owner`, `marked`, `risk`, `cast`, `staffed`, `sign-in` or `default` (the role's agent). */
export type CastRole = { role: string; holder: string; why: string; source: string; words?: string | null };

/** One level of the reporting line every role at the place reports to. */
export type CastLevel = { level: string; role?: string | null; holder?: string | null; why: string };

/** A place's cast as `PLACE_CAST` answers it. */
export type PlaceCastView = {
  place: string; risk: string; riskSource: string; marks: string[]; roles: CastRole[]; roster: string[]; line: CastLevel[];
  /** Why the casts file does not read, where it does not: the roles shown are the last good read and no choice is kept until it reads. */
  unreadable?: string | null;
};

/** The cast as `SET_PLACE_CAST` takes it: no holder takes the cast away, back to the role's default. */
export type PlaceCastChange = { workspace?: string | null; repository?: string | null; role: string; holder: string | null; why?: string | null };

/**
 * The bridge's calls a place's cast reads and writes through, handed down by the organism that holds the bridge (`placeCastBridge`), so the
 * cast's section holds none: the read of the place's cast and the owner's choice, each as the bridge's hook answers it.
 */
export type PlaceCastBridge = {
  useCast: (workspace: string, repository: string | null) => { data?: PlaceCastView };
  useSet: () => { mutate: (change: PlaceCastChange) => void; isPending: boolean };
};

/** A holder in the reader's words: you, the role's agent, or an agent by its name. */
export function holderSaid(t: TFunction, holder: string | null | undefined): string {
  if (!holder || holder === 'person') return t('settings.cast.holder.person');
  if (holder === 'agent') return t('settings.cast.holder.agent');
  return t('settings.cast.holder.named', { name: holder.replace(/^agent:/, '') });
}

/** Why a role's holder is who it is, with the place a cast is set on and its reason. */
const whySaid = (t: TFunction, role: CastRole) =>
  t(`settings.cast.why.${role.why}`, { from: t(`settings.risk.from.${role.source}`, { defaultValue: '' }), words: role.words ?? '', defaultValue: role.why });

/** A level of the line in the reader's words: whom it is, and who holds it. */
const levelSaid = (t: TFunction, level: CastLevel) =>
  t('settings.cast.level', {
    level: t(`settings.cast.levels.${level.level}`, { defaultValue: level.level }),
    holder: level.holder == null ? t('settings.cast.vacant') : level.holder === 'person' ? t('settings.cast.holder.person') : `\`${level.role ?? level.holder}\``,
  });

/**
 * **A place's cast**: each role with its holder now and why, the place's risk and the owner's marks, the roster a cast may name and the reporting
 * line each role reports to. A choice casts the role at once, with the reason written beside it, judged and journaled by the driver; choosing what
 * stands sends nothing. Props only (components §2).
 */
export function PlaceCastSetting({ name, twin, view, busy = false, onCast }: {
  name: string;
  twin: string;
  view: PlaceCastView;
  busy?: boolean;
  onCast: (change: Pick<PlaceCastChange, 'role' | 'holder' | 'why'>) => void;
}) {
  const { t } = useTranslation();
  const [why, setWhy] = useState('');
  return (
    <SettingRow label={t('settings.cast.label')} hint={twin} why={t('settings.cast.body')}>
      <p className="m-0 text-small text-ink-soft">
        {[t(`settings.risk.says.${view.risk}`, { defaultValue: view.risk }), view.marks.includes('release.production') ? t('settings.risk.marked') : '']
          .filter(Boolean).join(t('projects.setup.sentenceJoin'))}
      </p>
      {view.unreadable && <p role="alert" className="m-0 text-small text-ink"><Inline text={t('settings.cast.unreadable', { problem: view.unreadable })} /></p>}
      <label className="grid gap-0.5 text-small text-ink-soft">
        {t('settings.cast.why.label')}
        <input className="rounded border border-line px-1.5 py-0.5 text-small" value={why} disabled={busy} onChange={(event) => setWhy(event.target.value)} />
      </label>
      <ul aria-label={t('settings.cast.roles', { name })} className="m-0 grid list-none gap-1 p-0">
        {view.roles.map((role) => (
          <li key={role.role} data-role={role.role} className="flex min-w-0 flex-wrap items-baseline gap-x-2 text-small">
            <span className="font-medium text-ink">{t(`workflow.role.${role.role}`, { defaultValue: role.role })}</span>
            <span className="text-ink">{holderSaid(t, role.holder)}</span>
            <span className="text-ink-faint"><Inline text={whySaid(t, role)} /></span>
            {role.why !== 'owner' && (
              <SelectField
                ariaLabel={t('settings.cast.choice', { role: role.role })}
                disabled={busy || !!view.unreadable}
                value={role.why === 'cast' ? role.holder : DEFAULT_HOLDER}
                options={[{ value: DEFAULT_HOLDER, label: t('settings.cast.default') }, ...view.roster.map((holder) => ({ value: holder, label: holderSaid(t, holder) }))]}
                onChange={(chosen) => {
                  const holder = chosen === DEFAULT_HOLDER ? null : chosen;
                  if (holder === (role.why === 'cast' ? role.holder : null)) return;
                  onCast({ role: role.role, holder, why: why.trim() || null });
                }}
              />
            )}
          </li>
        ))}
      </ul>
      <p className="m-0 text-small text-ink-soft">{t('settings.cast.roster', { holders: view.roster.map((holder) => holderSaid(t, holder)).join(t('settings.cast.listJoin')) })}</p>
      <p className="m-0 text-small text-ink-soft">
        <Inline text={t('settings.cast.reports', { line: view.line.map((level) => levelSaid(t, level)).join(' → ') })} />
      </p>
    </SettingRow>
  );
}
