import type { TFunction, i18n as I18n } from 'i18next';
import type { PresetDelivery, PresetWorkflow } from './answers';

// A shipped workflow's and delivery's words for the page (SETUPUX1a): the driver's data holds one English name and purpose, and the page
// words them from its catalogue by the id, in the language it is shown in. A locale with no key for an id (or a driver's custom preset)
// falls back to the driver's English.

const said = (t: TFunction, i18n: I18n, key: string, fallback: string): string => (i18n.exists(key) ? t(key) : fallback);

/** A shipped workflow's name. */
export const presetName = (t: TFunction, i18n: I18n, preset: Pick<PresetWorkflow, 'id' | 'name'>): string =>
  said(t, i18n, `workflow.shipped.${preset.id}.name`, preset.name);

/** What a shipped workflow is for, or null where the driver says none. */
export const presetFor = (t: TFunction, i18n: I18n, preset: Pick<PresetWorkflow, 'id' | 'for'>): string | null =>
  preset.for ? said(t, i18n, `workflow.shipped.${preset.id}.for`, preset.for) : null;

/** A shipped delivery's name. */
export const deliveryName = (t: TFunction, i18n: I18n, delivery: Pick<PresetDelivery, 'id' | 'name'>): string =>
  said(t, i18n, `workflow.shipped.delivery.${delivery.id}.name`, delivery.name);

/** What a shipped delivery is for. */
export const deliveryFor = (t: TFunction, i18n: I18n, delivery: Pick<PresetDelivery, 'id' | 'for'>): string =>
  said(t, i18n, `workflow.shipped.delivery.${delivery.id}.for`, delivery.for);
