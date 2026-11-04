import { type ReactNode } from 'react';

/** A record's row of facts: its name, and what it holds. */
export function Fact({ name, children }: { name: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-ink-faint">{name}</dt>
      <dd className="m-0">{children}</dd>
    </>
  );
}
