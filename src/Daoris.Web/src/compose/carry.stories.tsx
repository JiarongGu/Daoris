import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { CarryFields, NO_CARRY, type Carry } from './carry';

// What a quest or an ask carries beside its words (D65 §2): the links typed, and the files dropped,
// pasted or chosen. One molecule, so the two composers cannot drift on how a file arrives.

const meta: Meta = { title: 'Compose/CarryFields' };
export default meta;

const file = (name: string, bytes: number) => new File([new Uint8Array(bytes)], name);

function Held({ start, leftOff = null, dragging = false }: {
  start: Carry; leftOff?: 'tooMany' | 'tooLarge' | null; dragging?: boolean;
}) {
  const [carry, setCarry] = useState(start);
  return (
    <div className="max-w-xl">
      <CarryFields
        carry={carry}
        filesLabel="files — kept on this machine until it travels"
        leftOff={leftOff}
        dragging={dragging}
        onLinks={(links) => setCarry({ ...carry, links })}
        onAttach={(files) => setCarry({ ...carry, files: [...carry.files, ...files] })}
        onRemove={(index) => setCarry({ ...carry, files: carry.files.filter((_, at) => at !== index) })}
      />
    </div>
  );
}

export const Empty: StoryObj = { render: () => <Held start={NO_CARRY} /> };

export const Carrying: StoryObj = {
  render: () => (
    <Held start={{
      links: 'https://tickets.example/T-1\nhttps://docs.example/streaming',
      files: [file('before.png', 48_000), file('trace-with-a-very-long-name-that-must-truncate.log', 2_300_000)],
    }} />
  ),
};

/** A drag over the composer lights the box that says where the file goes. */
export const Dragging: StoryObj = { render: () => <Held start={NO_CARRY} dragging /> };

/** What a drop left off, said while the person is still choosing. */
export const LeftOff: StoryObj = {
  render: () => <Held start={{ links: '', files: [file('one.png', 10)] }} leftOff="tooMany" />,
};
