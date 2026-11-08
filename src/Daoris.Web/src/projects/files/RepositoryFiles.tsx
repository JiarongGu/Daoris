import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useFiles, useFilesPress } from '../../bridge/repositories/files';
import type { FilesPress } from './filesLook';
import { FilesTab } from './FilesTab';

/** A repository's Files tab with its reading and presses: read only while the tab draws it, the person's presses the window's. */
export function RepositoryFiles({ repository }: { repository: string }) {
  const { t } = useTranslation();
  const reading = useFiles(repository);
  const press = useFilesPress(repository);
  const [said, setSaid] = useState<string | null>(null);
  const refusal = reading.error ? String(reading.error.message) : reading.data?.refusal ? t(`files.view.cannot.${reading.data.refusal}`) : null;

  const send = (made: FilesPress) => press.mutate(made, {
    onSuccess: (answer) => setSaid(answer.done || answer.why === 'unchanged' ? null : t(`files.view.refusedPress.${answer.why ?? 'refused'}`, { message: answer.message ?? '' })),
  });

  return (
    <FilesTab
      repository={repository}
      look={reading.data?.look}
      reading={reading.isPending}
      refusal={refusal}
      said={said}
      onPress={send}
    />
  );
}
