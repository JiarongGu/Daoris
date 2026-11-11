import { type KeyboardEvent, useState } from 'react';
import { type ComboKeys } from '../../design/atoms/ChipCombobox';
import { type FocusRow } from './focusPlaces';

/**
 * The workspaces a picker of places has unfolded onto their repositories, and its keys, the focus picker's and the history's alike:
 * → unfolds the active workspace at the field's end; ← folds it at its start, or goes from a repository up to its workspace.
 */
export function useFolds() {
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set());
  /** The keys over the rows a picker draws, with the folds it holds now. */
  const onKeyOver = (rowsFor: (typed: string) => FocusRow[]) => (event: KeyboardEvent<HTMLInputElement>, keys: ComboKeys) => {
    if (keys.active === null || event.currentTarget.value.trim()) return false;
    // The first row of the value is the active one, as the combobox finds it.
    const row = rowsFor('').find((each) => each.key === keys.active);
    if (!row) return false;
    const workspace = row.place.kind === 'machine' ? null : row.place.workspace.toLowerCase();
    if (event.key === 'ArrowRight' && keys.atEnd && row.place.kind === 'workspace' && !row.open) {
      setOpen(new Set([...open, workspace!]));
      return true;
    }
    if (event.key !== 'ArrowLeft' || !keys.atStart) return false;
    if (row.place.kind === 'repository' && row.nested) {
      keys.setActive(rowsFor('').find((each) => each.place.kind === 'workspace' && !each.nested
        && each.place.workspace.toLowerCase() === workspace)?.key ?? null);
      return true;
    }
    if (row.place.kind === 'workspace' && row.open) {
      setOpen(new Set([...open].filter((each) => each !== workspace)));
      return true;
    }
    return false;
  };
  return { open, setOpen, onKeyOver };
}
