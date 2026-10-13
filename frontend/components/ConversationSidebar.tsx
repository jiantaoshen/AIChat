// This component renders the SQLite-backed conversation archive and user-facing voice/session controls in the right sidebar.
"use client";

import { useEffect, useMemo, useState } from "react";
import {
  LoaderCircleIcon,
  MessageSquareTextIcon,
  PlusIcon,
  Trash2Icon,
  Volume2Icon,
  VolumeXIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { deleteConversation, listConversations } from "@/lib/api";
import type { ConversationArchiveItem } from "@/types/chat";

interface ConversationSidebarProps {
  activeConversationId: string | null;
  archiveRevision: number;
  isConversationLoading: boolean;
  voiceEnabled: boolean;
  onSelectConversation: (conversationId: string) => void;
  onNewConversation: () => void;
  onToggleVoice: () => void;
  onOpenLog: () => void;
}

export function ConversationSidebar({
  activeConversationId,
  archiveRevision,
  isConversationLoading,
  voiceEnabled,
  onSelectConversation,
  onNewConversation,
  onToggleVoice,
  onOpenLog,
}: ConversationSidebarProps) {
  const [conversations, setConversations] = useState<ConversationArchiveItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [conversationToDelete, setConversationToDelete] =
    useState<ConversationArchiveItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    async function refresh() {
      setIsLoading(true);
      setError(null);

      try {
        const items = await listConversations(controller.signal);
        setConversations(items);
      } catch {
        if (controller.signal.aborted) {
          return;
        }

        setError("无法读取本地对话存档，请稍后重试。");
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    void refresh();
    return () => controller.abort();
  }, [archiveRevision]);

  const dateFormatter = useMemo(
    () =>
      new Intl.DateTimeFormat(undefined, {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      }),
    [],
  );

  function requestDelete(conversation: ConversationArchiveItem) {
    setDeleteError(null);
    setConversationToDelete(conversation);
  }

  async function confirmDelete() {
    if (!conversationToDelete || isDeleting) {
      return;
    }

    const deletingConversation = conversationToDelete;
    setIsDeleting(true);
    setDeleteError(null);

    try {
      await deleteConversation(deletingConversation.id);
      setConversations((current) =>
        current.filter((item) => item.id !== deletingConversation.id),
      );

      if (activeConversationId === deletingConversation.id) {
        onNewConversation();
      }

      setConversationToDelete(null);
    } catch (caught) {
      setDeleteError(
        caught instanceof Error ? caught.message : "删除对话失败，请稍后重试。",
      );
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <>
      <Card className="flex h-[calc(100dvh-2rem)] min-h-0 max-h-[calc(100dvh-2rem)] flex-col gap-3 overflow-hidden rounded-3xl p-4 shadow-sm">
        <CardHeader className="shrink-0 gap-3 border-b p-0 pb-4">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <p className="text-xs font-bold uppercase tracking-widest text-primary">
                本地保存
              </p>
              <CardTitle className="truncate text-xl">对话存档</CardTitle>
            </div>
            <Button
              type="button"
              size="icon"
              variant="outline"
              title="新对话"
              aria-label="新对话"
              onClick={onNewConversation}
            >
              <PlusIcon />
            </Button>
          </div>

          <Button
            type="button"
            variant={voiceEnabled ? "secondary" : "outline"}
            className="w-full justify-start"
            onClick={onToggleVoice}
            aria-pressed={voiceEnabled}
          >
            {voiceEnabled ? <Volume2Icon /> : <VolumeXIcon />}
            语音：{voiceEnabled ? "开" : "关"}
          </Button>
        </CardHeader>

        <CardContent className="flex min-h-0 flex-1 flex-col p-0">
          <ScrollArea className="min-h-0 flex-1">
            <div className="flex flex-col gap-2 pr-2">
              {isLoading && conversations.length === 0 && (
                <div className="flex items-center gap-2 rounded-2xl border bg-muted px-3 py-4 text-sm text-muted-foreground">
                  <LoaderCircleIcon className="size-4 animate-spin" />
                  正在读取对话存档…
                </div>
              )}

              {error && (
                <div className="rounded-2xl border border-destructive/30 bg-destructive/5 px-3 py-3 text-xs leading-5 text-destructive">
                  {error}
                </div>
              )}

              {!isLoading && !error && conversations.length === 0 && (
                <div className="rounded-2xl border border-dashed px-3 py-5 text-center text-sm text-muted-foreground">
                  还没有已保存的对话。
                </div>
              )}

              {conversations.map((conversation) => {
                const isActive = conversation.id === activeConversationId;

                return (
                  <div key={conversation.id} className="relative">
                    <button
                      type="button"
                      disabled={isConversationLoading || isDeleting}
                      onClick={() => onSelectConversation(conversation.id)}
                      className={`w-full rounded-2xl border px-3 py-3 pr-11 text-left transition-colors disabled:cursor-wait disabled:opacity-60 ${
                        isActive
                          ? "border-primary/50 bg-primary/10"
                          : "border-border bg-card hover:bg-muted"
                      }`}
                      aria-current={isActive ? "true" : undefined}
                    >
                      <span className="flex items-start gap-2">
                        <MessageSquareTextIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" />
                        <span className="min-w-0 flex-1">
                          <strong className="block truncate text-sm font-semibold">
                            {conversation.title || "未命名对话"}
                          </strong>
                          <span className="mt-1 flex items-center justify-between gap-2 text-xs text-muted-foreground">
                            <span>{conversation.messageCount} 条消息</span>
                            <time dateTime={conversation.updatedAtUtc}>
                              {formatArchiveDate(
                                conversation.updatedAtUtc,
                                dateFormatter,
                              )}
                            </time>
                          </span>
                        </span>
                      </span>
                    </button>

                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      className="absolute right-1.5 top-1.5 text-destructive hover:bg-destructive/10 hover:text-destructive"
                      title="删除对话"
                      aria-label={`删除对话：${conversation.title || "未命名对话"}`}
                      disabled={isConversationLoading || isDeleting}
                      onClick={() => requestDelete(conversation)}
                    >
                      <Trash2Icon />
                    </Button>
                  </div>
                );
              })}
            </div>
          </ScrollArea>

          <div className="grid shrink-0 gap-3 border-t pt-4">
            <Button type="button" size="lg" onClick={onOpenLog}>
              <MessageSquareTextIcon />
              Conversation log
            </Button>
          </div>
        </CardContent>
      </Card>

      <Dialog
        open={conversationToDelete !== null}
        onOpenChange={(open) => {
          if (!open && !isDeleting) {
            setConversationToDelete(null);
            setDeleteError(null);
          }
        }}
      >
        <DialogContent className="max-w-md p-6" showCloseButton={!isDeleting}>
          <DialogHeader>
            <DialogTitle>删除对话？</DialogTitle>
            <DialogDescription>
              确认删除“{conversationToDelete?.title || "未命名对话"}”？该会话及其消息记录将永久删除，此操作无法撤销。
            </DialogDescription>
          </DialogHeader>

          {deleteError && (
            <div className="mt-4 rounded-xl border border-destructive/30 bg-destructive/5 px-3 py-2 text-sm text-destructive">
              {deleteError}
            </div>
          )}

          <DialogFooter className="mt-6">
            <Button
              type="button"
              variant="outline"
              disabled={isDeleting}
              onClick={() => {
                setConversationToDelete(null);
                setDeleteError(null);
              }}
            >
              取消
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={isDeleting}
              onClick={() => void confirmDelete()}
            >
              {isDeleting && <LoaderCircleIcon className="animate-spin" />}
              确认删除
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

function formatArchiveDate(
  value: string,
  formatter: Intl.DateTimeFormat,
): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : formatter.format(date);
}
