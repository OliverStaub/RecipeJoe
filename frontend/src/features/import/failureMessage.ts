import { HttpError } from '@/api/client';
import type { components } from '@/api/schema';

// The generated schema's enum is `| null` only because the field that carries it (ImportDto.failure)
// is nullable; the kind itself is never null.
export type ImportFailure = NonNullable<components['schemas']['ImportFailure']>;

export const importFailures = [
  'InvalidUrl',
  'Unreachable',
  'Blocked',
  'NotFound',
  'BadResponse',
  'ForbiddenAddress',
  'NoRecipe',
  'SaveFailed',
] as const satisfies readonly ImportFailure[];

const kinds: readonly string[] = importFailures;

/** Network failures are `Unreachable`; any other unrecognised error is `BadResponse`. */
export function toImportFailure(error: unknown): ImportFailure {
  const body = error instanceof HttpError ? error.body : error;
  if (typeof body === 'object' && body !== null && 'kind' in body) {
    const { kind } = body;
    if (typeof kind === 'string' && kinds.includes(kind)) {
      return kind as ImportFailure;
    }
  }
  return error instanceof TypeError ? 'Unreachable' : 'BadResponse';
}

const retryableKinds = [
  'Unreachable',
  'Blocked',
  'BadResponse',
  'SaveFailed',
] as const satisfies readonly ImportFailure[];

/** Whether "Erneut versuchen" is offered: only for kinds where a retry could plausibly help. */
export function isRetryable(kind: ImportFailure): boolean {
  return (retryableKinds as readonly string[]).includes(kind);
}

export function failureMessage(kind: ImportFailure): string {
  switch (kind) {
    case 'InvalidUrl':
      return 'Das ist keine gültige Webadresse.';
    case 'Unreachable':
      return 'Die Seite ist nicht erreichbar. Prüfe die Adresse und versuch es nochmal.';
    case 'Blocked':
      return 'Diese Seite blockiert automatische Zugriffe und kann nicht importiert werden.';
    case 'NotFound':
      return 'Diese Seite existiert nicht.';
    case 'BadResponse':
      return 'Die Seite hat etwas geliefert, das wir nicht lesen können.';
    case 'ForbiddenAddress':
      return 'Diese Adresse ist nicht erlaubt.';
    case 'NoRecipe':
      return 'Auf dieser Seite wurde kein Rezept gefunden.';
    case 'SaveFailed':
      return 'Das Rezept konnte nicht gespeichert werden. Versuch es nochmal.';
  }
}
