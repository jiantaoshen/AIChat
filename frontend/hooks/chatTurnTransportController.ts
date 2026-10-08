// This file owns the deterministic pending-request/retry state machine used by useChatTurnTransport and is testable without React.

export interface ChatTransportRequest {
  conversationId: string | null;
  turnId: string;
  message: string;
}

export interface ExecuteTurnOptions {
  sessionId: number;
  conversationId: string | null;
  message: string;
  onStart: () => void;
}

export interface CompletedTurn<TResponse> {
  turnId: string;
  response: TResponse;
}

interface ActiveTurn {
  sessionId: number;
  turnId: string;
  controller: AbortController;
}

interface RetryableTurn {
  sessionId: number;
  conversationId: string | null;
  turnId: string;
  message: string;
}

type SendRequest<TResponse> = (
  request: ChatTransportRequest,
  signal?: AbortSignal,
) => Promise<TResponse>;

type TurnIdFactory = () => string;

export class ChatTurnTransportController<TResponse> {
  private readonly sendRequest: SendRequest<TResponse>;
  private readonly createTurnId: TurnIdFactory;
  private activeTurn: ActiveTurn | null = null;
  private retryableTurn: RetryableTurn | null = null;

  constructor(
    sendRequest: SendRequest<TResponse>,
    createTurnId: TurnIdFactory = () => crypto.randomUUID(),
  ) {
    this.sendRequest = sendRequest;
    this.createTurnId = createTurnId;
  }

  reset() {
    this.activeTurn?.controller.abort();
    this.activeTurn = null;
    this.retryableTurn = null;
  }

  async execute({
    sessionId,
    conversationId,
    message,
    onStart,
  }: ExecuteTurnOptions): Promise<CompletedTurn<TResponse> | null> {
    // Text generation is truly single-flight. An active LLM turn owns the
    // transport until it succeeds, fails, or is explicitly reset. A second
    // execute must not preempt/abort it and must not run onStart.
    if (this.activeTurn !== null) {
      return null;
    }

    const retryable = this.retryableTurn;
    const isRetry =
      retryable?.sessionId === sessionId &&
      retryable.conversationId === conversationId &&
      retryable.message === message;
    const turnId = isRetry && retryable ? retryable.turnId : this.createTurnId();

    const controller = new AbortController();
    this.activeTurn = { sessionId, turnId, controller };
    onStart();

    try {
      const response = await this.sendRequest(
        { conversationId, turnId, message },
        controller.signal,
      );

      if (!this.isActiveTurn(sessionId, turnId)) {
        return null;
      }

      this.activeTurn = null;
      this.retryableTurn = null;
      return { turnId, response };
    } catch (caught) {
      if (controller.signal.aborted || !this.isActiveTurn(sessionId, turnId)) {
        return null;
      }

      this.activeTurn = null;
      this.retryableTurn = {
        sessionId,
        conversationId,
        turnId,
        message,
      };
      throw caught;
    }
  }

  private isActiveTurn(sessionId: number, turnId: string) {
    return (
      this.activeTurn?.sessionId === sessionId &&
      this.activeTurn.turnId === turnId
    );
  }
}
