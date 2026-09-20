// This file defines frontend types shared by the chat UI, avatar renderer, local backend API, and inference telemetry panel.
export type Emotion =
  | "neutral"
  | "happy"
  | "sad"
  | "angry"
  | "surprised"
  | "confused";

export type Gesture = "none" | "nod" | "shake" | "jump";

export type OperationalState = "idle" | "thinking" | "error";

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
  decision: AvatarDecision;
  telemetry: ModelTelemetry;
}

export interface ChatRequest {
  messages: ChatMessage[];
}
