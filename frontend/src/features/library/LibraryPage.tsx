import { ImportDialog } from '@/features/import/ImportDialog';

export function LibraryPage() {
  return (
    <main className="mx-auto max-w-2xl p-4">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Rezepte</h1>
        <ImportDialog />
      </header>
    </main>
  );
}
