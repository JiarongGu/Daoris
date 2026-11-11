import { useTranslation } from 'react-i18next';
import { GrantTools, type PressGrant } from './view/GrantTools';
import { PluginRuns, type PressPlugin } from './view/PluginRuns';
import { type PressRules, RulesChange } from './view/RulesChange';
import { type PressSupervisor, SupervisorChange } from './view/SupervisorChange';

/**
 * What a terminal's press means, as its route's view read it at the ask (`PressView`): the kind its sentence is worded by
 * (`person.view.<kind>`), the values that sentence names, the `daoris` command the press is, and any further sentences it means.
 * What only one route's card draws rides beside it as fields the page words: a place's supervisor rules, a supervisor's record, what a
 * plugin runs, every tool an approval of a plugin's server names.
 */
export type PressView = {
  kind: string;
  facts: Record<string, string>;
  command: string;
  also?: { kind: string; facts: Record<string, string> }[] | null;
  rules?: PressRules | null;
  supervisor?: PressSupervisor | null;
  plugin?: PressPlugin | null;
  grant?: PressGrant | null;
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
      {view.rules && <RulesChange rules={view.rules} />}
      {view.supervisor && <SupervisorChange supervisor={view.supervisor} />}
      {view.plugin && <PluginRuns plugin={view.plugin} />}
      {view.grant && <GrantTools grant={view.grant} />}
      <code className="break-all text-small text-ink-soft">{view.command}</code>
    </div>
  );
}
