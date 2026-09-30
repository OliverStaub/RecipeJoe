import { AppProviders } from './AppProviders';
import { router } from './router';

export function App() {
  return <AppProviders router={router} />;
}
