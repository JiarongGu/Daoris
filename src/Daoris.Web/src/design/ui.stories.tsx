import type { Meta, StoryObj } from '@storybook/react-vite';
import {
  Button, Card, CardStack, CheckField, Chip, CodeText, Dot, Drawer, EmptyState, FormField, FormGrid, FormRow, Icon, Menu, MetaLine,
  MonoWell, PathText, Pill, SettingList, SettingRow, SkeletonRows, Tile, Toasts,
} from './ui';
import { InTheme } from '../plugins/storyIcons';
import { storyWidths } from './storyWidths';
import { InlineConfirm } from '../work/InlineConfirm';

// Every state of every primitive, on the shipped components — including the states real data rarely
// shows. This is where the design is reviewed and kept (D42); the product cannot drift from it,
// because it IS the product's code.

const meta: Meta = { title: 'Design/Components' };
export default meta;

export const Buttons: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <Button variant="primary">publish quest</Button>
      <Button>take</Button>
      <Button variant="ghost">decline…</Button>
      <Button variant="danger">decline with this reason</Button>
      <Button disabled>disabled</Button>
    </div>
  ),
};

export const Pills: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-2">
      <Pill tone="open" title="published — nobody has taken it">Open</Pill>
      <Pill tone="taken">Taken</Pill>
      <Pill tone="done">Done</Pill>
      <Pill tone="declined">Declined</Pill>
      <Pill tone="declined">sat 12d</Pill>
      <Pill>Canonical</Pill>
      <Chip>the engine runtime — simulation, rendering, assets</Chip>
      <Chip accent>a failing case</Chip>
    </div>
  ),
};

/**
 * Red drawn as words (UXFIX5): a danger button's label, a declined pill's word, a failed mark's word, a refusal and a
 * diff's removed count, in the danger ink on each of the four surfaces; the line, the field and the mark stay declined's
 * hue. `tokens.test.ts` computes each at 4.5:1 or more; these are where the eye checks it.
 */
function DangerWords() {
  const surfaces = [['bg-page', 'page'], ['bg-sunken', 'sunken'], ['bg-raised', 'raised'], ['bg-overlay', 'overlay']] as const;
  return (
    <div className="grid gap-2">
      {surfaces.map(([surface, name]) => (
        <div key={name} className={`flex flex-wrap items-center gap-3 rounded-card border border-line p-3 ${surface}`}>
          <span className="w-14 font-mono text-meta text-ink-faint">{name}</span>
          <Button variant="danger">Remove plugin</Button>
          <Pill tone="declined">Declined</Pill>
          <Dot tone="failed" label="failed" />
          <span className="font-mono text-meta text-ink-danger">−12</span>
          <p className="m-0 text-small text-ink-danger">The driver stopped: sessions will not start until it runs again.</p>
        </div>
      ))}
    </div>
  );
}

export const Danger: StoryObj = { render: () => <DangerWords /> };

/** The same in dark, where declined's hue as text read 3.2 to 3.95:1 and the danger ink reads 4.6 or more. */
export const DangerDark: StoryObj = { render: () => <InTheme theme="dark"><DangerWords /></InTheme> };

/**
 * Every other status hue drawn as words (UXFIX5c): each pill's word, a waiting mark's word, a diff's added count and its
 * letters, and a sentence, in each hue's ink on the four surfaces; the line, the field and the mark stay the hue.
 * `tokens.test.ts` computes each at 4.6:1 or more on its own field; these are where the eye checks it.
 */
function StatusWords() {
  const surfaces = [['bg-page', 'page'], ['bg-sunken', 'sunken'], ['bg-raised', 'raised'], ['bg-overlay', 'overlay']] as const;
  return (
    <div className="grid gap-2">
      {surfaces.map(([surface, name]) => (
        <div key={name} className={`flex flex-wrap items-center gap-3 rounded-card border border-line p-3 ${surface}`}>
          <span className="w-14 font-mono text-meta text-ink-faint">{name}</span>
          <Pill tone="open">Open</Pill>
          <Pill tone="taken">Taken</Pill>
          <Pill tone="done">Done</Pill>
          <Dot tone="parked" label="waiting on you" />
          <span className="font-mono text-meta">
            <span className="text-ink-done">A</span> <span className="text-ink-taken">M</span>{' '}
            <span className="text-ink-open">R</span> <span className="text-ink-done">+12</span>
          </span>
          <p className="m-0 text-small text-ink-done">Every step passed.</p>
        </div>
      ))}
    </div>
  );
}

