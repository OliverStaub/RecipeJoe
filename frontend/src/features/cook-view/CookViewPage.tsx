import { Link, useParams } from 'react-router-dom';
import { ArrowLeftIcon } from 'lucide-react';
import { useRecipe } from './api';

export function CookViewPage() {
  const id = Number(useParams().id);
  const { data: recipe, isPending, isError } = useRecipe(id);
  const invalid = !Number.isInteger(id);

  return (
    <main className="mx-auto max-w-2xl p-4">
      <Link
        to="/"
        className="mb-4 inline-flex items-center gap-1 text-sm text-muted-foreground"
      >
        <ArrowLeftIcon className="size-4" />
        Rezepte
      </Link>
      {invalid ? (
        <p>Rezept nicht gefunden.</p>
      ) : isError || (!isPending && !recipe) ? (
        <p>Rezept konnte nicht geladen werden.</p>
      ) : isPending || !recipe ? null : (
        <article className="grid gap-6">
          <h1 className="text-2xl font-semibold">{recipe.title}</h1>
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
        </article>
      )}
    </main>
  );
}
