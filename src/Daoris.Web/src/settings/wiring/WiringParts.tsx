import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Card, SectionTitle } from '../../design/ui';

// The pieces This Daoris's cards share (WIRING1c): a fact not read says why in its own words, and a card is one titled section
// with a press at its right. Molecules: drawn from props, so every state is reachable without a shell (D52 as amended).

/**
 * A fact the reader could not give, in the words of its `…Why` code (the this-Daoris design §6: a fact not read is `null` beside
 * a code). A code the catalogue has no word for says it could not be read, so a code the driver adds later shows a sentence
 * and not its own spelling.
 */
export function Unread({ why }: { why: string | null | undefined }) {
  const { t, i18n } = useTranslation();
  const key = `settings.daoris.why.${why}`;
  return <span className="text-small text-ink-faint">{why && i18n.exists(key) ? t(key) : t('settings.daoris.why.unknown')}</span>;
}

/** A value where there is one, else why there is none: the row's control in either case. */
export function Fact({ value, why, children }: { value: unknown; why: string | null | undefined; children: ReactNode }) {
  return value === null || value === undefined ? <Unread why={why} /> : <>{children}</>;
}

/**
 * A fold under a card's rows (the records, the variables not set): shut until pressed, so the card leads with what is there.
 * Its own press is the only state it holds.
 */
export function Fold({ label, children }: { label: string; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="border-t border-line pt-2.5">
      <Button variant="ghost" aria-expanded={open} onClick={() => setOpen(!open)}>{label}</Button>
      {open && <div className="mt-1.5">{children}</div>}
    </div>
  );
}

/** A door to where a thing is set: a press that goes there and changes nothing. */
export function Door({ label, onPress }: { label: string; onPress: () => void }) {
  return <Button variant="ghost" onClick={onPress}>{label}</Button>;
}

/** One card of the page: its title as a heading, a press at its right where it has one, and its rows under. */
export function WiringCard({ id, title, aside, children }: { id: string; title: string; aside?: { label: string; busy?: boolean; onPress: () => void }; children: ReactNode }) {
  return (
    <Card id={id} className="scroll-mt-3">
      <section aria-label={title}>
        <div className="flex items-baseline justify-between gap-4">
          <SectionTitle>{title}</SectionTitle>
          {aside && <Button variant="ghost" disabled={aside.busy} onClick={aside.onPress}>{aside.label}</Button>}
        </div>
        <div>{children}</div>
      </section>
    </Card>
  );
}
