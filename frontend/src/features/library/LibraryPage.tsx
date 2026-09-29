import { Link } from 'react-router-dom';
import { ChefHatIcon, SearchIcon } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { ImportDialog } from '@/features/import/ImportDialog';
import { sourceHost } from '@/lib/sourceHost';
import { useRecipes } from './api';
import { useLibrarySearch } from './useLibrarySearch';

export function LibraryPage() {
  const { input, setInput, q } = useLibrarySearch();
  const { data: recipes, isPlaceholderData } = useRecipes(q);

  return (
    <main className="mx-auto max-w-2xl p-4">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Rezepte</h1>
        <ImportDialog />
      </header>
      <div className="sticky top-0 z-10 -mx-4 bg-background px-4 py-3">
        <div className="relative">
          <SearchIcon
            aria-hidden
            className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
          />
          <Input
            type="search"
            aria-label="Rezepte durchsuchen…"
            placeholder="Rezepte durchsuchen…"
            className="pl-8"
            value={input}
            onChange={(event) => setInput(event.target.value)}
          />
        </div>
      </div>
      {recipes?.length === 0 && !isPlaceholderData && (
        <p className="py-8 text-center text-muted-foreground">
          {q ? (
            `Keine Rezepte zu „${q}"`
          ) : (
            <>
              Importiere dein erstes Rezept
              <br />
              <span className="text-sm">
                Über „Importieren" oben rechts fügst du eine Webadresse ein.
              </span>
            </>
          )}
        </p>
      )}
      <ul className="divide-y">
        {recipes?.map((recipe) => (
          <li key={recipe.id}>
            <Link
              to={`/recipes/${recipe.id}`}
              className="flex items-center gap-3 py-2"
            >
              {recipe.hasImage ? (
                <img
                  src={`/api/recipes/${recipe.id}/image`}
                  alt={recipe.title}
                  className="size-14 shrink-0 rounded-md object-cover"
                />
              ) : (
                <span className="flex size-14 shrink-0 items-center justify-center rounded-md bg-muted text-muted-foreground">
                  <ChefHatIcon aria-hidden className="size-6" />
                </span>
              )}
              <span className="min-w-0">
                <span className="block truncate font-medium">
                  {recipe.title}
                </span>
                <span className="block truncate text-sm text-muted-foreground">
                  {sourceHost(recipe.sourceUrl)}
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </main>
  );
}
