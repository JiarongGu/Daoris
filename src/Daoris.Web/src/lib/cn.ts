import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** The standard composition idiom: conditional classes, with Tailwind conflicts resolved last-wins. */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
