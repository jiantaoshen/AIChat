// This hook adapts React lifecycle to the deterministic chat transport controller. It never owns browser-visible chat state.
"use client";

import { useCallback, useEffect, useRef } from "react";
import {
  ChatTurnTransportController,
  type ExecuteTurnOptions,
} from "@/hooks/chatTurnTransportController";
import { sendChatRequest } from "@/lib/api";
import type { AvatarChatResponse } from "@/types/chat";

export function useChatTurnTransport() {
  const controllerRef = useRef<ChatTurnTransportController<AvatarChatResponse> | null>(
    null,
  );

  if (controllerRef.current === null) {
    controllerRef.current = new ChatTurnTransportController(sendChatRequest);
  }

  const executeTurn = useCallback((options: ExecuteTurnOptions) => {
    return controllerRef.current!.execute(options);
  }, []);

  const resetTurnTransport = useCallback(() => {
    controllerRef.current?.reset();
  }, []);

  useEffect(() => {
    return () => {
      controllerRef.current?.reset();
    };
  }, []);

  return {
    executeTurn,
    resetTurnTransport,
  };
}
