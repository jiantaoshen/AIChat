// This hook owns the temporary-expression timer. The caller validates both session and turn generation before applying the timeout result.
"use client";

import { useCallback, useEffect, useRef } from "react";
import type { AvatarDecision } from "@/types/chat";

export function useNeutralDecisionTimer(
  onReset: (sessionId: number, turnGeneration: number) => void,
) {
  const timerRef = useRef<number | null>(null);

  const clearNeutralResetTimer = useCallback(() => {
    if (timerRef.current === null) {
      return;
    }

    window.clearTimeout(timerRef.current);
    timerRef.current = null;
  }, []);

  const scheduleNeutralReset = useCallback(
    (
      decision: AvatarDecision,
      delayMs: number,
      sessionId: number,
      turnGeneration: number,
    ) => {
      clearNeutralResetTimer();

      if (decision.emotion === "neutral" && decision.gesture === "none") {
        return;
      }

      timerRef.current = window.setTimeout(() => {
        timerRef.current = null;
        onReset(sessionId, turnGeneration);
      }, delayMs);
    },
    [clearNeutralResetTimer, onReset],
  );

  useEffect(() => {
    return () => {
      clearNeutralResetTimer();
    };
  }, [clearNeutralResetTimer]);

  return {
    clearNeutralResetTimer,
    scheduleNeutralReset,
  };
}
