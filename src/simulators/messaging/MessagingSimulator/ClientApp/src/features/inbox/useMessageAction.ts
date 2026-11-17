import { useCallback, useState } from 'react';

import type { InboxMessage } from '../../api';
import { errorMessage } from '../../hooks';

/** What {@link useMessageAction} gives a channel view. */
export interface MessageAction {
  /** Whether an action is being sent. */
  busy: boolean;
  /** Why the latest action was not accepted, or `null`. */
  error: string | null;
  /**
   * Runs one patient action.
   * Resolves to `true` when CareLoop accepted it and `false` when it was refused or could not be sent.
   */
  send: (action: () => Promise<InboxMessage>) => Promise<boolean>;
}

/**
 * Sends a patient action for one inbox message and reports the outcome.
 *
 * @remarks
 * Makes the network call the action performs. The message on screen changes only through
 * `onChanged`, which is called with the host's answer after CareLoop accepted the action, so the
 * view never shows a response CareLoop does not know about.
 *
 * @param onChanged - Receives the updated message after an accepted action.
 * @returns The busy and error state, and the function that runs an action.
 */
export function useMessageAction(onChanged: (message: InboxMessage) => void): MessageAction {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const send = useCallback(
    async (action: () => Promise<InboxMessage>) => {
      setBusy(true);
      setError(null);
      try {
        onChanged(await action());
        return true;
      } catch (failure) {
        setError(errorMessage(failure));
        return false;
      } finally {
        setBusy(false);
      }
    },
    [onChanged],
  );

  return { busy, error, send };
}
