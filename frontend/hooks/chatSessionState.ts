// This reducer owns browser-visible chat session state. Async effects may dispatch only after their session/turn identity is still current.
import type {
  AvatarChatResponse,
  AvatarDecision,
  ChatMessage,
  ModelTelemetry,
} from "@/types/chat";

export type ChatState = "idle" | "thinking" | "error";

export const INITIAL_DECISION: AvatarDecision = {
  speech: "",
  language: "zh",
  emotion: "neutral",
  emotionIntensity: 0.22,
  gesture: "none",
  gestureIntensity: 0,
};

const INITIAL_MESSAGES: ChatMessage[] = [
  {
    role: "assistant",
    content: "你好。想聊点什么？你也可以直接用 English 或 svenska。",
  },
];

export interface ChatSessionState {
  messages: ChatMessage[];
  conversationId: string | null;
  input: string;
  decision: AvatarDecision;
  telemetry: ModelTelemetry | null;
  chatState: ChatState;
  animationKey: number;
  error: string | null;
  ttsError: string | null;
  isLogOpen: boolean;
}

export type ChatSessionAction =
  | { type: "inputChanged"; value: string }
  | { type: "sendStarted"; userMessage: ChatMessage }
  | { type: "sendSucceeded"; response: AvatarChatResponse }
  | {
      type: "sendFailed";
      message: string;
      decision: AvatarDecision;
      retryMessage: string;
    }
  | { type: "ttsErrorChanged"; message: string | null }
  | { type: "neutralReset" }
  | { type: "logOpenChanged"; open: boolean }
  | { type: "reset" };

export function createInitialChatSessionState(): ChatSessionState {
  return {
    messages: [...INITIAL_MESSAGES],
    conversationId: null,
    input: "",
    decision: { ...INITIAL_DECISION },
    telemetry: null,
    chatState: "idle",
    animationKey: 0,
    error: null,
    ttsError: null,
    isLogOpen: false,
  };
}

export function chatSessionReducer(
  state: ChatSessionState,
  action: ChatSessionAction,
): ChatSessionState {
  switch (action.type) {
    case "inputChanged":
      return { ...state, input: action.value };

    case "sendStarted":
      return {
        ...state,
        messages: [...state.messages, action.userMessage],
        input: "",
        error: null,
        ttsError: null,
        chatState: "thinking",
        animationKey: state.animationKey + 1,
      };

    case "sendSucceeded":
      return {
        ...state,
        conversationId: action.response.conversationId,
        messages: [
          ...state.messages,
          { role: "assistant", content: action.response.decision.speech },
        ],
        decision: action.response.decision,
        telemetry: action.response.telemetry,
        chatState: "idle",
        animationKey: state.animationKey + 1,
      };

    case "sendFailed":
      return {
        ...state,
        // ChatTurnTransportController enforces one accepted text turn at a
        // time, so the final message is the one optimistic user message added
        // by sendStarted for this failed turn.
        messages: state.messages.slice(0, -1),
        input: action.retryMessage,
        error: action.message,
        decision: action.decision,
        chatState: "error",
        animationKey: state.animationKey + 1,
      };

    case "ttsErrorChanged":
      return { ...state, ttsError: action.message };

    case "neutralReset":
      return {
        ...state,
        decision: {
          ...state.decision,
          emotion: "neutral",
          emotionIntensity: INITIAL_DECISION.emotionIntensity,
          gesture: "none",
          gestureIntensity: 0,
        },
        animationKey: state.animationKey + 1,
      };

    case "logOpenChanged":
      return { ...state, isLogOpen: action.open };

    case "reset":
      return {
        ...createInitialChatSessionState(),
        animationKey: state.animationKey + 1,
      };
  }
}
