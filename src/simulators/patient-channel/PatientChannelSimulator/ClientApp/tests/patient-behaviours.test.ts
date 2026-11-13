import { describe, it } from 'vitest';

// Planned WS8 behaviours; each becomes a real test when the screen is built.
describe('Patient behaviours', () => {
  it.todo('lists the patients that have a behaviour of their own');
  it.todo('shows the default behaviour used for every other patient');
  it.todo('saves a delivery outcome and a response for one patient');
  it.todo('requires a reason when delivery is set to fail');
  it.todo('allows a response only when delivery is set to deliver');
  it.todo('removes a behaviour so the default applies again');
  it.todo('states that delivery is not an acknowledgement');
});
