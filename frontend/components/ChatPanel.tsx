// This file owns the MVP interaction loop and composes the Hybrid Fluid visual-novel stage, shadcn/ui input dock, telemetry rail, timed neutral reset, and conversation-log dialog.
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
import { sendChatRequest } from "@/lib/api";
import type {
  AvatarDecision,
  ChatMessage,
  ModelTelemetry,
  OperationalState,
} from "@/types/chat";

const EXPRESSION_HOLD_MS = 4500;

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
  const [operationalState, setOperationalState] =
    useState<OperationalState>("idle");
  const [animationKey, setAnimationKey] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [isLogOpen, setIsLogOpen] = useState(false);
  const neutralResetTimerRef = useRef<number | null>(null);

  const canSend = input.trim().length > 0 && operationalState !== "thinking";
  const latestAssistantMessage = [...messages]
    .reverse()
    .find((message) => message.role === "assistant");

  const visibleReply =
    operationalState === "thinking"
      ? "……"
      : error
        ? "本地模型似乎没有正常回应。请检查右侧状态。"
        : decision.speech || latestAssistantMessage?.content || "";

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
    if (!text || operationalState === "thinking") {
      return;
    }

    clearNeutralResetTimer();

    const userMessage: ChatMessage = { role: "user", content: text };
    const nextMessages = [...messages, userMessage];

    setMessages(nextMessages);
    setInput("");
    setError(null);
    setOperationalState("thinking");
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
      setOperationalState("idle");
      setAnimationKey((current) => current + 1);
      scheduleNeutralReset(response.decision);
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
      setOperationalState("error");
      setAnimationKey((current) => current + 1);
      scheduleNeutralReset(fallbackDecision);
    }
  }

  function resetConversation() {
    clearNeutralResetTimer();
    setMessages(INITIAL_MESSAGES);
    setDecision(INITIAL_DECISION);
    setTelemetry(null);
    setOperationalState("idle");
    setAnimationKey((current) => current + 1);
    setError(null);
    setIsLogOpen(false);
  }

  function scheduleNeutralReset(latestDecision: AvatarDecision) {
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
    }, EXPRESSION_HOLD_MS);
  }

  function clearNeutralResetTimer() {
    if (neutralResetTimerRef.current !== null) {
      window.clearTimeout(neutralResetTimerRef.current);
      neutralResetTimerRef.current = null;
    }
  }

  return (
    <main className="avatar-page">
      <div className="avatar-shell">
        <section className="avatar-main">
          <AvatarStage
            emotion={decision.emotion}
            emotionIntensity={decision.emotionIntensity}
            gesture={decision.gesture}
            gestureIntensity={decision.gestureIntensity}
            operationalState={operationalState}
            animationKey={animationKey}
            reply={visibleReply}
          />

          <Card className="avatar-input-card">
            <CardContent className="avatar-input-content">
              <form className="avatar-input-form" onSubmit={handleSubmit}>
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
                  className="avatar-input-textarea"
                />
                <Button
                  type="submit"
                  size="lg"
                  disabled={!canSend}
                  className="avatar-send-button"
                >
                  {operationalState === "thinking" ? (
                    <>
                      <LoaderCircleIcon
                        data-icon="inline-start"
                        className="avatar-loading-icon"
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
          error={error}
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
