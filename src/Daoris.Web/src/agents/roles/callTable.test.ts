import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { callsOf, REGISTERED_TOOLS, WORK_COLUMNS } from './callTable';

// COMPANYVIEW1i (the company-view design §2 row 9): what the page shows a work may call is held, cell for cell, to the service's call
// table fixture, which McpRoleFilterTests holds to `CallTable` over the host's registration.

type Column = { only?: string[]; allBut?: string[] };
const fixture = JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Service', 'Daoris.Service.Tests', 'fixtures', 'call-table.json'), 'utf8'),
) as { registered: string[]; columns: Record<string, Column> };

const served = (column: Column, tool: string) => column.only ? column.only.includes(tool) : !column.allBut!.includes(tool);

describe('what a work may call', () => {
  it('lists the tools the host registers, as the service table names them', () => {
    expect([...REGISTERED_TOOLS]).toEqual(fixture.registered);
  });

  it('names a column of the service table for each work, brainstorm for its two turns', () => {
    const used = Object.keys(fixture.columns).filter((name) => !['unplaced', 'unread'].includes(name));
    expect(Object.values(WORK_COLUMNS).flat().sort()).toEqual(used.sort());
    expect(Object.keys(WORK_COLUMNS)).toHaveLength(9);
  });

  it.each(Object.entries(WORK_COLUMNS))('shows %s exactly the tools its column serves', (work, columns) => {
    const shown = new Set(callsOf(work).served);
    for (const tool of fixture.registered) {
      expect(shown.has(tool), `${work} × ${tool}`).toBe(columns.some((name) => served(fixture.columns[name]!, tool)));
    }
    expect(shown.size).toBe([...shown].filter((tool) => fixture.registered.includes(tool)).length);
  });
});
