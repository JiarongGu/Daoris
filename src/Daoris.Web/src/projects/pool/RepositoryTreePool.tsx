import { useShenora } from '@shenora/react';
import { useTreePool } from '../../bridge/repositories/treePool';
import { TreePoolSection } from './TreePoolSection';

/** The repository's machine-local pool reading, mounted only on its Details page. */
export function RepositoryTreePool({ workspace, repository }: { workspace: string; repository: string }) {
  const { isAvailable } = useShenora();
  const reading = useTreePool(workspace, repository);
  if (!isAvailable) return null;
  return <TreePoolSection slots={reading.data} failed={reading.isError} />;
}
