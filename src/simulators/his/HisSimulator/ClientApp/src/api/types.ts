/**
 * Body of `GET /health` on the HIS simulator.
 *
 * @remarks
 * Hand-written to mirror the simulator's `HealthResponse` record. The simulator has no generated
 * client; add further response types here as WS7 implements each route.
 */
export interface HealthResponse {
  /** Liveness indicator; `ok` while the simulator is serving requests. */
  status: string;
  /** Stable service name, `his-simulator`. */
  service: string;
  /** Always `true`: the simulator only ever handles simulated data. */
  synthetic: boolean;
}
