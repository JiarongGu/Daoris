import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import type { CodeDependency, CodeModule, Provenance } from '../api';
import { CodeMapCanvas, CodeMapDetail } from './CodeMap';

// A repository's own code map (MAP3a), in the shapes a producer writes: a small layered service, a
// map with a recorded cycle, and ids too long for their boxes.

const MODULES: CodeModule[] = [
  { id: 'cli', path: 'src/Cli', summary: 'the npm package: commands and the canon' },
  { id: 'web', path: 'src/Web', summary: 'the platform, the only UI' },
  { id: 'desktop', path: 'src/Desktop', summary: 'the local driver and its window' },
  { id: 'service', path: 'src/Service', summary: 'indexes the family, reachable over MCP' },
  { id: 'devkit', path: 'src/Devkit', summary: 'five universal gates' },
];

const DEPENDENCIES: CodeDependency[] = [
  { from: 'desktop', to: 'web', kind: 'hosts' },
  { from: 'desktop', to: 'service', kind: 'http' },
  { from: 'web', to: 'service', kind: 'http' },
  { from: 'cli', to: 'service', kind: 'http' },
];

function Both({ modules, dependencies, fed, initial = 'service' }: {
  modules: CodeModule[]; dependencies: CodeDependency[]; fed?: Provenance; initial?: string | null;
}) {
  const [selected, setSelected] = useState<string | null>(initial);
  return (
    <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <CodeMapCanvas repository="daoris" modules={modules} dependencies={dependencies} selected={selected} onSelect={setSelected} />
      <CodeMapDetail
        modules={modules} dependencies={dependencies} selected={selected} file="docs/code-map.json" fed={fed}
        onSelect={setSelected}
      />
    </div>
  );
}

const meta: Meta<typeof Both> = {
  title: 'Map/CodeMap',
  component: Both,
  args: { modules: MODULES, dependencies: DEPENDENCIES },
};
export default meta;

type Story = StoryObj<typeof Both>;

/** Five artefacts and what reaches what. */
export const Layered: Story = {};

/** A producer recorded a cycle: every module is still drawn once, and one arrow points up. */
export const WithACycle: Story = {
  args: { dependencies: [...DEPENDENCIES, { from: 'service', to: 'desktop', kind: 'callback' }] },
};

/**
 * A teammate's map (MAP3e): no checkout here, so it came down with the sync, and the detail says
 * from which commit and whose key — before a module is chosen, where the file line sits.
 */
export const FromATeammate: Story = {
  args: {
    initial: null,
    fed: {
      commit: 'c0ffee1234567890', shortCommit: 'c0ffee12', committedAt: '2026-09-24T08:00:00Z',
      branch: 'main', origin: 'person@machine-b',
    },
  },
};

/** Ids longer than a box, cut on the drawing and whole in the detail. */
export const LongIds: Story = {
  args: {
    modules: [
      { id: 'Daoris.Desktop.Driver.Host', path: 'src/Desktop/Host', summary: 'the headless driver' },
      { id: 'Daoris.Desktop.Modules', path: 'src/Desktop/Modules', summary: 'the IPC surfaces' },
    ],
    dependencies: [{ from: 'Daoris.Desktop.Driver.Host', to: 'Daoris.Desktop.Modules', kind: 'project' }],
  },
};
