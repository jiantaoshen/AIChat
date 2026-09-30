// This file renders the complete conversation history in an accessible shadcn/ui Dialog; page-specific presentation stays in Tailwind class names.
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
      <DialogContent className="flex max-w-none flex-col gap-0 overflow-hidden rounded-3xl p-0 shadow-2xl [height:var(--avatar-modal-height)] [width:var(--avatar-modal-width)]">
        <DialogHeader className="shrink-0 gap-1 border-b border-border px-[var(--avatar-modal-header-pad-x)] py-[var(--avatar-modal-header-pad-y)] pr-14">
          <span className="text-xs font-semibold uppercase tracking-widest text-primary">
            Session history
          </span>
          <DialogTitle className="text-lg">Conversation log</DialogTitle>
          <DialogDescription>
            All user and avatar messages in the current local session.
          </DialogDescription>
        </DialogHeader>

        <ScrollArea className="min-h-0 flex-1 rounded-none">
          <div className="flex flex-col gap-3 p-[var(--avatar-modal-body-pad)]">
            {messages.map((message, index) => {
              const roleClass =
                message.role === "user"
                  ? "ml-auto border-primary/20 bg-primary/10"
                  : "mr-auto border-border bg-muted";

              return (
                <article
                  key={`${message.role}-${index}`}
                  className={`max-w-[86%] rounded-xl border px-4 py-3 ${roleClass}`}
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
              <article className="mr-auto max-w-[86%] rounded-xl border border-border bg-muted px-4 py-3">
                <Badge
                  variant="outline"
                  className="mb-1.5 h-5 px-2 text-[0.6rem] uppercase tracking-[0.12em] text-muted-foreground"
                >
                  Avatar
                </Badge>
                <p className="animate-pulse text-sm text-muted-foreground">…</p>
              </article>
            )}

            <div ref={endRef} aria-hidden="true" />
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
