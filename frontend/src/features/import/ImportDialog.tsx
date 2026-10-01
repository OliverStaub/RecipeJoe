import { useState, type FormEvent } from 'react';
import { Loader2Icon } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { useStartImport } from '@/api/recipes';
import { failureMessage, toImportFailure } from './failureMessage';

export function ImportDialog() {
  const [open, setOpen] = useState(false);
  const [url, setUrl] = useState('');
  const { mutate, isPending, error, reset } = useStartImport();

  function submit(event: FormEvent) {
    event.preventDefault();
    mutate(
      { body: { url: url.trim() } },
      {
        onSuccess: () => {
          setOpen(false);
          setUrl('');
        },
      },
    );
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (isPending) return;
        setOpen(next);
        if (!next) reset();
      }}
    >
      <DialogTrigger render={<Button />}>Importieren</DialogTrigger>
      <DialogContent showCloseButton={!isPending}>
        <DialogHeader>
          <DialogTitle>Rezept importieren</DialogTitle>
        </DialogHeader>
        <form onSubmit={submit} className="grid gap-3">
          <Input
            type="text"
            inputMode="url"
            aria-label="Webadresse"
            placeholder="https://…"
            value={url}
            disabled={isPending}
            onChange={(e) => {
              setUrl(e.target.value);
              if (error) reset();
            }}
          />
          {error && (
            <Alert variant="destructive">
              <AlertTitle>Import fehlgeschlagen</AlertTitle>
              <AlertDescription>
                {failureMessage(toImportFailure(error))}
              </AlertDescription>
            </Alert>
          )}
          <Button type="submit" disabled={isPending || url.trim() === ''}>
            {isPending ? (
              <>
                <Loader2Icon className="animate-spin" />
                Wird importiert…
              </>
            ) : (
              'Importieren'
            )}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
