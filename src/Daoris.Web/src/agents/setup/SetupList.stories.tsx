import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { SetupList } from './SetupList';
import { PLACES } from './setupFixtures';
import { repositoryId } from './SetupPlaces';
import type { SetupState } from './setupTreeModel';

// The places tree: second lines (how a place lands, held, adoption running, a need with its first
// reason), the *Needs attention* chip with its count, and a fold open on a page of imported repositories. At the usual frame widths, in
// English and 中文, light and dark. The keys are the WAI tree pattern's: Tab enters on one row, then ↑ ↓ Home End → ← walk and open.

const nothing = () => {};

/** The fixture's answer with a repository whose start is held and one whose adoption runs, so each kind of second line is drawn. */
const read = (fold: string | null) => {
  const base = PLACES(fold ? { fold } : {});
  const [engine] = base.repositories;
  return {
    ...base,
    repositories: [
      ...base.repositories,
      { ...engine!, repository: 'billing', held: true },
      { ...engine!, repository: 'portal', driven: false, trees: false, adoption: 'running' as const, values: null },
    ],
  };
};

function Tree({ fold = null, needsLook = false, search = '' }: { fold?: string | null; needsLook?: boolean; search?: string }) {
  const [chosen, setChosen] = useState<string | null>(repositoryId('aurora', 'engine'));
  const all = read(fold);
  // A search narrows the rows the answer holds; the chosen place is kept by the view and stays chosen though its row is not drawn.
  const narrowed = search ? { ...all, repositories: all.repositories.filter((row) => row.repository?.includes(search)) } : all;
  const state: SetupState = {
    search, onSearch: nothing, needsLook, onNeedsLook: nothing, fold, onFold: nothing, foldAfter: null, onFoldAfter: nothing,
    onMore: nothing, read: narrowed, status: 'ok',
  };
  return <SetupList state={state} chosen={chosen} onChoose={setChosen} />;
}

const frames = atWidths();
const meta: Meta<typeof Tree> = { title: 'Agents/SetupTree', component: Tree, decorators: [frames, inLight] };
export default meta;

type Story = StoryObj<typeof Tree>;

/** A workspace with its driven repositories, each with its second line, and the imported ones folded into one row. */
export const Folded: Story = {};
export const FoldedDark: Story = { decorators: [frames, inDark] };
export const FoldedChinese: Story = { decorators: [chinese, frames, inLight] };
export const FoldedChineseDark: Story = { decorators: [chinese, frames, inDark] };

/** The fold open on a page of imported repositories, a level below the fold's row. */
export const FoldOpen: Story = { args: { fold: 'aurora' } };
export const FoldOpenChinese: Story = { args: { fold: 'aurora' }, decorators: [chinese, frames, inLight] };

/** The chip pressed: it narrows the places to those that need a look. */
export const NeedsAttention: Story = { args: { needsLook: true } };
export const NeedsAttentionChinese: Story = { args: { needsLook: true }, decorators: [chinese, frames, inLight] };

/** A search that leaves the chosen place's row out: the tree shows the rows that match, and the place stays the page's (the kept selection). */
export const SearchKeepsTheChoice: Story = { args: { search: 'portal' } };
export const SearchKeepsTheChoiceChinese: Story = { args: { search: 'portal' }, decorators: [chinese, frames, inLight] };
