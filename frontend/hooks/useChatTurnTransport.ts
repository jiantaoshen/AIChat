// This hook owns one in-flight chat HTTP request plus retry idempotency. It never owns browser-visible chat state.
"use client";

import { useCallback, useEffect, useRef } from "react";
import { sendChatRequest } from "@/lib/api";
import type { AvatarChatResponse } from "@/types/chat";

interface ActiveTurn {
  sessionId: number;
  turnId: string;
  controller: AbortController;
}

interface RetryableTurn {
  sessionId: number;
  conversationId: string | null;
  turnId: string;
  message: string;
}

interface ExecuteTurnOptions {
  sessionId: number;
  conversationId: string | null;
  message: string;
  onStart: () => void;
}

interface CompletedTurn {
  turnId: string;
  response: AvatarChatResponse;
}

export function useChatTurnTransport() {
  const activeTurnRef = useRef<ActiveTurn | null>(null);
  const retryableTurnRef = useRef<RetryableTurn | null>(null);

  const resetTurnTransport = useCallback(() => {
    activeTurnRef.current?.controller.abort();
    activeTurnRef.current = null;
    retryableTurnRef.current = null;
  }, []);

  const executeTurn = useCallback(
    async ({
      sessionId,
      conversationId,
      message,
      onStart,
    }: ExecuteTurnOptions): Promise<CompletedTurn | null> => {
      const retryable = retryableTurnRef.current;
      const isRetry =
        retryable?.sessionId === sessionId &&
        retryable.conversationId === conversationId &&
        retryable.message === message;
      const turnId =
        isRetry && retryable ? retryable.turnId : crypto.randomUUID();

      const controller = new AbortController();
      activeTurnRef.current?.controller.abort();
      activeTurnRef.current = { sessionId, turnId, controller };
      onStart();

      try {
        const response = await sendChatRequest(
          { conversationId, turnId, message },
          controller.signal,
        );

        if (!isActiveTurn(activeTurnRef.current, sessionId, turnId)) {
          return null;
        }

        activeTurnRef.current = null;
        retryableTurnRef.current = null;
        return { turnId, response };
      } catch (caught) {
        if (
          controller.signal.aborted ||
          !isActiveTurn(activeTurnRef.current, sessionId, turnId)
        ) {
          return null;
        }

        activeTurnRef.current = null;
        retryableTurnRef.current = {
          sessionId,
          conversationId,
          turnId,
          message,
        };
        throw caught;
      }
    },
    [],
  );

  useEffect(() => {
    return () => {
      resetTurnTransport();
    };
  }, [resetTurnTransport]);

  return {
    executeTurn,
    resetTurnTransport,
  };
}

function isActiveTurn(
  activeTurn: ActiveTurn | null,
  sessionId: number,
  turnId: string,
) {
  return activeTurn?.sessionId === sessionId && activeTurn.turnId === turnId;
}
