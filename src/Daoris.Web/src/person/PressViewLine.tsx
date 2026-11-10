import { useTranslation } from 'react-i18next';

/**
 * What a terminal's press means, as its route's view read it at the ask (`PressView`): the kind its sentence is worded by
 * (`person.view.<kind>`), the values that sentence names, the `daoris` command the press is, and any further sentences it means.
 */
export type PressView = {
  kind: string;
  facts: Record<string, string>;
  command: string;
  also?: { kind: string; facts: Record<string, string> }[] | null;
};

/** A press's view on its card: its sentence in the window's language, then the command it is, above every value it carries. */
export function PressViewLine({ view }: { view: PressView }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-0.5">
      <p className="m-0">{t(`person.view.${view.kind}`, view.facts)}</p>
      {(view.also ?? []).map((said, index) => (
        <p key={index} className="m-0">{t(`person.view.${said.kind}`, said.facts)}</p>
      ))}
      <code className="break-all text-small text-ink-soft">{view.command}</code>
    </div>
  );
}
