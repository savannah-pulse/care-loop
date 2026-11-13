import { describe, it } from 'vitest';

// Planned WS8 behaviours; each becomes a real test when the screen is built.
describe('API client', () => {
  it.todo('listInbox fetches GET /api/inbox');
  it.todo('actOnInboxMessage posts to the action route');
  it.todo('getCaseStatus fetches GET /api/case-status/{taskId}');
  it.todo('listPatientBehaviours fetches GET /api/patient-behaviours');
  it.todo('setPatientBehaviour sends PUT /api/patient-behaviours/{patientSyntheticId}');
  it.todo('removePatientBehaviour sends DELETE /api/patient-behaviours/{patientSyntheticId}');
  it.todo('runScenario posts to /scenarios/{name}/run');
  it.todo('rejects with a readable error on a non-2xx response');
});
