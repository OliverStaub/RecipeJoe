export function formatDuration(totalMinutes: number): string {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (totalMinutes === 0) return '0 Min.';
  return [hours > 0 && `${hours} Std.`, minutes > 0 && `${minutes} Min.`]
    .filter(Boolean)
    .join(' ');
}
