// This component renders the compact right-side status panel for avatar state, model telemetry, TTS status, warnings, and session actions.
"use client";

import type { ReactNode } from "react";
import {
  ActivityIcon,
  AudioLinesIcon,
  BrainCircuitIcon,
  CpuIcon,
  MessageSquareTextIcon,
  RotateCcwIcon,
  SparklesIcon,
  Volume2Icon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import type { SpeechPlaybackState } from "@/hooks/useAvatarSpeech";
import type {
  AvatarDecision,
  ModelTelemetry,
  OperationalState,
} from "@/types/chat";

interface TelemetrySidebarProps {
  decision: AvatarDecision;
  telemetry: ModelTelemetry | null;
  operationalState: OperationalState;
  ttsState: SpeechPlaybackState;
  error: string | null;
  ttsError: string | null;
  canReplay: boolean;
  onReplay: () => void;
  onOpenLog: () => void;
  onReset: () => void;
}

export function TelemetrySidebar({
  decision,
  telemetry,
  operationalState,
  ttsState,
  error,
  ttsError,
  canReplay,
  onReplay,
  onOpenLog,
  onReset,
}: TelemetrySidebarProps) {
  const isThinking = operationalState === "thinking";
  const visibleEmotion = isThinking ? "thinking" : decision.emotion;
  const visibleGesture = isThinking ? "local" : decision.gesture;
  const emotionIntensity = Math.round(
    (isThinking ? 0.35 : decision.emotionIntensity) * 100,
  );
  const gestureIntensity = Math.round(
    (isThinking ? 0 : decision.gestureIntensity) * 100,
  );

  return (
    <Card className="h-full min-h-0 gap-4 overflow-y-auto rounded-3xl p-4 shadow-sm">
      <CardHeader className="gap-1 border-b p-0 pb-4">
        <CardDescription className="flex items-center gap-2 text-xs font-bold uppercase tracking-widest text-primary">
          <SparklesIcon className="size-3.5" />
          Local Avatar
        </CardDescription>
        <CardTitle className="text-xl">Control Panel</CardTitle>
        <div className="flex flex-wrap gap-2 pt-1">
          <Badge variant="secondary">LOCAL</Badge>
          <Badge
            variant={stateBadgeVariant(operationalState)}
            className="capitalize"
          >
            {formatOperationalState(operationalState)}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="flex min-h-0 flex-1 flex-col gap-3 p-0">
        <div className="grid gap-3">
          <TelemetryItem
            icon={<BrainCircuitIcon className="size-4" />}
            label="Expression"
            value={visibleEmotion}
            detail={`${emotionIntensity}% intensity`}
          />
          <TelemetryItem
            icon={<ActivityIcon className="size-4" />}
            label="Gesture"
            value={visibleGesture}
            detail={`${gestureIntensity}% intensity`}
          />
          <TelemetryItem
            icon={<CpuIcon className="size-4" />}
            label="Inference"
            value={formatDuration(telemetry?.totalDurationMs)}
            detail={
              telemetry?.loadDurationMs != null
                ? `load ${formatDuration(telemetry.loadDurationMs)}`
                : "local Ollama"
            }
          />
          <TelemetryItem
            icon={<AudioLinesIcon className="size-4" />}
            label="Voice"
            value={formatVoiceState(ttsState)}
            detail={
              ttsState === "unsupported"
                ? "language not supported by CosyVoice3"
                : "local CosyVoice3"
            }
          />
          <TelemetryItem
            icon={<MessageSquareTextIcon className="size-4" />}
            label="Tokens"
            value={
              telemetry
                ? `${telemetry.promptTokens ?? "?"} → ${telemetry.outputTokens ?? "?"}`
                : "—"
            }
            detail="prompt → output"
          />
        </div>

        <Card className="gap-2 rounded-2xl bg-muted py-3 shadow-none">
          <CardContent className="px-3">
            <span className="block text-xs font-bold uppercase tracking-wider text-muted-foreground">
              Model
            </span>
            <p className="mt-1 break-all text-xs leading-5 text-foreground/80">
              {telemetry?.model ?? "—"}
            </p>
          </CardContent>
        </Card>

        {(error || ttsError) && (
          <Card
            className="rounded-xl border-destructive/35 bg-destructive/5 py-3 text-destructive shadow-none"
            role="alert"
          >
            <CardContent className="px-3 text-xs leading-5">
              <strong className="block">Local service warning</strong>
              {error && <span className="mt-1 block break-words opacity-80">{error}</span>}
              {ttsError && (
                <span className="mt-1 block break-words opacity-80">
                  TTS: {ttsError}
                </span>
              )}
            </CardContent>
          </Card>
        )}

        <div className="mt-auto grid gap-3 pt-2">
          <Button type="button" size="lg" onClick={onReplay} disabled={!canReplay}>
            <Volume2Icon />
            Replay voice
          </Button>
          <Button type="button" size="lg" onClick={onOpenLog}>
            <MessageSquareTextIcon />
            Conversation log
          </Button>
          <Button type="button" size="lg" onClick={onReset}>
            <RotateCcwIcon />
            Reset conversation
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function TelemetryItem({
  icon,
  label,
  value,
  detail,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <Card className="gap-2 rounded-2xl bg-muted py-3 shadow-none">
      <CardContent className="px-3">
        <div className="flex items-center gap-2 text-muted-foreground">
          {icon}
          <span className="truncate text-xs font-bold uppercase tracking-wider">
            {label}
          </span>
        </div>
        <strong className="mt-1.5 block truncate text-base font-semibold capitalize">
          {value}
        </strong>
        <small className="mt-0.5 block truncate text-xs text-muted-foreground">
          {detail}
        </small>
      </CardContent>
    </Card>
  );
}

function formatVoiceState(state: SpeechPlaybackState): string {
  return state === "unsupported" ? "Unsupported language" : state;
}

function formatOperationalState(state: OperationalState): string {
  return state === "unsupported" ? "Unsupported language" : state;
}

function formatDuration(durationMs: number | null | undefined): string {
  return durationMs == null ? "—" : `${(durationMs / 1000).toFixed(2)} s`;
}

function stateBadgeVariant(
  state: OperationalState,
): "secondary" | "destructive" | "outline" {
  switch (state) {
    case "thinking":
    case "synthesizing":
    case "speaking":
      return "secondary";
    case "unsupported":
      return "outline";
    case "error":
      return "destructive";
    default:
      return "outline";
  }
}
