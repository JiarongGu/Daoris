import { useEffect } from 'react';
import type { ListView } from '../work/layout';
import type { ListPanes } from '../work/listPanes';

/**
 * A door that names an item as it opens a view (D118 §3i), as the application's opener does: the list chooses it now, so
 * it opens whatever became of it. A choice put in the list's keys before a view mounts is a remembered one instead, which
 * the view reopens only while its item still waits (UX6b, design §1 rule 6).
 */
export function useDoor(lists: ListPanes, view: ListView, door: string | undefined) {
  const { choose } = lists;
  useEffect(() => {
    if (door) choose(view, door);
  }, [choose, view, door]);
}
