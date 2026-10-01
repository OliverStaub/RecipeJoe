import type { components } from '@/api/schema';

type ImportStage = NonNullable<components['schemas']['ImportStage']>;

const webStageLabels: Record<ImportStage, string> = {
  Fetching: 'Seite wird geladen…',
  Extracting: 'Rezept wird gelesen…',
  Saving: 'Wird gespeichert…',
};

/** The German label for a Web Import's stage. */
export function stageLabel(stage: ImportStage): string {
  return webStageLabels[stage];
}
