import { CircleAlertIcon, GlobeIcon, Loader2Icon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import type { components } from '@/api/schema';
import { failureMessage, isRetryable } from '@/features/import/failureMessage';
import { stageLabel } from '@/features/import/stageLabel';
import { urlLabel } from '@/features/import/urlLabel';
import { useDismissImport, useRetryImport } from '@/api/recipes';

type Import = components['schemas']['ImportDto'];

export function ImportRow({ importDto }: { importDto: Import }) {
  const { mutate: retry, isPending: isRetrying } = useRetryImport();
  const { mutate: dismiss, isPending: isDismissing } = useDismissImport();

  return (
    <li className="flex items-center gap-1">
      <span className="flex min-w-0 flex-1 items-center gap-3 py-2">
        {importDto.state === 'Pending' ? (
          <span className="relative flex size-14 shrink-0 items-center justify-center">
            <Skeleton className="absolute inset-0" />
            <GlobeIcon aria-hidden className="relative text-muted-foreground" />
          </span>
        ) : (
          <span className="flex size-14 shrink-0 items-center justify-center rounded-md bg-destructive/10 text-destructive">
            <CircleAlertIcon aria-hidden className="size-6" />
          </span>
        )}
        <span className="min-w-0">
          <span className="block truncate font-medium" title={importDto.url}>
            {urlLabel(importDto.url)}
          </span>
          {importDto.state === 'Pending' ? (
            <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
              <Loader2Icon aria-hidden className="size-3.5 animate-spin" />
              {stageLabel(importDto.stage ?? 'Fetching')}
            </span>
          ) : (
            <span className="block text-sm text-destructive">
              {failureMessage(importDto.failure!, importDto.kind)}
            </span>
          )}
        </span>
      </span>
      {importDto.state === 'Failed' && (
        <span className="flex shrink-0 gap-1">
          {isRetryable(importDto.failure!) && (
            <Button
              variant="outline"
              size="sm"
              disabled={isRetrying || isDismissing}
              onClick={() => retry({ params: { path: { id: importDto.id } } })}
            >
              Erneut versuchen
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            disabled={isRetrying || isDismissing}
            onClick={() => dismiss({ params: { path: { id: importDto.id } } })}
          >
            Verwerfen
          </Button>
        </span>
      )}
    </li>
  );
}
