import { useTranslation } from 'react-i18next';
import { placeSaid, ruleSaid } from '../agents/roles/SupervisorRulesFold';
import { list } from '../locales/format';

/** One supervisor rule as a card carries it: the rules' own fields, which the page words from its catalogue. */
export type HelpRuleShown = { count: string; at: number; do: string; upTo?: number | null; roles?: string[] | null };

/** What the card of a place's supervisor rules shows: the place and its rules before and after, as fields. */
export type HelpRulesShown = { place: string; before: HelpRuleShown[]; after: HelpRuleShown[] };

/** The rules of a place before and after a press, in the words of the supervisor's fold, in the person's language. */
export function RulesChange({ rules }: { rules: HelpRulesShown }) {
  const { t } = useTranslation();
  const said = (rule: HelpRuleShown) => {
    const count = t(`settings.supervisor.count.${rule.count}`);
    const roles = rule.roles?.length ? list(rule.roles) : t('settings.supervisor.everyRole');
    return t('help.proposal.rules.rule', { count, rule: ruleSaid(t, { at: rule.at, do: rule.do, upTo: rule.upTo ?? undefined }), roles });
  };
  const rows = (label: string, rule: HelpRuleShown[], side: 'before' | 'after') => (
    <section aria-label={label} data-side={side} className="m-0 mt-1">
      <p className="m-0 text-meta font-semibold text-ink-soft">{label}</p>
      {rule.length === 0
        ? <p className="m-0 text-meta text-ink-faint">{t('help.proposal.rules.none')}</p>
        : (
          <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
            {rule.map((each) => <li key={`${each.count}:${each.roles?.join(',') ?? ''}`} className="m-0 text-meta text-ink-soft">{said(each)}</li>)}
          </ul>
        )}
    </section>
  );
  return (
    <div>
      <p className="m-0 mt-1 text-small text-ink">{t('help.proposal.rules.title', { place: placeSaid(t, rules.place) })}</p>
      {rows(t('help.proposal.rules.before'), rules.before, 'before')}
      {rows(t('help.proposal.rules.after'), rules.after, 'after')}
      <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.rules.reads')}</p>
    </div>
  );
}
