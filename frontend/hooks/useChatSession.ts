// This hook owns one browser chat session: messages, backend requests, avatar decisions, TTS playback, errors, replay, and temporary-expression timing.
"use client";

import { useEffect, useRef, useState } from "react";
import { useAvatarSpeech } from "@/hooks/useAvatarSpeech";
import { sendChatRequest } from "@/lib/api";
import type {
  AvatarDecision,
  ChatMessage,
  ModelTelemetry,
  OperationalState,
} from "@/types/chat";

const EXPRESSION_HOLD_WITHOUT_TTS_MS = 4500;
const EXPRESSION_HOLD_AFTER_SPEECH_MS = 1500;

type ChatState = "idle" | "thinking" | "error";

interface RetryableTurn {
  conversationId: string | null;
  turnId: string;
  message: string;
}

const INITIAL_DECISION: AvatarDecision = {
  speech: "",
  language: "zh",
  emotion: "neutral",
  emotionIntensity: 0.22,
  gesture: "none",
  gestureIntensity: 0,
};

const INITIAL_MESSAGES: ChatMessage[] = [
  {
    role: "assistant",
    content: "你好。想聊点什么？你也可以直接用 English 或 svenska。",
  },
];

export function useChatSession() {
  const [messages, setMessages] = useState<ChatMessage[]>(INITIAL_MESSAGES);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [decision, setDecision] = useState<AvatarDecision>(INITIAL_DECISION);
  const [telemetry, setTelemetry] = useState<ModelTelemetry | null>(null);
  const [chatState, setChatState] = useState<ChatState>("idle");
  const [animationKey, setAnimationKey] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [ttsError, setTtsError] = useState<string | null>(null);
  const [isLogOpen, setIsLogOpen] = useState(false);
  const neutralResetTimerRef = useRef<number | null>(null);
  // Identifies the latest user turn. A new message can interrupt TTS playback,
  // so older speech completions must not schedule UI work for the new turn.
  const turnGenerationRef = useRef(0);
  // Keeps one failed logical turn stable across retries so the backend can
  // return the already-committed result instead of executing the turn twice.
  const retryableTurnRef = useRef<RetryableTurn | null>(null);

  const {
    speechState,
    hasReplay,
    speak,
    replay,
    stopSpeech,
    clearSpeech,
  } = useAvatarSpeech({
    onError: setTtsError,
  });

  const operationalState: OperationalState =
    chatState !== "idle" ? chatState : speechState;
  const isBusy = operationalState !== "idle" && operationalState !== "error";
  // Text generation itself remains single-flight, but TTS synthesis/playback is
  // interruptible: typing and sending the next message should stop the voice.
  const canSend = input.trim().length > 0 && chatState !== "thinking";
  const canReplay = hasReplay && !isBusy;

  const latestAssistantMessage = [...messages]
    .reverse()
    .find((message) => message.role === "assistant");

  const visibleReply =
    operationalState === "thinking"
      ? "……"
      : decision.speech ||
        latestAssistantMessage?.content ||
        (error ? "本地服务似乎没有正常回应。请检查右侧状态。" : "");

  useEffect(() => {
    return () => {
      if (neutralResetTimerRef.current !== null) {
        window.clearTimeout(neutralResetTimerRef.current);
      }
    };
  }, []);

  async function sendMessage() {
    const text = input.trim();
    if (!text || chatState === "thinking") {
      return;
    }

    const turnGeneration = ++turnGenerationRef.current;
    stopSpeech();
    clearNeutralResetTimer();

    const retryable = retryableTurnRef.current;
    const isRetry =
      retryable?.conversationId === conversationId &&
      retryable.message === text;
    const turnId =
      isRetry && retryable ? retryable.turnId : crypto.randomUUID();

    const messagesBeforeTurn = messages;
    const userMessage: ChatMessage = { role: "user", content: text };
    const nextMessages = [...messagesBeforeTurn, userMessage];

    setMessages(nextMessages);
    setInput("");
    setError(null);
    setTtsError(null);
    setChatState("thinking");
    bumpAnimation();

    try {
      const response = await sendChatRequest({
        conversationId,
        turnId,
        message: text,
      });

      if (turnGeneration !== turnGenerationRef.current) {
        return;
      }

      retryableTurnRef.current = null;
      setConversationId(response.conversationId);
      setMessages((current) => [
        ...current,
        { role: "assistant", content: response.decision.speech },
      ]);
      setDecision(response.decision);
      setTelemetry(response.telemetry);
      setChatState("idle");
      bumpAnimation();

      if (!response.decision.speech.trim()) {
        scheduleNeutralReset(response.decision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
        return;
      }

      try {
        const speechResult = await speak({
          messageId: response.assistantMessageId,
          text: response.decision.speech,
          language: response.decision.language,
          emotion: response.decision.emotion,
          emotionIntensity: response.decision.emotionIntensity,
        });

        if (turnGeneration !== turnGenerationRef.current) {
          return;
        }

        scheduleNeutralReset(
          response.decision,
          speechResult === "completed"
            ? EXPRESSION_HOLD_AFTER_SPEECH_MS
            : EXPRESSION_HOLD_WITHOUT_TTS_MS,
        );
      } catch {
        // A TTS failure must not discard a successful text response.
        if (turnGeneration !== turnGenerationRef.current) {
          return;
        }

        scheduleNeutralReset(response.decision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
      }
    } catch (caught) {
      if (turnGeneration !== turnGenerationRef.current) {
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

      retryableTurnRef.current = {
        conversationId,
        turnId,
        message: text,
      };
      // The backend owns persistent conversation state. If this logical turn did
      // not produce a confirmed response, remove its optimistic UI message and
      // put the text back so a retry can reuse the same turnId.
      setMessages(messagesBeforeTurn);
      setInput(text);
      setError(message);
      setDecision(fallbackDecision);
      setChatState("error");
      bumpAnimation();
      scheduleNeutralReset(fallbackDecision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
    }
  }

  async function replayVoice() {
    if (!canReplay) {
      return;
    }

    clearNeutralResetTimer();
    setTtsError(null);

    try {
      await replay();
      scheduleNeutralReset(decision, EXPRESSION_HOLD_AFTER_SPEECH_MS);
    } catch {
      // useAvatarSpeech already exposes the readable playback error.
    }
  }

  function resetConversation() {
    turnGenerationRef.current += 1;
    retryableTurnRef.current = null;
    clearSpeech();
    clearNeutralResetTimer();
    setMessages(INITIAL_MESSAGES);
    setConversationId(null);
    setInput("");
    setDecision(INITIAL_DECISION);
    setTelemetry(null);
    setChatState("idle");
    setError(null);
    setTtsError(null);
    setIsLogOpen(false);
    bumpAnimation();
  }

  function scheduleNeutralReset(
    latestDecision: AvatarDecision,
    delayMs: number,
  ) {
    clearNeutralResetTimer();

    if (
      latestDecision.emotion === "neutral" &&
      latestDecision.gesture === "none"
    ) {
      return;
    }

    neutralResetTimerRef.current = window.setTimeout(() => {
      setDecision((current) => ({
        ...current,
        emotion: "neutral",
        emotionIntensity: INITIAL_DECISION.emotionIntensity,
        gesture: "none",
        gestureIntensity: 0,
      }));
      bumpAnimation();
      neutralResetTimerRef.current = null;
    }, delayMs);
  }

  function clearNeutralResetTimer() {
    if (neutralResetTimerRef.current === null) {
      return;
    }

    window.clearTimeout(neutralResetTimerRef.current);
    neutralResetTimerRef.current = null;
  }

  function bumpAnimation() {
    setAnimationKey((current) => current + 1);
  }

  return {
    messages,
    input,
    setInput,
    decision,
    telemetry,
    operationalState,
    animationKey,
    error,
    ttsError,
    speechState,
    visibleReply,
    canSend,
    canReplay,
    isLogOpen,
    setIsLogOpen,
    sendMessage,
    replayVoice,
    resetConversation,
  };
}