export const StatusInks: StoryObj = { render: () => <StatusWords /> };

/** The same in dark, where the hues as words read 4.1 to 4.4:1 on their fields over an overlay, and the inks 4.6 or more. */
export const StatusInksDark: StoryObj = { render: () => <InTheme theme="dark"><StatusWords /></InTheme> };

/**
 * A setting is a row (2026-09-23), and a card's rows share one label track (the UI/UX design R1, UIFIT1): a switch, a
 * number with the notice its zero earned under it, a path with the row's own act after it, a long label that wraps at the
 * track's cap, a short value, and a row with no control. Each value starts at the track's edge; in a main area under
 * 32rem the track goes and each value sits under its words. In English and 中文, at the frame's three widths.
 */
const numberField = <input type="number" defaultValue={0} className="w-[4.5rem] rounded-control border border-line-strong bg-raised px-2.5 py-1 text-right text-body text-ink" />;

function SettingRowsCards({ zh = false }: { zh?: boolean }) {
  return (
    <CardStack>
      <Card>
        <SettingList>
          <SettingRow
            label={zh ? '会话停泊或意外结束时通知我' : 'Tell me when a session parks or ends unasked'}
            hint="Also `daoris driver notify on|off` — one file, either door."
            why="Nobody should have to watch a driver."
            control={<CheckField hideLabel checked onChange={() => {}} label="Tell me when a session parks or ends unasked" />}
          />
          <SettingRow
            label={zh ? '委托在多少次失败会话后停泊' : 'Park a quest after this many failed sessions'}
            hint="Also `daoris driver strikes <n>`; stand-downs, declines and stops never count."
            why="One quest whose session could not start ran eighteen times, and every run spent an account."
            control={numberField}
          >
            <p className="m-0 border-l-[3px] border-warn bg-page/60 px-3.5 py-2 text-body text-ink-soft">
              {zh ? '零表示一直重试。' : 'Zero means it keeps trying.'}
            </p>
          </SettingRow>
          <SettingRow
            label={zh ? 'Daoris 主目录' : 'Daoris home'}
            hint="Every file on this page lives under it."
            control={(
              <>
                <PathText path="C:/somewhere/Daoris/data" className="text-small text-ink" />
                <Button>{zh ? '打开本机' : 'Open This Daoris'}</Button>
              </>
            )}
          />
          <SettingRow
            label={zh ? '没有指定时间时的冷却时长，按分钟计' : 'Cool-off when no time is named, and the label is long enough to wrap at the cap'}
            hint="Also `daoris driver cool-off <minutes>`."
            control={<span className="inline-flex items-center gap-1.5">{numberField}<span className="text-small text-ink-soft">min</span></span>}
          />
          <SettingRow label={zh ? '版本' : 'Version'} control={<Pill>0.22.0</Pill>} />
          <SettingRow label={zh ? '上次交换' : 'Last swap'}>
            <p className="m-0 text-small text-ink-soft">{zh ? '已安装 0.21.0。' : 'Installed 0.21.0.'}</p>
          </SettingRow>
        </SettingList>
      </Card>
      <Card>
        <SettingList>
          <SettingRow label={zh ? '完全访问' : 'Full access'} control={<CheckField hideLabel checked={false} onChange={() => {}} label="Full access" />}>
            <p className="m-0 text-small text-ink-soft">{zh ? '关闭：以下每个暂停按设置生效。' : 'Off: each pause below as set.'}</p>
          </SettingRow>
          <SettingRow label={zh ? '权限提示' : 'Permission prompts'} control={<><span className="text-small text-ink">auto</span><Button variant="ghost">{zh ? '为本机设置' : 'Set for this machine'}</Button></>} />
        </SettingList>
      </Card>
    </CardStack>
  );
}

export const SettingRows: StoryObj = { render: () => <SettingRowsCards />, decorators: [storyWidths.wide] };

/** The same at the half width, 630 px: the track still beside, at most half the card. */
export const SettingRowsHalf: StoryObj = { render: () => <SettingRowsCards />, decorators: [storyWidths.half] };

