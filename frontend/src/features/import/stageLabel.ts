import type { components } from '@/api/schema';
import type { ImportKind } from './failureMessage';

type ImportStage = NonNullable<components['schemas']['ImportStage']>;

const stageLabels: Record<ImportKind, Record<ImportStage, string>> = {
  Web: {
    Fetching: 'Seite wird geladen…',
    Extracting: 'Rezept wird gelesen…',
    Saving: 'Wird gespeichert…',
  },
  Video: {
    Fetching: 'Video wird geladen…',
    Extracting: 'Rezept wird geschrieben…',
    Saving: 'Wird gespeichert…',
  },
};

/** The German label for an Import's stage, worded by Import kind. */
export function stageLabel(stage: ImportStage, kind: ImportKind): string {
  return stageLabels[kind][stage];
}
