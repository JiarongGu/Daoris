import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, Pill, Prose, SelectField } from '../../design/ui';
import { CheckField } from '../../design/atoms/FormControls';
import { PageSection } from '../../work/frame/ViewMain';
import { FILES_KINDS, type FilesKind, type FilesLook, type FilesPress } from './filesLook';

/**
 * **The person's own rules** for a repository's files: each with its remove, and *Add a rule* one press away. A rule goes to the end
 * of the person's rules, so it wins over the repository's and the found ones, and a later one over the same paths changes it.
 */
export function FilesRules({ look, onPress }: { look: FilesLook; onPress?: (press: FilesPress) => void }) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<FilesKind>('scratch');
  const [paths, setPaths] = useState('');
  const [copy, setCopy] = useState(false);
  const [machine, setMachine] = useState(false);

  return (
    <PageSection title={t('files.view.rules')}>
      <div className="grid gap-3">
        {look.rules.length === 0
          ? <Prose className="text-small text-ink-soft">{t('files.view.rulesNone')}</Prose>
          : (
            <ul aria-label={t('files.view.rules')} className="m-0 grid list-none gap-1 p-0 text-small">
              {look.rules.map((rule) => (
                <li key={`${rule.scope}:${rule.kind}:${rule.paths.join(',')}`} className="flex min-w-0 flex-wrap items-center gap-2 border-t border-line pt-1 first:border-t-0 first:pt-0">
                  <Pill>{t(`files.view.kind.${rule.kind}`)}</Pill>
                  <span className="min-w-0 flex-1 font-mono [overflow-wrap:anywhere]">{rule.paths.join(', ')}</span>
                  <span className="text-meta text-ink-faint">
                    {rule.scope === 'machine' ? t('files.view.scope.machine') : t('files.view.scope.repository')}
                    {rule.copy != null ? ` · ${t(rule.copy ? 'files.view.copies' : 'files.view.noCopies')}` : ''}
                  </span>
                  {onPress && (
                    <Button
                      variant="ghost" aria-label={t('files.view.removeRule', { paths: rule.paths.join(', ') })}
                      onClick={() => onPress({ act: 'remove', paths: rule.paths, machine: rule.scope === 'machine' })}
                    >
                      <Icon name="x" size={12} />
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        {onPress && !adding && <div><Button variant="ghost" onClick={() => setAdding(true)}><Icon name="plus" size={13} />{t('files.view.addRule')}</Button></div>}
        {onPress && adding && (
          <form
            className="flex min-w-0 flex-wrap items-center gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              const list = paths.split(/[\s,]+/).filter(Boolean);
              if (list.length === 0) return;
              onPress({ act: 'set', kind, paths: list, copy: kind === 'local' ? copy : undefined, machine });
              setPaths('');
              setAdding(false);
            }}
          >
            <SelectField
              value={kind} onChange={(value) => setKind(value as FilesKind)} ariaLabel={t('files.view.kindLabel')}
              options={FILES_KINDS.map((name) => ({ value: name, label: t(`files.view.kind.${name}`) }))}
            />
            <input
              value={paths} autoFocus aria-label={t('files.view.pathsLabel')} placeholder="local/"
              onChange={(event) => setPaths(event.target.value)} className="min-w-0 flex-1 rounded-control border border-line bg-raised px-2 py-1 font-mono text-small"
            />
            {kind === 'local' && <CheckField checked={copy} onChange={setCopy} label={t('files.view.copyLabel')} />}
            <CheckField checked={machine} onChange={setMachine} label={t('files.view.machine')} />
            <Button type="submit">{t('files.view.add')}</Button>
          </form>
        )}
      </div>
    </PageSection>
  );
}
