import { type TFunction } from 'i18next';
import type { DoorSheet, SheetRow } from '../tools';

/** What the sheet says of a value: `yes` and `no` in the page's words, an agent's own words as they came. */
const valueWord = (t: TFunction<'translation', undefined>, row: SheetRow) =>
  row.value === 'yes' ? t('agents.sheet.yes') : row.value === 'no' ? t('agents.sheet.no') : row.value ?? '';

/**
 * One door's sheet on the agent's page (COAGENT1 §3): each capability the driver answered, with the value and how it is known,
 * or *not known* with the fallback that applies. The door's own row is the wire word the header already says, so it is not
 * repeated; a door with no rows (an older shell's roster) draws nothing.
 */
export function DoorSheetLines({ t, sheet }: { t: TFunction<'translation', undefined>; sheet: DoorSheet | undefined }) {
  const rows = (sheet?.rows ?? []).filter((row) => row.capability !== 'door');
  if (rows.length === 0) return null;
  return (
    <ul aria-label={t('agents.sheet.label', { door: sheet!.door })} className="m-0 mt-2 list-none space-y-0.5 p-0 text-small text-ink-soft">
      {rows.map((row) => (
        <li key={row.capability}>
          <span className="text-ink">{t(`agents.sheet.cap.${row.capability}`)}</span>
          {row.source === 'absent'
            ? <span className="text-ink-faint">{t('agents.sheet.absent', { fallback: row.fallback })}</span>
            : <span>{t('agents.sheet.known', { value: valueWord(t, row), source: t(`agents.sheet.source.${row.source}`) })}</span>}
        </li>
      ))}
    </ul>
  );
}
