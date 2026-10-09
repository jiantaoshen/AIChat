// This hook owns TTS/replay plus temporary-expression timing for one chat turn and refuses stale speech/timer completions.
"use client";

import { useCallback } from "react";
import { useAvatarSpeech } from "@/hooks/useAvatarSpeech";
import {
  completeSpeechHold,
  type TurnIdentity,
} from "@/hooks/chatSpeechLifecyclePolicy";
import { useNeutralDecisionTimer } from "@/hooks/useNeutralDecisionTimer";
import { shouldForceSpeechStatusHold } from "@/lib/operationalState";
import type { AvatarDecision, SpeechCapability } from "@/types/chat";

const EXPRESSION_HOLD_WITHOUT_TTS_MS = 4500;
const EXPRESSION_HOLD_AFTER_SPEECH_MS = 1500;

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
    clearUnsupportedState,
  } = useAvatarSpeech({ onError: handleTtsError });

  const handleNeutralTimer = useCallback(
    (sessionId: number, turnGeneration: number) => {
      completeSpeechHold({
        identity: { sessionId, turnGeneration },
        isCurrentTurn,
        clearUnsupportedState,
        onNeutralReset,
      });
    },
    [clearUnsupportedState, isCurrentTurn, onNeutralReset],
  );
  const { clearNeutralResetTimer, scheduleNeutralReset } =
    useNeutralDecisionTimer(handleNeutralTimer);

  async function speakDecision(
    messageId: string,
    decision: AvatarDecision,
    speechCapability: SpeechCapability,
    identity: TurnIdentity,
  ) {
    if (!decision.speech.trim()) {
      scheduleDecisionReset(decision, EXPRESSION_HOLD_WITHOUT_TTS_MS, identity);
      return;
    }

    try {
      const result = await speak({
        messageId,
        supported: speechCapability.supported,
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
        shouldForceSpeechStatusHold(result),
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
    force = false,
  ) {
    scheduleNeutralReset(
      decision,
      delayMs,
      identity.sessionId,
      identity.turnGeneration,
      force,
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
