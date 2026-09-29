import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeftIcon, MoreVerticalIcon, UsersIcon } from 'lucide-react';
import { AspectRatio } from '@/components/ui/aspect-ratio';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { DeleteRecipeDialog } from '@/features/delete-recipe/DeleteRecipeDialog';
import { sourceHost } from '@/lib/sourceHost';
import { useRecipe } from './api';
import { formatDuration } from './formatDuration';
import { useWakeLock, type WakeLockStatus } from './useWakeLock';

const wakeLockLabel: Record<WakeLockStatus, string> = {
  active: 'Bildschirm bleibt an',
  released: 'Bildschirm kann sich sperren',
  unsupported: 'Bildschirmsperre nicht verfügbar',
};

export function CookViewPage() {
  const id = Number(useParams().id);
  const { data: recipe, isPending, isError } = useRecipe(id);
  const invalid = !Number.isInteger(id);
  const wakeLock = useWakeLock();
  const navigate = useNavigate();
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const times = [
    ['Vorbereitung', recipe?.prepMinutes],
    ['Kochen', recipe?.cookMinutes],
    ['Gesamt', recipe?.totalMinutes],
  ] as const;

  return (
    <>
      <header className="sticky top-0 z-10 border-b bg-background">
        <div className="mx-auto flex max-w-2xl items-center gap-2 px-4 py-2">
          <Link
            to="/"
            aria-label="Rezepte"
            className="inline-flex shrink-0 items-center gap-1 text-sm text-muted-foreground"
          >
            <ArrowLeftIcon className="size-4" />
            Rezepte
          </Link>
          <span className="min-w-0 flex-1 truncate text-center text-sm font-medium">
            {recipe?.title}
          </span>
          {recipe ? (
            <DropdownMenu>
              <DropdownMenuTrigger
                render={<Button variant="ghost" size="icon" />}
                aria-label="Mehr"
              >
                <MoreVerticalIcon />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem
                  render={
                    <a
                      href={recipe.sourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    />
                  }
                >
                  Quelle öffnen
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => setConfirmingDelete(true)}>
                  Löschen
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : (
            <span className="size-8 shrink-0" />
          )}
        </div>
      </header>
      <main className="mx-auto max-w-2xl p-4">
        {invalid ? (
          <p>Rezept nicht gefunden.</p>
        ) : isError || (!isPending && !recipe) ? (
          <p>Rezept konnte nicht geladen werden.</p>
        ) : isPending || !recipe ? null : (
          <article className="grid gap-6">
            {recipe.imageUrl && (
              <AspectRatio ratio={4 / 3} className="overflow-hidden rounded-lg">
                <img
                  src={recipe.imageUrl}
                  alt={recipe.title}
                  className="size-full object-cover"
                />
              </AspectRatio>
            )}
            <h1 className="text-2xl font-semibold">{recipe.title}</h1>
            <div className="flex flex-wrap gap-2">
              {recipe.servings && (
                <Badge variant="secondary">
                  <UsersIcon />
                  {recipe.servings}
                </Badge>
              )}
              {times.map(
                ([label, minutes]) =>
                  minutes != null && (
                    <Badge key={label} variant="secondary">
                      {label}: {formatDuration(minutes)}
                    </Badge>
                  ),
              )}
              <Badge variant="outline">{wakeLockLabel[wakeLock]}</Badge>
            </div>
            <section>
              <h2 className="mb-2 text-lg font-medium">Zutaten</h2>
              <ul className="list-disc pl-5">
                {recipe.ingredientLines.map((line, i) => (
                  <li key={i}>{line}</li>
                ))}
              </ul>
            </section>
            <section>
              <h2 className="mb-2 text-lg font-medium">Zubereitung</h2>
              <ol className="list-decimal space-y-3 pl-5">
                {recipe.steps.map((step, i) => (
                  <li key={i} className="whitespace-pre-line">
                    {step}
                  </li>
                ))}
              </ol>
            </section>
            <footer className="text-sm text-muted-foreground">
              <a
                href={recipe.sourceUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="underline"
              >
                Von {sourceHost(recipe.sourceUrl)}
              </a>
            </footer>
          </article>
        )}
      </main>
      <DeleteRecipeDialog
        recipe={confirmingDelete && recipe ? recipe : null}
        onClose={() => setConfirmingDelete(false)}
        onDeleted={() => void navigate('/')}
      />
    </>
  );
}
