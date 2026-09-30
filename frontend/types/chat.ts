// This file defines frontend types shared by the local Qwen chat UI, SQLite-backed conversation flow, avatar renderer, CosyVoice3 TTS pipeline, and inference telemetry panel.
export type Emotion =
  | "neutral"
  | "happy"
  | "sad"
  | "angry"
  | "surprised"
  | "confused";

export type Gesture = "none" | "nod" | "shake" | "jump";

export type OperationalState =
  | "idle"
  | "thinking"
  | "synthesizing"
  | "speaking"
  | "error";

export type MessageRole = "user" | "assistant";

export interface ChatMessage {
  role: MessageRole;
  content: string;
}

export interface AvatarDecision {
  speech: string;
  emotion: Emotion;
  emotionIntensity: number;
  gesture: Gesture;
  gestureIntensity: number;
}

export interface ModelTelemetry {
  model: string;
  totalDurationMs: number | null;
  loadDurationMs: number | null;
  promptTokens: number | null;
  outputTokens: number | null;
}

export interface AvatarChatResponse {
  conversationId: string;
  assistantMessageId: string;
  decision: AvatarDecision;
  telemetry: ModelTelemetry;
}

export interface ChatRequest {
  conversationId: string | null;
  messages: ChatMessage[];
}

export interface SpeechSynthesisRequest {
  messageId: string;
  text: string;
  emotion: Emotion;
  emotionIntensity: number;
}