/** The same at the 400 px floor: the track goes, each value under its words. */
export const SettingRowsNarrow: StoryObj = { render: () => <SettingRowsCards />, decorators: [storyWidths.narrow] };

/** The same rows in 中文: shorter labels, the track at its floor. */
export const SettingRowsChinese: StoryObj = { render: () => <SettingRowsCards zh />, decorators: [storyWidths.wide] };

/** In 中文 at the half width. */
export const SettingRowsChineseHalf: StoryObj = { render: () => <SettingRowsCards zh />, decorators: [storyWidths.half] };

/** In 中文 at the floor. */
export const SettingRowsChineseNarrow: StoryObj = { render: () => <SettingRowsCards zh />, decorators: [storyWidths.narrow] };

/**
 * A form laid by its own width (the UI/UX design R1, §4.2; UIFIT1d): one field, three fields, and a list of rows with its
 * head row, each field by what it holds and nothing capped below the pane. Below the form's floor each row stacks, the
 * head row goes and each field shows its own name. At the frame's three widths; `measured/settings.spec.ts` reads them.
 */
const field = (label: string, mono = false) => (
  <input aria-label={label} defaultValue="" className={`min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink ${mono ? 'font-mono' : ''}`} />
);
const GATES = [
  { name: 'web', run: 'npm --prefix web test -- --run', cwd: 'web' },
  { name: 'driver', run: 'dotnet test Driver.Tests --no-build', cwd: '' },
];

function FormGridsCards() {
  return (
    <CardStack>
      <Card>
        <SettingList>
          <SettingRow label="Branch pattern" hint="One field: a pattern, 20rem.">
            <FormGrid columns={['pattern']}>
              <FormRow><FormField label="Pattern">{field('pattern', true)}</FormField></FormRow>
            </FormGrid>
          </SettingRow>
        </SettingList>
      </Card>
      <Card>
        <p className="m-0 mb-2 text-small text-ink-soft">Three fields: an address takes the rest, a title 16rem, the act after them.</p>
        <FormGrid columns={['long', 'name', 'acts']}>
          <FormRow>
            <FormField label="Address">{field('address', true)}</FormField>
            <FormField label="Title">{field('title')}</FormField>
            <Button variant="primary">Add</Button>
          </FormRow>
        </FormGrid>
      </Card>
      <Card>
        <p className="m-0 mb-2 text-small text-ink-soft">A list of rows: the heavy gates, their columns aligned, stacked below 40rem with each field named.</p>
        <FormGrid columns={['short', 'long', 'short', 'acts']} floor="40rem">
          <FormRow head>
            <span className="text-meta text-ink-faint">Name</span>
            <span className="text-meta text-ink-faint">Command</span>
            <span className="text-meta text-ink-faint">Folder</span>
            <span />
          </FormRow>
          {GATES.map((gate, at) => (
            <FormRow key={gate.name}>
              <FormField label="Name" when="stacked"><input aria-label={`Name of gate ${at + 1}`} defaultValue={gate.name} className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink" /></FormField>
              <FormField label="Command" when="stacked"><input aria-label={`Command of gate ${at + 1}`} defaultValue={gate.run} className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink" /></FormField>
              <FormField label="Folder" when="stacked"><input aria-label={`Folder of gate ${at + 1}`} defaultValue={gate.cwd} placeholder="the checkout" className="w-full min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1 font-mono text-small text-ink placeholder:italic placeholder:text-ink-faint" /></FormField>
              <span className="flex items-center">
                <Button variant="ghost" aria-label={`Move gate ${at + 1} up`}><Icon name="chevronUp" size={12} /></Button>
                <Button variant="ghost" aria-label={`Move gate ${at + 1} down`}><Icon name="chevronDown" size={12} /></Button>
                <Button variant="ghost" aria-label={`Remove gate ${at + 1}`}><Icon name="x" size={12} /></Button>
              </span>
            </FormRow>
          ))}
        </FormGrid>
      </Card>
    </CardStack>
  );
}

export const FormGrids: StoryObj = { render: () => <FormGridsCards />, decorators: [storyWidths.wide] };

