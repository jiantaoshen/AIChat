// This file renders the complete conversation history in an accessible shadcn/ui Dialog with local Tailwind layout/presentation utilities.
"use client";

import { useEffect, useRef } from "react";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import type { ChatMessage, OperationalState } from "@/types/chat";

interface ChatLogModalProps {
  open: boolean;
  messages: ChatMessage[];
  operationalState: OperationalState;
  onClose: () => void;
}

export function ChatLogModal({
  open,
  messages,
  operationalState,
  onClose,
}: ChatLogModalProps) {
  const endRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    const frame = window.requestAnimationFrame(() => {
      endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
    });

    return () => window.cancelAnimationFrame(frame);
  }, [messages, open, operationalState]);

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (!nextOpen) {
          onClose();
        }
      }}
    >
      <DialogContent className="flex h-[min(78dvh,52rem)] w-[min(92vw,58rem)] max-w-none flex-col gap-0 overflow-hidden p-0 shadow-[0_1.75rem_5.5rem_rgb(24_32_51/22%)]">
        <DialogHeader className="shrink-0 gap-1 border-b border-border px-[clamp(1rem,1.25vw,1.5rem)] py-[clamp(1rem,1.1vw,1.35rem)]">
          <span className="text-xs font-bold uppercase tracking-[0.12em] text-primary">
            Session history
          </span>
          <DialogTitle className="text-lg">Conversation log</DialogTitle>
          <DialogDescription>
            All user and avatar messages in the current local session.
          </DialogDescription>
        </DialogHeader>

        <ScrollArea className="min-h-0 flex-1">
          <div className="flex flex-col gap-3 p-[clamp(0.9rem,1vw,1.25rem)]">
            {messages.map((message, index) => {
              const roleClass =
                message.role === "user"
                  ? "ml-auto border-primary/20 bg-primary/10"
                  : "mr-auto border-border bg-muted";

              return (
                <article
                  key={`${message.role}-${index}`}
                  className={`max-w-[86%] rounded-2xl border px-4 py-3 ${roleClass}`}
                >
                  <Badge
                    variant="outline"
                    className="mb-1.5 h-5 px-2 text-[0.6rem] uppercase tracking-[0.12em] text-muted-foreground"
                  >
                    {message.role === "user" ? "You" : "Avatar"}
                  </Badge>
                  <p className="whitespace-pre-wrap text-sm leading-6 text-foreground">
                    {message.content}
                  </p>
                </article>
              );
            })}

            {operationalState === "thinking" && (
              <article className="mr-auto max-w-[86%] rounded-2xl border border-border bg-muted px-4 py-3">
                <Badge
                  variant="outline"
                  className="mb-1.5 h-5 px-2 text-[0.6rem] uppercase tracking-[0.12em] text-muted-foreground"
                >
                  Avatar
                </Badge>
                <p className="animate-pulse text-sm text-primary">…</p>
              </article>
            )}

            <div ref={endRef} aria-hidden="true" />
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
