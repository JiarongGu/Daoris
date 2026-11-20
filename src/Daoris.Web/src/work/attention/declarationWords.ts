import i18n from '../../locales/i18n';
import type { DeclarationCommandsChange, IntegrationWait } from './attentionPushed';

/** A declaration owed the person's acceptance, said with the mode that decided: only restricted access owes one. */
export function declarationOwedWhy(wait: Pick<IntegrationWait, 'state' | 'line' | 'tip' | 'change'>): string {
  const why = wait.state === 'changed'
    ? declarationChangedWhy(wait.line ?? '', wait.change)
    : i18n.t('work.attention.integrationDeclarationUnacceptedWhy', { line: wait.line ?? '', tip: (wait.tip ?? '').slice(0, 7) });
  return i18n.t('work.attention.integrationDeclarationRestricted', { why });
}

/**
 * A line's declaration changed since its acceptance, in the reader's words: the checks added, removed and changed by name and the other
 * parts by key, as the driver read them against the accepted one; a change it did not read is said without them, and one whose text
 * does not read strictly says so. Names and keys are content, shown as written.
 */
export function declarationChangedWhy(line: string, change: DeclarationCommandsChange | null | undefined): string {
  if (change?.unread) return i18n.t('work.attention.integrationDeclarationUnreadWhy', { line });
  const named = (names: string[]) => names.map((name) => `\`${name}\``).join(i18n.t('work.attention.integrationDeclarationJoin'));
  const parts = change ? [
    change.added.length ? i18n.t('work.attention.integrationDeclarationAdded', { names: named(change.added.map((each) => each.name)) }) : null,
    change.removed.length ? i18n.t('work.attention.integrationDeclarationRemoved', { names: named(change.removed.map((each) => each.name)) }) : null,
    change.changed.length ? i18n.t('work.attention.integrationDeclarationAltered', { names: named(change.changed.map((each) => each.name)) }) : null,
    change.other.length ? i18n.t('work.attention.integrationDeclarationOther', { keys: named(change.other), count: change.other.length }) : null,
  ].filter((part): part is string => part !== null) : [];
  return parts.length === 0
    ? i18n.t('work.attention.integrationDeclarationChangedWhy', { line })
    : i18n.t('work.attention.integrationDeclarationCommandsWhy', { line, commands: parts.join(i18n.t('work.attention.integrationDeclarationParts')) });
}
