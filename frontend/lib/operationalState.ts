// Operational state policy stays independent from React rendering timing.
// "unsupported" is a short-lived visible status, not active work.
export function isOperationalStateBusy(state: string): boolean {
  return (
    state === "thinking" ||
    state === "synthesizing" ||
    state === "speaking"
  );
}

export function shouldForceSpeechStatusHold(result: string): boolean {
  return result === "unsupported";
}
