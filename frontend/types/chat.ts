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
  | "unsupported"
  | "error";

export type MessageRole = "user" | "assistant";

export interface ChatMessage {
  role: MessageRole;
  content: string;
}

export interface AvatarDecision {
  speech: string;
  language: string;
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

export interface SpeechCapability {
  supported: boolean;
}

export interface AvatarChatResponse {
  conversationId: string;
  assistantMessageId: string;
  decision: AvatarDecision;
  telemetry: ModelTelemetry;
  speechCapability: SpeechCapability;
}

export interface ChatRequest {
  conversationId: string | null;
  turnId: string;
  message: string;
}

export interface SpeechSynthesisRequest {
  messageId: string;
}
