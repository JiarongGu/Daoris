import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { PressReceiptLine } from './PressReceiptLine';

// What a press that opened a place made, said once under its turn: a department's folder, line and set-up quest with the repository's page and the
// quest one press away, a folder department's folder alone, a creation that stopped, and a room's one quest. At the panel's floor, in English and
// 中文, light and dark.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];

const meta = {
  title: 'Help/PressReceiptLine',
  component: PressReceiptLine,
  args: {
    receipt: { kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: 'D:/work/aurora/ledger', line: 'main', room: null, quests: ['q41'], done: true },
    onOpen: () => {},
  },
  decorators: [storyWidths.narrow],
} satisfies Meta<typeof PressReceiptLine>;
export default meta;

type Story = StoryObj<typeof meta>;

/** A department opened, with the places it made one press away. */
export const Opened: Story = {};

export const OpenedChineseDark: Story = { decorators: [storyWidths.narrow, chinese, ...inTheme('dark')] };

/** A folder department: no line and no quest. */
export const FolderDepartment: Story = { args: { receipt: { kind: 'opened', repository: 'notes', workspace: 'aurora', folder: 'D:/work/aurora/notes', line: null, room: null, quests: [], done: true } } };

/** A creation that stopped, which says to press the same again. */
export const Stopped: Story = { args: { receipt: { kind: 'opened', repository: 'ledger', workspace: 'aurora', folder: 'D:/work/aurora/ledger', line: 'main', room: null, quests: [], done: false } } };

/** A room is its quest's line alone. */
export const Room: Story = { args: { receipt: { kind: 'room', repository: 'engine', workspace: null, folder: null, line: null, room: 'tools/web', quests: ['q42'], done: true } } };

export const RoomChinese: Story = { args: Room.args, decorators: [storyWidths.narrow, chinese] };

/** With nothing to open a place the line is words alone. */
export const WordsAlone: Story = { args: { onOpen: undefined } };
