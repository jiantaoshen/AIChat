// This component shows the current local conversation history in an accessible scrollable dialog and follows new messages while open.
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
    <Dialog open={open} onOpenChange={(nextOpen) => !nextOpen && onClose()}>
      <DialogContent className="flex h-[80vh] w-[900px] max-w-[calc(100vw-2rem)] flex-col gap-0 overflow-hidden rounded-3xl p-0">
        <DialogHeader className="shrink-0 gap-1 border-b px-5 py-4 pr-14">
          <span className="text-xs font-semibold uppercase tracking-widest text-primary">
            Session history
          </span>
          <DialogTitle>Conversation log</DialogTitle>
          <DialogDescription>
            All user and avatar messages in the current local session.
          </DialogDescription>
        </DialogHeader>

        <ScrollArea className="min-h-0 flex-1 rounded-none">
          <div className="flex flex-col gap-3 p-4">
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
                    className="mb-1.5 h-5 px-2 text-xs uppercase tracking-wider text-muted-foreground"
                  >
                    {message.role === "user" ? "You" : "Avatar"}
                  </Badge>
                  <p className="whitespace-pre-wrap text-sm leading-6">
                    {message.content}
                  </p>
                </article>
              );
            })}

            {operationalState === "thinking" && (
              <article className="mr-auto max-w-[86%] rounded-xl border bg-muted px-4 py-3">
                <Badge
                  variant="outline"
                  className="mb-1.5 h-5 px-2 text-xs uppercase tracking-wider text-muted-foreground"
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