/** At the half width, 630 px: the one- and three-field forms keep their columns; the gates stack, since their value column is under 40rem. */
export const FormGridsHalf: StoryObj = { render: () => <FormGridsCards />, decorators: [storyWidths.half] };

/** At the 400 px floor: every row stacked, each field named. */
export const FormGridsNarrow: StoryObj = { render: () => <FormGridsCards />, decorators: [storyWidths.narrow] };

/**
 * A code span breaks only between its words (LOOK5): the installed window broke a setting's terminal twin inside
 * `--no-keep` and `--workspace`, in 中文, at a narrow row. Three widths and both languages, a word wider than its
 * line, and a command a screen sets itself.
 */
export const CodeSpans: StoryObj = {
  render: () => (
    <div className="grid gap-6">
      {['w-[22rem]', 'w-[30rem]', 'w-[44rem]'].map((width) => (
        <div key={width} className={`${width} max-w-full rounded-card border border-line bg-raised px-4 py-3`}>
          <SettingRow
            label="对话保留在"
            hint="`daoris agent profile use claude-code --keep <account>|--no-keep --workspace engine`"
            control={<CheckField hideLabel label="保留" checked onChange={() => {}} />}
          />
          <SettingRow
            label="Keep for conversations"
            hint="Or `daoris agent profile order claude-code <account>…|--clear --workspace engine` at a terminal."
          />
          <SettingRow
            label="A word wider than its line"
            hint="`C:/somewhere/a-very-long-folder-name-that-will-not-fit/beside-anything-at-all/plugins`"
          />
          <p className="m-0 mt-2 flex flex-wrap items-baseline gap-1.5 text-meta text-ink-faint">
            <span>At a terminal:</span>
            <CodeText text="daoris agent settings claude-code --account work model <model> effort <effort>" />
          </p>
        </div>
      ))}
    </div>
  ),
};

/**
 * The meanings a liveness mark carries, each wearing its word; `done` and `waits` are a fact's marks (MILESTONEUX1). There is
 * no story here for a dot without a label, because the component cannot render one — which is the point (D41 §6).
 */
export const Dots: StoryObj = {
  render: () => (
    <div className="flex flex-wrap items-center gap-5">
      <Dot tone="live" label="working" />
      <Dot tone="parked" label="awaiting person" />
      <Dot tone="ended" label="completed" />
      <Dot tone="idle" label="queued" />
      <Dot tone="done" label="reached" />
      <Dot tone="waits" label="pending" />
    </div>
  ),
};

/** 500 lines is the scroll case; 12k dropped is the one a person must never have to infer. */
const LONG_LOG = Array.from(
  { length: 500 },
  (_, index) => `[${String(index).padStart(4, '0')}] resolving dependency graph — 道衍 canon/core/rules`,
).join('\n');

export const Wells: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl gap-6">
      <MonoWell label="empty — the well is there, with nothing in it yet" text="" />
      <MonoWell
        label="console"
        live
        text={'$ npm run verify\ndaoris: clean — 23862 of 26000 bytes of always-loaded core\nrelease-prep: 0.0.1 agrees across every shipped reference'}
      />
      <MonoWell label="console" tall dropped={12_043} text={LONG_LOG} />
    </div>
  ),
};

/**
 * A pair with no value is absent, never blank: on a session record an absence means something real
 * (no profile means the harness's own home), and a placeholder would look like a bug instead.
 */
export const MetaLines: StoryObj = {
  render: () => (
    <div className="grid max-w-2xl gap-5">
      <MetaLine items={[{ label: 'moved', value: '4m ago' }]} />
      <MetaLine
        items={[
          { label: 'repository', value: 'engine' },
          { label: 'tree', value: 'worktrees/streaming-budget', mono: true },
          { label: 'quest', value: '#7a82cc', mono: true },
          { label: 'tool', value: 'claude-code 2.1.4 · as owner' },
          { label: 'started', value: '2h ago' },
          { label: 'moved', value: '4m ago' },
        ]}
      />
      <MetaLine
        items={[
          { label: 'repository', value: 'engine' },
          { label: 'profile', value: null },
          { label: 'tree', value: 'a/deliberately/long/checkout/path/that/has/nowhere/to/wrap/worktrees/streaming', mono: true },
        ]}
      />
    </div>
  ),
};

