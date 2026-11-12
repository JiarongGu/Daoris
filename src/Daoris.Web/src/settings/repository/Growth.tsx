import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { SettingRow, Segmented } from '../../design/ui';

/**
 * The growth switch as the driver answers it: whether an agent's press that opens a repository or a room runs `mode` (the default: as the
 * access mode says, at once in full access and on your Confirm in restricted mode), is `asked` (on your Confirm even in full access) or is
 * `off` (refused even in full access). It is the person's alone: no mode lets an agent press it, so the page offers it as a choice and
 * never as something Ask Daoris may do.
 */
export type GrowthValue = 'mode' | 'asked' | 'off';

/** Where a repository's value is set: for it, for its workspace, or nowhere, which is `mode`. */
export type GrowthSource = 'repository' | 'workspace' | 'default';

/** One repository's value as the driver resolves it (`LINES`' `growth`): never null, its source `default` where nothing is set. */
export type RepositoryGrowth = { repository: string; workspace: string; value?: string | null; source?: GrowthSource | null };

/** The edit for a repository or a workspace, as `SET_GROWTH` takes it; a repository names its workspace beside it. */
export type GrowthChange = { repository?: string; workspace?: string; value: GrowthValue };

export const GROWTH_VALUES: readonly GrowthValue[] = ['mode', 'asked', 'off'];

/** What stands where nothing is set. */
export const GROWTH_DEFAULT: GrowthValue = 'mode';

/** A value the page knows, else the default's, where the driver answered one this page does not. */
export const growthValue = (value: string | null | undefined): GrowthValue =>
  (GROWTH_VALUES as readonly string[]).includes(value ?? '') ? (value as GrowthValue) : GROWTH_DEFAULT;

/** What each door says of a value, in the page's language: the terminal's sentence for it (`daoris settings growth`). */
export function growthSays(t: TFunction, value: string | null | undefined): string {
  return t(`settings.growth.says.${growthValue(value)}`);
}

/** What a Setup row says of the value standing: the door's sentence, then where it is set, the default included. */
export function growthRowSays(t: TFunction, value: string | null | undefined, from: { source?: GrowthSource | null; workspace?: string }): string {
  return [
    growthSays(t, value),
    from.source === 'repository' ? t('settings.growth.from.repository')
      : from.source === 'workspace' ? t('settings.growth.from.workspace', { workspace: from.workspace })
        : t('settings.growth.from.default'),
  ].join(t('projects.setup.sentenceJoin'));
}

/** A value in a folded section's line. */
export function growthSummary(t: TFunction, value: string | null | undefined): string {
  return t(`settings.growth.summary.${growthValue(value)}`);
}

/** What a change said once the driver took it. */
export function growthToast(t: TFunction, name: string, value: GrowthValue): string {
  return t(`settings.growth.saved.${value}`, { name });
}

/**
 * **The growth switch's row**, *New repositories and rooms*: its three values as one choice, the value standing for the place and where it is
 * set. A press goes out as the edit at once, judged by the driver, which refuses what is no value; choosing the value that stands sends nothing.
 * Props only (components §2).
 */
export function GrowthSetting({ name, twin, value, source, workspace, busy = false, onChange }: {
  /** The repository or workspace, for the control's accessible name. */
  name: string;
  /** The terminal twin of the row: a repository's, or a workspace's. */
  twin: string;
  /** The value standing for this place. */
  value: string | null | undefined;
  /** Where it is set; the workspace's own row reads `workspace` where one is set. */
  source?: GrowthSource | null;
  /** The workspace a repository's value may come from, named in the row's words. */
  workspace?: string;
  busy?: boolean;
  onChange: (value: GrowthValue) => void;
}) {
  const { t } = useTranslation();
  const standing = growthValue(value);
  return (
    <SettingRow label={t('settings.growth.label')} hint={twin} why={t('settings.growth.body')}
      control={(
        <Segmented
          label={t('settings.growth.choice', { name })}
          value={standing}
          options={GROWTH_VALUES.map((each) => ({ value: each, label: t(`settings.growth.values.${each}`) }))}
          onChange={(chosen) => {
            if (busy || chosen === standing) return;
            onChange(chosen);
          }}
        />
      )}
    >
      <p className="m-0 text-small text-ink-soft">{growthRowSays(t, standing, { source, workspace })}</p>
    </SettingRow>
  );
}
