import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { SettingRow, Segmented } from '../../design/ui';

/** A place's risk (`daoris settings risk`): at low and medium an appointed role settles what its grants cover, at high none does. */
export type RiskValue = 'low' | 'medium' | 'high';

/** The mark that keeps a place's production releases the owner's whatever its risk. */
export const PRODUCTION_RELEASES = 'release.production';

/** One place's row as `STATE`'s `placeRisk` answers it: `machine`, `workspace/<name>` or `repository/<name>`. */
export type PlaceRiskRow = { place: string; risk: string; marks: string[] };

/** The edit as `SET_PLACE_RISK` takes it: neither place named is the machine's record. */
export type PlaceRiskChange = { workspace?: string; repository?: string; risk?: RiskValue; mark?: string[]; unmark?: string[] };

export const RISK_VALUES: readonly RiskValue[] = ['low', 'medium', 'high'];

/** A risk the page knows, else high, the safe side, where the driver answered one this page does not; absent is the default, low. */
export const riskValue = (value: string | null | undefined): RiskValue =>
  value == null ? 'low' : (RISK_VALUES as readonly string[]).includes(value) ? (value as RiskValue) : 'high';

/** A workspace's own row among `STATE`'s, none where nothing is set. */
export const workspaceRisk = (rows: PlaceRiskRow[] | undefined, workspace: string): PlaceRiskRow | undefined =>
  rows?.find((row) => row.place.toLowerCase() === `workspace/${workspace}`.toLowerCase());

/** Where a place's risk stands from where it sets none: its workspace's row, else the machine's. */
export type InheritedRisk = { row: PlaceRiskRow; from: 'workspace' | 'machine' };

/**
 * A repository's own row and, where it sets none, the row it takes, the narrowest set standing (`PlaceRiskBook`): its workspace's, else
 * the machine's; neither where nothing is set, which is Daoris's default.
 */
export function repositoryRisk(rows: PlaceRiskRow[] | undefined, repository: string, workspace?: string | null): { row?: PlaceRiskRow; inherited?: InheritedRisk } {
  const own = rows?.find((row) => row.place.toLowerCase() === `repository/${repository}`.toLowerCase());
  if (own) return { row: own };
  const wider = workspace ? workspaceRisk(rows, workspace) : undefined;
  if (wider) return { inherited: { row: wider, from: 'workspace' } };
  const machine = rows?.find((row) => row.place === 'machine');
  return machine ? { inherited: { row: machine, from: 'machine' } } : {};
}

/** A value in a folded section's line. */
export function riskSummary(t: TFunction, value: string | null | undefined): string {
  return t(`settings.risk.summary.${riskValue(value)}`);
}

/**
 * **A place's risk and the owner's marks**: the risk as one choice, the production releases' mark as another, what stands and where it is
 * set. Each press goes out as the edit at once, judged by the driver; choosing what stands sends nothing. Props only (components §2).
 */
export function PlaceRiskSetting({ name, twin, row, inherited, busy = false, onChange }: {
  name: string;
  twin: string;
  /** The place's own row, none where nothing is set here. */
  row?: PlaceRiskRow;
  /** Where nothing is set here, the wider place's row that stands and which place it is: a repository's own row overrides it. */
  inherited?: InheritedRisk;
  busy?: boolean;
  onChange: (change: Omit<PlaceRiskChange, 'workspace' | 'repository'>) => void;
}) {
  const { t } = useTranslation();
  const standing = row ?? inherited?.row;
  const risk = riskValue(standing?.risk);
  // A destination's mark covers every place under it, so a wider place's mark stands here too.
  const marked = [row, inherited?.row].some((each) => each?.marks.includes(PRODUCTION_RELEASES) ?? false);
  const from = row ? 'own' : inherited ? inherited.from : 'default';
  return (
    <SettingRow label={t('settings.risk.label')} hint={twin} why={t('settings.risk.body')}
      control={(
        <Segmented
          label={t('settings.risk.choice', { name })}
          value={risk}
          options={RISK_VALUES.map((each) => ({ value: each, label: t(`settings.risk.values.${each}`) }))}
          onChange={(chosen) => {
            if (busy || chosen === risk) return;
            onChange({ risk: chosen });
          }}
        />
      )}
    >
      <p className="m-0 text-small text-ink-soft">
        {[t(`settings.risk.says.${risk}`), t(`settings.risk.from.${from}`), marked ? t('settings.risk.marked') : '']
          .filter(Boolean).join(t('projects.setup.sentenceJoin'))}
      </p>
      <Segmented
        label={t('settings.risk.releases', { name })}
        value={marked ? 'yours' : 'place'}
        options={[{ value: 'yours', label: t('settings.risk.releases.yours') }, { value: 'place', label: t('settings.risk.releases.place') }]}
        onChange={(chosen) => {
          if (busy || (chosen === 'yours') === marked) return;
          onChange(chosen === 'yours' ? { mark: [PRODUCTION_RELEASES] } : { unmark: [PRODUCTION_RELEASES] });
        }}
      />
    </SettingRow>
  );
}
