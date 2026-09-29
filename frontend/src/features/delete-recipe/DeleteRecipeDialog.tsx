import { toast } from 'sonner';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { useDeleteRecipe } from './api';

type Props = {
  /** The Recipe to confirm deleting; `null` keeps the dialog closed. */
  recipe: { id: number; title: string } | null;
  onClose: () => void;
  onDeleted?: () => void;
};

export function DeleteRecipeDialog({ recipe, onClose, onDeleted }: Props) {
  const { mutate, isPending, isError, reset } = useDeleteRecipe();

  function close() {
    reset();
    onClose();
  }

  return (
    <AlertDialog
      open={recipe !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>„{recipe?.title}" löschen?</AlertDialogTitle>
          <AlertDialogDescription>
            Das kann nicht rückgängig gemacht werden.
          </AlertDialogDescription>
        </AlertDialogHeader>
        {isError && (
          <p role="alert" className="text-sm text-destructive">
            Rezept konnte nicht gelöscht werden.
          </p>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel>Abbrechen</AlertDialogCancel>
          <Button
            variant="destructive"
            disabled={isPending}
            onClick={() => {
              if (!recipe) return;
              mutate(
                { params: { path: { id: recipe.id } } },
                {
                  onSuccess: () => {
                    toast('Rezept gelöscht');
                    close();
                    onDeleted?.();
                  },
                },
              );
            }}
          >
            Löschen
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
