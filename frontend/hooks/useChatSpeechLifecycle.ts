// This hook owns TTS/replay plus temporary-expression timing for one chat turn and refuses stale speech/timer completions.
"use client";

import { useCallback } from "react";
import { useAvatarSpeech } from "@/hooks/useAvatarSpeech";
import { useNeutralDecisionTimer } from "@/hooks/useNeutralDecisionTimer";
import type { AvatarDecision } from "@/types/chat";

const EXPRESSION_HOLD_WITHOUT_TTS_MS = 4500;
const EXPRESSION_HOLD_AFTER_SPEECH_MS = 1500;

export interface TurnIdentity {
  sessionId: number;
  turnGeneration: number;
}

interface UseChatSpeechLifecycleOptions {
  isCurrentTurn: (identity: TurnIdentity) => boolean;
  onNeutralReset: () => void;
  onTtsError: (message: string | null) => void;
}

export function useChatSpeechLifecycle({
  isCurrentTurn,
  onNeutralReset,
  onTtsError,
}: UseChatSpeechLifecycleOptions) {
  const handleTtsError = useCallback(
    (message: string) => onTtsError(message),
    [onTtsError],
  );
  const {
    speechState,
    hasReplay,
    speak,
    replay,
    stopSpeech,
    clearSpeech,
  } = useAvatarSpeech({ onError: handleTtsError });

  const handleNeutralTimer = useCallback(
    (sessionId: number, turnGeneration: number) => {
      const identity = { sessionId, turnGeneration };
      if (isCurrentTurn(identity)) {
        onNeutralReset();
      }
    },
    [isCurrentTurn, onNeutralReset],
  );
  const { clearNeutralResetTimer, scheduleNeutralReset } =
    useNeutralDecisionTimer(handleNeutralTimer);

  async function speakDecision(
    messageId: string,
    decision: AvatarDecision,
    identity: TurnIdentity,
  ) {
    if (!decision.speech.trim()) {
      scheduleDecisionReset(decision, EXPRESSION_HOLD_WITHOUT_TTS_MS, identity);
      return;
    }

    try {
      const result = await speak({
        messageId,
        text: decision.speech,
        language: decision.language,
        emotion: decision.emotion,
        emotionIntensity: decision.emotionIntensity,
      });
      if (!isCurrentTurn(identity)) {
        return;
      }

      scheduleDecisionReset(
        decision,
        result === "completed"
          ? EXPRESSION_HOLD_AFTER_SPEECH_MS
          : EXPRESSION_HOLD_WITHOUT_TTS_MS,
        identity,
      );
    } catch {
      if (isCurrentTurn(identity)) {
        // A TTS failure must not discard a successful text response.
        scheduleDecisionReset(
          decision,
          EXPRESSION_HOLD_WITHOUT_TTS_MS,
          identity,
        );
      }
    }
  }

  async function replayDecision(
    decision: AvatarDecision,
    identity: TurnIdentity,
  ) {
    clearNeutralResetTimer();
    onTtsError(null);

    try {
      const result = await replay();
      if (result === "completed" && isCurrentTurn(identity)) {
        scheduleDecisionReset(
          decision,
          EXPRESSION_HOLD_AFTER_SPEECH_MS,
          identity,
        );
      }
    } catch {
      // useAvatarSpeech already exposes the readable playback error.
    }
  }

  function scheduleDecisionReset(
    decision: AvatarDecision,
    delayMs: number,
    identity: TurnIdentity,
  ) {
    scheduleNeutralReset(
      decision,
      delayMs,
      identity.sessionId,
      identity.turnGeneration,
    );
  }

  function scheduleWithoutSpeechReset(
    decision: AvatarDecision,
    identity: TurnIdentity,
  ) {
    scheduleDecisionReset(decision, EXPRESSION_HOLD_WITHOUT_TTS_MS, identity);
  }

  return {
    speechState,
    hasReplay,
    speakDecision,
    replayDecision,
    scheduleWithoutSpeechReset,
    stopSpeech,
    clearSpeech,
    clearNeutralResetTimer,
  };
}
