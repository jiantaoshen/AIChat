// This hook orchestrates one browser chat session while transport, reducer state, speech, and timers own their separate lifecycles.
"use client";

import { useCallback, useReducer, useRef } from "react";
import {
  chatSessionReducer,
  createInitialChatSessionState,
} from "@/hooks/chatSessionState";
import {
  type TurnIdentity,
  useChatSpeechLifecycle,
} from "@/hooks/useChatSpeechLifecycle";
import { useChatTurnTransport } from "@/hooks/useChatTurnTransport";
import { isOperationalStateBusy } from "@/lib/operationalState";
import type { AvatarDecision, ChatMessage, OperationalState } from "@/types/chat";

export function useChatSession() {
  const [state, dispatch] = useReducer(
    chatSessionReducer,
    undefined,
    createInitialChatSessionState,
  );
  const sessionIdRef = useRef(0);
  const turnGenerationRef = useRef(0);
  const { executeTurn, resetTurnTransport } = useChatTurnTransport();

  const isCurrentTurn = useCallback((identity: TurnIdentity) => {
    return (
      sessionIdRef.current === identity.sessionId &&
      turnGenerationRef.current === identity.turnGeneration
    );
  }, []);

  const handleTtsError = useCallback((message: string | null) => {
    dispatch({ type: "ttsErrorChanged", message });
  }, []);
  const handleNeutralReset = useCallback(() => {
    dispatch({ type: "neutralReset" });
  }, []);

  const {
    speechState,
    hasReplay,
    speakDecision,
    replayDecision,
    scheduleWithoutSpeechReset,
    stopSpeech,
    clearSpeech,
    clearNeutralResetTimer,
  } = useChatSpeechLifecycle({
    isCurrentTurn,
    onNeutralReset: handleNeutralReset,
    onTtsError: handleTtsError,
  });

  const operationalState: OperationalState =
    state.chatState !== "idle" ? state.chatState : speechState;
  // "unsupported" is informational and short-lived, so it must never lock
  // composer/replay busy semantics while its hold timer is counting down.
  const isBusy = isOperationalStateBusy(operationalState);
  // Text generation is single-flight; TTS remains interruptible by the next send.
  const canSend = state.input.trim().length > 0 && state.chatState !== "thinking";
  const canReplay = hasReplay && !isBusy;

  const latestAssistantMessage = [...state.messages]
    .reverse()
    .find((message) => message.role === "assistant");
  const visibleReply =
    operationalState === "thinking"
      ? "……"
      : state.decision.speech ||
        latestAssistantMessage?.content ||
        (state.error ? "本地服务似乎没有正常回应。请检查右侧状态。" : "");

  async function sendMessage() {
    const text = state.input.trim();
    if (!text || state.chatState === "thinking") {
      return;
    }

    const sessionId = sessionIdRef.current;
    const userMessage: ChatMessage = { role: "user", content: text };
    let identity: TurnIdentity | null = null;

    try {
      const completed = await executeTurn({
        sessionId,
        conversationId: state.conversationId,
        message: text,
        onStart: () => {
          // This callback runs only after the transport synchronously acquires
          // the single-flight text-turn slot. A duplicate send rejected by the
          // controller must not mutate turn identity, interrupt speech, or add
          // an optimistic user message.
          identity = {
            sessionId,
            turnGeneration: ++turnGenerationRef.current,
          };
          stopSpeech();
          clearNeutralResetTimer();
          dispatch({ type: "sendStarted", userMessage });
        },
      });

      // null means either the execute was rejected because another text turn
      // already owns the transport, or this turn became stale after reset.
      if (!completed || identity === null || !isCurrentTurn(identity)) {
        return;
      }

      dispatch({ type: "sendSucceeded", response: completed.response });
      await speakDecision(
        completed.response.assistantMessageId,
        completed.response.decision,
        identity,
      );
    } catch (caught) {
      if (identity === null || !isCurrentTurn(identity)) {
        return;
      }

      const message =
        caught instanceof Error ? caught.message : "Unknown local-model error.";
      const fallbackDecision: AvatarDecision = {
        speech: "",
        language: "en",
        emotion: "confused",
        emotionIntensity: 0.5,
        gesture: "none",
        gestureIntensity: 0,
      };

      dispatch({
        type: "sendFailed",
        message,
        decision: fallbackDecision,
        retryMessage: text,
      });
      scheduleWithoutSpeechReset(fallbackDecision, identity);
    }
  }

  async function replayVoice() {
    if (!canReplay) {
      return;
    }

    const identity: TurnIdentity = {
      sessionId: sessionIdRef.current,
      turnGeneration: turnGenerationRef.current,
    };
    await replayDecision(state.decision, identity);
  }

  function resetConversation() {
    // Identity changes first: cancellation is best-effort, identity is the final
    // correctness barrier against already-resolved stale Promise callbacks.
    sessionIdRef.current += 1;
    turnGenerationRef.current += 1;
    resetTurnTransport();
    clearSpeech();
    clearNeutralResetTimer();
    dispatch({ type: "reset" });
  }

  return {
    messages: state.messages,
    input: state.input,
    setInput: (value: string) => dispatch({ type: "inputChanged", value }),
    decision: state.decision,
    telemetry: state.telemetry,
    operationalState,
    animationKey: state.animationKey,
    error: state.error,
    ttsError: state.ttsError,
    speechState,
    visibleReply,
    canSend,
    canReplay,
    isLogOpen: state.isLogOpen,
    setIsLogOpen: (open: boolean) => dispatch({ type: "logOpenChanged", open }),
    sendMessage,
    replayVoice,
    resetConversation,
  };
}