export const Tiles: StoryObj = {
  render: () => (
    <div className="grid max-w-3xl grid-cols-[repeat(auto-fit,minmax(10.5rem,1fr))] gap-3">
      <Tile label="Adopted repositories" value={2} note="of 17 in the family" />
      <Tile label="Open quests" value={3} note="oldest has sat 12d" warn />
      <Tile label="Knowledge entries" value="12.9K" note="across 14 repositories" />
    </div>
  ),
};

export const Empty: StoryObj = {
  render: () => (
    <EmptyState
      icon="inbox"
      headline="No open quests anywhere"
      body="The family owes itself nothing right now. When a repository needs something from a sibling, it is asked for here — never edited across."
      action={<Button>ask for something</Button>}
    />
  ),
};

export const Loading: StoryObj = {
  render: () => <SkeletonRows rows={4} />,
};

export const ToastStates: StoryObj = {
  render: () => (
    <Toasts
      onClose={() => {}}
      items={[
        {
          id: 1,
          kind: 'ok',
          text: 'Published quest `#7a82cc` to `engine` — Open. It is held by the service, not written into that repository.',
        },
        { id: 2, kind: 'error', text: 'Declining needs a reason: it is the part the asker can act on.' },
      ]}
    />
  ),
};

export const DrawerDetail: StoryObj = {
  render: () => (
    <Drawer
      title="Expose a streaming budget on the chunk API"
      onClose={() => {}}
      meta={<><Pill tone="open">Open</Pill><span className="font-mono text-meta text-ink-faint">#7a82cc</span></>}
      footer={
        <div className="flex flex-wrap gap-2">
          <Button>take</Button>
          <Button variant="primary">done</Button>
          <Button variant="ghost">decline…</Button>
        </div>
      }
    >
      <p className="m-0 whitespace-pre-wrap text-body leading-relaxed">
        World streaming needs to cap hydration work per frame; today the engine hydrates unbounded.
        Evidence: the seam appears whenever more than three chunks hydrate in one frame.
      </p>
    </Drawer>
  ),
};

/**
 * A toast while a drawer is open (UXTOAST1): it stands at the drawer's left edge, over the scrim, so the ask open at the
 * drawer's foot keeps its presses in view. In the corner it lay over them, since the drawer holds that corner whole.
 */
export const ToastBesideDrawer: StoryObj = {
  render: () => (
    <>
      <Drawer title="Manage engine" onClose={() => {}}>
        <p className="m-0 text-body text-ink-soft">
          Retiring takes engine off the family's list. Its checkout, its files and its history stay where they are.
        </p>
        <InlineConfirm
          className="mt-2.5"
          label="Retire"
          says="Retire engine? It leaves the family's list; nothing on disk is deleted."
          meanIt="Retire"
          onClose={() => {}}
        />
      </Drawer>
      <Toasts onClose={() => {}} items={[{ id: 1, kind: 'ok', text: 'Wrote the declaration for `engine`.' }]} />
    </>
  ),
};

/**
 * A menu longer than the window (MENU1): a receiver filter over sixty repositories, open. Its content is capped at
 * the room on its side and scrolls inside it, with the theme's bar, where it ran off the window. The filter's shape is
 * the list's ⋯: a group named for what it chooses, the chosen one ticked, a rule, and a toggle.
 */
export const LongMenu: StoryObj = {
  render: () => (
    <Menu.Root defaultOpen>
      <Menu.Trigger asChild><Button>Filter the list</Button></Menu.Trigger>
      <Menu.Content side="bottom" align="start" highlight="accent" className="min-w-44">
        <Menu.Label className="pt-1.5">Receiver</Menu.Label>
        <Menu.RadioGroup aria-label="Receiver" value="repository-7">
          {Array.from({ length: 60 }, (_, i) => (
            <Menu.RadioItem key={i} value={`repository-${i}`}>
              <span className="truncate">{i % 9 === 4 ? `仓库 ${i}` : `repository-${i}`}</span>
            </Menu.RadioItem>
          ))}
        </Menu.RadioGroup>
        <Menu.Separator />
        <Menu.CheckboxItem checked={false}>Include closed</Menu.CheckboxItem>
      </Menu.Content>
    </Menu.Root>
  ),
};
