// This file renders the complete conversation history in an accessible shadcn/ui Dialog; visual styling is centralized in globals.css.
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
      <DialogContent className="avatar-log-dialog">
        <DialogHeader className="avatar-log-header">
          <span className="avatar-log-kicker">Session history</span>
          <DialogTitle className="avatar-log-title">Conversation log</DialogTitle>
          <DialogDescription>
            All user and avatar messages in the current local session.
          </DialogDescription>
        </DialogHeader>

        <ScrollArea className="avatar-log-scroll">
          <div className="avatar-log-list">
            {messages.map((message, index) => {
              const roleClass =
                message.role === "user"
                  ? "avatar-log-message-user"
                  : "avatar-log-message-assistant";

              return (
                <article
                  key={`${message.role}-${index}`}
                  className={`avatar-log-message ${roleClass}`}
                >
                  <Badge variant="outline" className="avatar-log-role">
                    {message.role === "user" ? "You" : "Avatar"}
                  </Badge>
                  <p className="avatar-log-copy">{message.content}</p>
                </article>
              );
            })}

            {operationalState === "thinking" && (
              <article className="avatar-log-message avatar-log-message-assistant">
                <Badge variant="outline" className="avatar-log-role">
                  Avatar
                </Badge>
                <p className="avatar-log-thinking">…</p>
              </article>
            )}

            <div ref={endRef} aria-hidden="true" />
          </div>
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
