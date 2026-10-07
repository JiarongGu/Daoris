import type { CommandEntry, MenuId } from '../commands';
import type { MenuItem } from './AppMenu';

/**
 * One menu of the bar, as its rows (UX7a, D152 §2): built from the one table, so a menu holds exactly what the palette
 * and the keys do, and nothing a list kept by care.
 *
 * @remarks
 * - **A rule between groups**, as VS Code's menus have them; a record's group (Run's *This session*, *This quest*) is named
 *   over its first row, with the tip that says what to open while its acts are off (the design §3.3).
 * - **A row is disabled where its surface exists and the state forbids it**, so its place is learned, and absent where the
 *   surface is absent: the table has already left out what a browser may not know (D47 §4).
 * - **A row prints its first key** at its right, as VS Code's do; a key a browser keeps is not printed there.
 * - **A submenu is one row** (View's *Theme ▸*, *Language ▸*), at its first choice's place, its choices a radio group.
 * - **A row carries what its tick means** (UXFIX1): a region shown is a toggle, a place or a workspace one choice
 *   (`radio`), so the menu says each as it is and not only draws it.
 *
 * Built as data, so a desktop's menus and a browser's are asserted without mounting the application.
 */
export function menuRows(entries: readonly CommandEntry[], menu: MenuId): MenuItem[] {
  const rows: MenuItem[] = [];
  let group: string | null = null;

  for (const entry of entries) {
    if (entry.menu !== menu || !entry.menuItem) continue;
    const separated = group !== null && entry.group !== group;
    const firstOfGroup = entry.group !== group;
    group = entry.group;

    if (entry.submenu) {
      const sub: MenuItem = {
        id: entry.id,
        label: entry.label,
        ...(entry.checked !== undefined ? { checked: entry.checked } : {}),
        ...(entry.enabled ? {} : { disabled: true }),
      };
      const parentId = entry.id.split(':')[0]!;
      const parent = rows.find((row) => row.id === parentId);
      if (parent) {
        parent.sub!.push(sub);
        continue;
      }
      rows.push({ id: parentId, label: entry.submenu, sub: [sub], ...(separated ? { separated } : {}) });
      continue;
    }

    rows.push({
      id: entry.id,
      label: entry.label,
      ...(entry.icon ? { icon: entry.icon } : {}),
      ...(entry.badge !== undefined ? { badge: entry.badge } : {}),
      ...(separated ? { separated } : {}),
      ...(entry.checked !== undefined ? { checked: entry.checked } : {}),
      ...(entry.radio ? { radio: entry.radio } : {}),
      ...(entry.enabled ? {} : { disabled: true }),
      ...(entry.keys[0] ? { shortcut: entry.keys[0].combo } : {}),
      ...(firstOfGroup && entry.heading ? { heading: entry.heading } : {}),
    });
  }

  // A group's tip says what to open while every act in it is off; once one applies, its name is enough.
  const groupOf = new Map(entries.map((entry) => [entry.id, entry.group]));
  for (const row of rows) {
    if (!row.heading) continue;
    const group = groupOf.get(row.id);
    const live = rows.some((other) => groupOf.get(other.id) === group && !other.disabled);
    if (live) row.heading = { label: row.heading.label };
  }
  return rows;
}
