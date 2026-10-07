// This component renders only the chat text input and send button; session state and network behavior stay in useChatSession.
"use client";

import type { FormEvent, KeyboardEvent } from "react";
import { LoaderCircleIcon, SendIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";

interface ChatComposerProps {
  value: string;
  canSend: boolean;
  isThinking: boolean;
  isSpeechActive: boolean;
  onChange: (value: string) => void;
  onSend: () => void;
}

export function ChatComposer({
  value,
  canSend,
  isThinking,
  isSpeechActive,
  onChange,
  onSend,
}: ChatComposerProps) {
  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSend();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      event.currentTarget.form?.requestSubmit();
    }
  }

  return (
    <Card className="rounded-3xl py-0 shadow-sm">
      <CardContent className="p-3">
        <form
          className="grid grid-cols-1 gap-3 md:grid-cols-[minmax(0,1fr)_9rem]"
          onSubmit={handleSubmit}
        >
          <label htmlFor="chat-input" className="sr-only">
            Message
          </label>
          <Textarea
            id="chat-input"
            value={value}
            onChange={(event) => onChange(event.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="输入文字… / Type a message… / Skriv något…"
            rows={2}
            maxLength={2000}
            className="min-h-16 resize-none rounded-2xl px-4 py-3 text-[0.95rem] leading-6"
          />
          <Button
            type="submit"
            size="lg"
            disabled={!canSend}
            className="min-h-16 rounded-2xl"
          >
            {isThinking ? (
              <>
                <LoaderCircleIcon data-icon="inline-start" className="animate-spin" />
                Thinking
              </>
            ) : isSpeechActive ? (
              <>
                <SendIcon data-icon="inline-start" />
                Interrupt &amp; Send
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
  );
}
