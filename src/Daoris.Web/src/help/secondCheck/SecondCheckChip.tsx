import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type ReadingsCheck } from './readingTypes';

/**
 * The second check beside the access chip: *Second check on · 2 pending* (this conversation's ranges queued or being read),
 * *Second check on*, or *Second check off*; its popover says today's readings on the machine and the tokens their harnesses
 * reported, those that reported none said apart. It never says a reader read the conversation: coverage is per turn, on its line.
 */
export function SecondCheckChip({ check }: { check: ReadingsCheck | null }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const id = useId();
  if (!check) return null;
  const said = !check.on ? t('help.secondCheck.chipOff')
    : check.pending > 0 ? t('help.secondCheck.chipPending', { count: check.pending }) : t('help.secondCheck.chipOn');
  return (
    <span data-second-check={check.on ? 'on' : 'off'} className="relative inline-grid">
      <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-spend`} onClick={() => setOpen((was) => !was)}>{said}</Button>
      {open && (
        <span id={`${id}-spend`} role="note" className="grid gap-0.5 rounded-control border border-line bg-sunken px-3 py-2 text-small text-ink-soft">
          <span>{t('help.secondCheck.today', { count: check.today.readings })}</span>
          <span>{t('help.secondCheck.tokens', { tokens: check.today.tokens })}</span>
          {check.today.unknown > 0 && <span>{t('help.secondCheck.tokensUnknown', { count: check.today.unknown })}</span>}
        </span>
      )}
    </span>
  );
}
