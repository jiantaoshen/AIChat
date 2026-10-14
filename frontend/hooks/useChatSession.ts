// This hook orchestrates one browser chat session while transport, reducer state, speech, and timers own their separate lifecycles.
"use client";

import { useCallback, useReducer, useRef, useState } from "react";
import {
  chatSessionReducer,
  createInitialChatSessionState,
} from "@/hooks/chatSessionState";
import type { TurnIdentity } from "@/hooks/chatSpeechLifecyclePolicy";
import { useChatSpeechLifecycle } from "@/hooks/useChatSpeechLifecycle";
import { useChatTurnTransport } from "@/hooks/useChatTurnTransport";
import { getConversation } from "@/lib/api";
import { isOperationalStateBusy } from "@/lib/operationalState";
import type { AvatarDecision, ChatMessage, OperationalState } from "@/types/chat";

export function useChatSession() {
  const [state, dispatch] = useReducer(
    chatSessionReducer,
    undefined,
    createInitialChatSessionState,
  );
  const [voiceEnabled, setVoiceEnabled] = useState(true);
  const voiceEnabledRef = useRef(true);
  const sessionIdRef = useRef(0);
  const turnGenerationRef = useRef(0);
  const conversationLoadAbortRef = useRef<AbortController | null>(null);
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
  const canSend =
    state.input.trim().length > 0 &&
    state.chatState !== "thinking" &&
    !state.isConversationLoading;
  const canReplay = voiceEnabled && hasReplay && !isBusy;

  const latestAssistantMessage = [...state.messages]
    .reverse()
    .find((message) => message.role === "assistant");
  const visibleReply =
    operationalState === "thinking"
      ? "..."
      : state.decision.speech ||
        latestAssistantMessage?.content ||
        (state.error ? "The local service did not respond correctly. Please try again." : "");

  async function sendMessage() {
    const text = state.input.trim();
    if (!text || state.chatState === "thinking" || state.isConversationLoading) {
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
      if (voiceEnabledRef.current) {
        await speakDecision(
          completed.response.assistantMessageId,
          completed.response.decision,
          completed.response.speechCapability,
          identity,
        );
      } else {
        scheduleWithoutSpeechReset(completed.response.decision, identity);
      }
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

  async function loadConversation(conversationId: string) {
    if (conversationId === state.conversationId && !state.isConversationLoading) {
      return;
    }

    // Switching archives is a session boundary: cancel any in-flight generation,
    // speech, stale archive request, and timers before accepting persisted state.
    sessionIdRef.current += 1;
    turnGenerationRef.current += 1;
    resetTurnTransport();
    clearSpeech();
    clearNeutralResetTimer();
    conversationLoadAbortRef.current?.abort();

    const sessionId = sessionIdRef.current;
    const controller = new AbortController();
    conversationLoadAbortRef.current = controller;
    dispatch({ type: "conversationLoadStarted" });

    try {
      const conversation = await getConversation(conversationId, controller.signal);
      if (controller.signal.aborted || sessionIdRef.current !== sessionId) {
        return;
      }

      dispatch({
        type: "conversationLoaded",
        conversationId: conversation.id,
        messages: conversation.messages,
      });
    } catch (caught) {
      if (controller.signal.aborted || sessionIdRef.current !== sessionId) {
        return;
      }

      dispatch({
        type: "conversationLoadFailed",
        message:
          caught instanceof Error ? caught.message : "Unable to load the local conversation archive.",
      });
    } finally {
      if (conversationLoadAbortRef.current === controller) {
        conversationLoadAbortRef.current = null;
      }
    }
  }

  function toggleVoice() {
    const next = !voiceEnabledRef.current;
    voiceEnabledRef.current = next;
    setVoiceEnabled(next);

    if (!next) {
      stopSpeech();
      dispatch({ type: "ttsErrorChanged", message: null });
    }
  }

  function resetConversation() {
    // Identity changes first: cancellation is best-effort, identity is the final
    // correctness barrier against already-resolved stale Promise callbacks.
    sessionIdRef.current += 1;
    turnGenerationRef.current += 1;
    resetTurnTransport();
    conversationLoadAbortRef.current?.abort();
    conversationLoadAbortRef.current = null;
    clearSpeech();
    clearNeutralResetTimer();
    dispatch({ type: "reset" });
  }

  return {
    messages: state.messages,
    conversationId: state.conversationId,
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
    voiceEnabled,
    isConversationLoading: state.isConversationLoading,
    archiveRevision: state.archiveRevision,
    isLogOpen: state.isLogOpen,
    setIsLogOpen: (open: boolean) => dispatch({ type: "logOpenChanged", open }),
    sendMessage,
    replayVoice,
    loadConversation,
    toggleVoice,
    resetConversation,
  };
}
