import { describe, it } from 'vitest';

// Planned WS8 behaviours; each becomes a real test when the screen is built.
describe('Patient inbox', () => {
  it.todo('lists simulated messages newest first with a simulated marker');
  it.todo('shows channel, delivery state and response state for each message');
  it.todo('acknowledge calls POST /api/inbox/{id}/acknowledge and shows the acknowledged state');
  it.todo('"I understand" calls POST /api/inbox/{id}/understand and shows the understood state');
  it.todo('"call me" calls POST /api/inbox/{id}/call-me and shows the call-requested state');
  it.todo('a delivered message is shown as not yet acknowledged');
  it.todo('shows an error when CareLoop cannot be reached');
});
