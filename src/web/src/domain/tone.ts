/**
 * Tone of a status: the colour of its dot and badge.
 *
 * @remarks
 * Colour never carries meaning alone: every status is also written as a word.
 * - `red`: urgent, overdue and declined.
 * - `amber`: not reached, not acknowledged and needs a new owner.
 * - `blue`: in progress, waiting on someone else.
 * - `green`: done and on track.
 * - `grey`: not started or closed.
 */
export type Tone = 'red' | 'amber' | 'blue' | 'green' | 'grey'
