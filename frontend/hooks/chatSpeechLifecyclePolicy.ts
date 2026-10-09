// Pure completion policy for the temporary speech/expression hold.
// The React hook owns the timer; this function owns the stale-turn guard and
// the explicit unsupported -> idle exit when that timer fires.

export interface TurnIdentity {
  sessionId: number;
  turnGeneration: number;
}

interface CompleteSpeechHoldOptions {
  identity: TurnIdentity;
  isCurrentTurn: (identity: TurnIdentity) => boolean;
  clearUnsupportedState: () => void;
  onNeutralReset: () => void;
}

export function completeSpeechHold({
  identity,
  isCurrentTurn,
  clearUnsupportedState,
  onNeutralReset,
}: CompleteSpeechHoldOptions): boolean {
  if (!isCurrentTurn(identity)) {
    return false;
  }

  clearUnsupportedState();
  onNeutralReset();
  return true;
}
