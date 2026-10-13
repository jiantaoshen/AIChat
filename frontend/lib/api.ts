// This file wraps browser calls to the local ASP.NET Core backend for Qwen chat and CosyVoice3 avatar speech, and converts failed HTTP responses into readable UI errors.
import type {
  AvatarChatResponse,
  ChatRequest,
  ConversationArchiveItem,
  ConversationSnapshot,
  SpeechSynthesisRequest,
} from "@/types/chat";

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5191";

export async function sendChatRequest(
  request: ChatRequest,
  signal?: AbortSignal,
): Promise<AvatarChatResponse> {
  const response = await fetch(`${API_BASE_URL}/api/chat`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(
      extractProblemDetail(details) ?? `Backend returned HTTP ${response.status}.`,
    );
  }

  return (await response.json()) as AvatarChatResponse;
}

export async function listConversations(
  signal?: AbortSignal,
): Promise<ConversationArchiveItem[]> {
  const response = await fetch(`${API_BASE_URL}/api/conversations`, { signal });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(
      extractProblemDetail(details) ?? `Backend returned HTTP ${response.status}.`,
    );
  }

  return (await response.json()) as ConversationArchiveItem[];
}

export async function getConversation(
  conversationId: string,
  signal?: AbortSignal,
): Promise<ConversationSnapshot> {
  const response = await fetch(
    `${API_BASE_URL}/api/conversations/${encodeURIComponent(conversationId)}`,
    { signal },
  );

  if (!response.ok) {
    const details = await response.text();
    throw new Error(
      extractProblemDetail(details) ?? `Backend returned HTTP ${response.status}.`,
    );
  }

  return (await response.json()) as ConversationSnapshot;
}

export async function synthesizeSpeech(
  request: SpeechSynthesisRequest,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/api/speech`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(
      extractProblemDetail(details) ?? `TTS returned HTTP ${response.status}.`,
    );
  }

  return await response.blob();
}

function extractProblemDetail(raw: string): string | null {
  try {
    const parsed = JSON.parse(raw) as { detail?: string; error?: string };
    return parsed.detail ?? parsed.error ?? null;
  } catch {
    return raw.trim() || null;
  }
}
