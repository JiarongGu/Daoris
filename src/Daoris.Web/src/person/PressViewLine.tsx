import { useTranslation } from 'react-i18next';

/**
 * What a terminal's press means, as its route's view read it at the ask (`PressView`, D261): the kind its sentence is worded by
 * (`person.view.<kind>`), the values that sentence names, and the `daoris` command the press is.
 */
export type PressView = { kind: string; facts: Record<string, string>; command: string };

/** A press's view on its card: its sentence in the window's language, then the command it is, above every value it carries. */
export function PressViewLine({ view }: { view: PressView }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-0.5">
      <p className="m-0">{t(`person.view.${view.kind}`, view.facts)}</p>
      <code className="break-all text-small text-ink-soft">{view.command}</code>
    </div>
  );
}
