// This file owns the local text interaction loop: Qwen chat, CosyVoice3 TTS playback, expression timing, telemetry, and conversation-log composition.
"use client";

import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { LoaderCircleIcon, SendIcon } from "lucide-react";
import { AvatarStage } from "@/components/AvatarStage";
import { ChatLogModal } from "@/components/ChatLogModal";
import { TelemetrySidebar } from "@/components/TelemetrySidebar";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
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

const INITIAL_DECISION: AvatarDecision = {
  speech: "",
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

export function ChatPanel() {
  const [messages, setMessages] = useState<ChatMessage[]>(INITIAL_MESSAGES);
  const [input, setInput] = useState("");
  const [decision, setDecision] = useState<AvatarDecision>(INITIAL_DECISION);
  const [telemetry, setTelemetry] = useState<ModelTelemetry | null>(null);
  const [chatState, setChatState] = useState<ChatState>("idle");
  const [animationKey, setAnimationKey] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [ttsError, setTtsError] = useState<string | null>(null);
  const [isLogOpen, setIsLogOpen] = useState(false);
  const neutralResetTimerRef = useRef<number | null>(null);

  const {
    speechState,
    hasReplay,
    speak,
    replay,
    stopSpeech,
    clearSpeech,
  } = useAvatarSpeech({
    onError: (message) => {
      setTtsError(message);
    },
  });

  const operationalState: OperationalState =
    chatState !== "idle"
      ? chatState
      : speechState !== "idle"
        ? speechState
        : "idle";

  const isBusy = !["idle", "error"].includes(operationalState);
  const canSend = input.trim().length > 0 && !isBusy;

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

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const text = input.trim();
    if (!text || isBusy) {
      return;
    }

    stopSpeech();
    clearNeutralResetTimer();

    const userMessage: ChatMessage = { role: "user", content: text };
    const nextMessages = [...messages, userMessage];

    setMessages(nextMessages);
    setInput("");
    setError(null);
    setTtsError(null);
    setChatState("thinking");
    setAnimationKey((current) => current + 1);

    try {
      const response = await sendChatRequest({
        // Keep the local 4B context intentionally short so response latency stays predictable.
        messages: nextMessages.slice(-12),
      });

      const assistantMessage: ChatMessage = {
        role: "assistant",
        content: response.decision.speech,
      };

      setMessages((current) => [...current, assistantMessage]);
      setDecision(response.decision);
      setTelemetry(response.telemetry);
      setChatState("idle");
      setAnimationKey((current) => current + 1);

      if (!response.decision.speech.trim()) {
        scheduleNeutralReset(response.decision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
        return;
      }

      try {
        await speak({
          text: response.decision.speech,
          emotion: response.decision.emotion,
          emotionIntensity: response.decision.emotionIntensity,
        });
        scheduleNeutralReset(
          response.decision,
          EXPRESSION_HOLD_AFTER_SPEECH_MS,
        );
      } catch {
        // TTS failure should not discard the successful Qwen text response.
        scheduleNeutralReset(response.decision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
      }
    } catch (caught) {
      const message =
        caught instanceof Error ? caught.message : "Unknown local-model error.";

      const fallbackDecision: AvatarDecision = {
        speech: "",
        emotion: "confused",
        emotionIntensity: 0.5,
        gesture: "none",
        gestureIntensity: 0,
      };

      setError(message);
      setDecision(fallbackDecision);
      setChatState("error");
      setAnimationKey((current) => current + 1);
      scheduleNeutralReset(fallbackDecision, EXPRESSION_HOLD_WITHOUT_TTS_MS);
    }
  }

  async function handleReplayVoice() {
    if (!hasReplay || isBusy) {
      return;
    }

    clearNeutralResetTimer();
    setTtsError(null);

    try {
      await replay();
      scheduleNeutralReset(decision, EXPRESSION_HOLD_AFTER_SPEECH_MS);
    } catch {
      // The speech hook already surfaces the readable playback error.
    }
  }

  function resetConversation() {
    clearSpeech();
    clearNeutralResetTimer();
    setMessages(INITIAL_MESSAGES);
    setInput("");
    setDecision(INITIAL_DECISION);
    setTelemetry(null);
    setChatState("idle");
    setAnimationKey((current) => current + 1);
    setError(null);
    setTtsError(null);
    setIsLogOpen(false);
  }

  function scheduleNeutralReset(
    latestDecision: AvatarDecision,
    delayMs: number,
  ) {
    clearNeutralResetTimer();

    const hasTemporaryExpression =
      latestDecision.emotion !== "neutral" || latestDecision.gesture !== "none";

    if (!hasTemporaryExpression) {
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
      setAnimationKey((current) => current + 1);
      neutralResetTimerRef.current = null;
    }, delayMs);
  }

  function clearNeutralResetTimer() {
    if (neutralResetTimerRef.current !== null) {
      window.clearTimeout(neutralResetTimerRef.current);
      neutralResetTimerRef.current = null;
    }
  }

  return (
    <main className="flex min-h-dvh justify-center bg-muted p-[var(--avatar-page-gutter)] text-foreground [align-items:var(--avatar-page-align)]">
      <div className="mx-auto grid w-full min-w-0 gap-[var(--avatar-layout-gap)] [grid-template-columns:var(--avatar-layout-columns)] [height:var(--avatar-shell-height)]">
        <section className="grid min-h-0 min-w-0 grid-rows-[minmax(0,1fr)_auto] gap-[var(--avatar-layout-gap)]">
          <AvatarStage
            emotion={decision.emotion}
            emotionIntensity={decision.emotionIntensity}
            gesture={decision.gesture}
            gestureIntensity={decision.gestureIntensity}
            operationalState={operationalState}
            animationKey={animationKey}
            reply={visibleReply}
          />

          <Card className="gap-0 rounded-3xl border-border bg-background py-0 shadow-sm">
            <CardContent className="p-[var(--avatar-input-card-pad)]">
              <form
                className="grid min-w-0 items-stretch gap-[var(--avatar-control-gap)] [grid-template-columns:var(--avatar-input-columns)]"
                onSubmit={handleSubmit}
              >
                <label htmlFor="chat-input" className="sr-only">
                  Message
                </label>
                <Textarea
                  id="chat-input"
                  value={input}
                  onChange={(event) => setInput(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter" && !event.shiftKey) {
                      event.preventDefault();
                      event.currentTarget.form?.requestSubmit();
                    }
                  }}
                  placeholder="输入文字… / Type a message… / Skriv något…"
                  rows={2}
                  maxLength={2000}
                  className="h-full min-h-[var(--avatar-input-height)] resize-none rounded-2xl border-input bg-background px-[var(--avatar-input-padding-x)] py-[var(--avatar-input-padding-y)] text-[var(--avatar-input-text-size)] leading-[1.6] text-foreground placeholder:text-muted-foreground"
                />

                <Button
                  type="submit"
                  size="lg"
                  disabled={!canSend}
                  className="min-h-[var(--avatar-input-height)] min-w-0 self-stretch rounded-2xl [width:var(--avatar-input-button-width)]"
                >
                  {operationalState === "thinking" ? (
                    <>
                      <LoaderCircleIcon
                        data-icon="inline-start"
                        className="animate-spin"
                      />
                      Thinking
                    </>
                  ) : (
                    <>
                      <SendIcon data-icon="inline-start" />
                      Send
                    </>
                  )}
                </Button>
              </form>
            </CardContent>
          </Card>
        </section>

        <TelemetrySidebar
          decision={decision}
          telemetry={telemetry}
          operationalState={operationalState}
          ttsState={speechState}
          error={error}
          ttsError={ttsError}
          canReplay={hasReplay && !isBusy}
          onReplay={() => void handleReplayVoice()}
          onOpenLog={() => setIsLogOpen(true)}
          onReset={resetConversation}
        />
      </div>

      <ChatLogModal
        open={isLogOpen}
        messages={messages}
        operationalState={operationalState}
        onClose={() => setIsLogOpen(false)}
      />
    </main>
  );
}
