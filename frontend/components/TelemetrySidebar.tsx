// This file renders the right-side control rail for avatar state, Qwen telemetry,
// local CosyVoice speech status, replay, conversation log, and reset actions.
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

const DEFAULT_MODEL = "qwen3:4b-instruct-2507-q4_K_M";

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

  const visibleEmotion = isThinking
    ? "thinking"
    : decision.emotion;

  const visibleGesture = isThinking
    ? "local"
    : decision.gesture;

  return (
    <Card
      className="
        min-h-0
        min-w-0
        w-full
        max-w-full
        gap-4
        overflow-hidden
        rounded-3xl
        border-border
        bg-background
        p-[var(--avatar-sidebar-pad)]
        shadow-sm
        [height:var(--avatar-sidebar-height)]
      "
    >
      <CardHeader
        className="
          min-w-0
          gap-1
          border-b
          border-border
          p-0
          pb-[var(--avatar-sidebar-header-pad-bottom)]
        "
      >
        <CardDescription
          className="
            flex
            min-w-0
            items-center
            gap-2
            text-[0.68rem]
            font-bold
            uppercase
            tracking-[0.16em]
            text-primary
          "
        >
          <SparklesIcon className="size-3.5 shrink-0" />
          <span className="min-w-0 truncate">
            Local Avatar
          </span>
        </CardDescription>

        <CardTitle
          className="
            min-w-0
            truncate
            text-[var(--avatar-sidebar-title-size)]
          "
        >
          Control Panel
        </CardTitle>

        <div className="flex min-w-0 flex-wrap items-center gap-2 pt-1">
          <Badge variant="secondary">
            LOCAL
          </Badge>

          <Badge
            variant={stateBadgeVariant(operationalState)}
            className="capitalize"
          >
            {operationalState}
          </Badge>
        </div>
      </CardHeader>

      <CardContent
        className="
          flex
          min-h-0
          min-w-0
          w-full
          max-w-full
          flex-1
          flex-col
          gap-[var(--avatar-control-gap)]
          p-0
          pt-[var(--avatar-sidebar-content-pad-top)]
        "
      >
        <div
          className="
            grid
            min-w-0
            w-full
            max-w-full
            gap-[var(--avatar-control-gap)]
            [grid-template-columns:var(--avatar-telemetry-columns)]
          "
        >
          <TelemetryItem
            icon={<BrainCircuitIcon className="size-4" />}
            label="Expression"
            value={visibleEmotion}
            detail={`${Math.round(
              (isThinking
                ? 0.35
                : decision.emotionIntensity) * 100,
            )}% intensity`}
          />

          <TelemetryItem
            icon={<ActivityIcon className="size-4" />}
            label="Gesture"
            value={visibleGesture}
            detail={`${Math.round(
              (isThinking
                ? 0
                : decision.gestureIntensity) * 100,
            )}% intensity`}
          />

          <TelemetryItem
            icon={<CpuIcon className="size-4" />}
            label="Inference"
            value={
              telemetry?.totalDurationMs != null
                ? `${(
                    telemetry.totalDurationMs / 1000
                  ).toFixed(2)} s`
                : "—"
            }
            detail={
              telemetry?.loadDurationMs != null
                ? `load ${(
                    telemetry.loadDurationMs / 1000
                  ).toFixed(2)} s`
                : "local Ollama"
            }
          />

          <TelemetryItem
            icon={<AudioLinesIcon className="size-4" />}
            label="Voice"
            value={ttsState}
            detail="local CosyVoice3"
          />

          <TelemetryItem
            icon={<MessageSquareTextIcon className="size-4" />}
            label="Tokens"
            value={
              telemetry
                ? `${telemetry.promptTokens ?? "?"} → ${
                    telemetry.outputTokens ?? "?"
                  }`
                : "—"
            }
            detail="prompt → output"
          />
        </div>

        <Card
          className="
            min-w-0
            w-full
            max-w-full
            gap-2
            overflow-hidden
            rounded-2xl
            border-border
            bg-muted
            py-3
            shadow-none
          "
        >
          <CardContent className="min-w-0 px-3">
            <span
              className="
                block
                text-[0.62rem]
                font-bold
                uppercase
                tracking-[0.12em]
                text-muted-foreground
              "
            >
              Model
            </span>

            <p
              className="
                mt-1
                min-w-0
                break-all
                text-xs
                leading-5
                text-foreground/80
              "
            >
              {telemetry?.model ?? DEFAULT_MODEL}
            </p>
          </CardContent>
        </Card>

        {(error || ttsError) && (
          <Card
            className="
              min-w-0
              w-full
              max-w-full
              overflow-hidden
              rounded-xl
              border-destructive/35
              bg-destructive/5
              py-3
              text-destructive
              shadow-none
            "
            role="alert"
          >
            <CardContent
              className="
                min-w-0
                px-3
                text-xs
                leading-5
              "
            >
              <strong className="block">
                Local service warning
              </strong>

              {error && (
                <span
                  className="
                    mt-1
                    block
                    min-w-0
                    break-words
                    opacity-80
                  "
                >
                  {error}
                </span>
              )}

              {ttsError && (
                <span
                  className="
                    mt-1
                    block
                    min-w-0
                    break-words
                    opacity-80
                  "
                >
                  TTS: {ttsError}
                </span>
              )}
            </CardContent>
          </Card>
        )}

        <div
          className="
            mt-auto
            grid
            min-w-0
            w-full
            max-w-full
            grid-cols-[minmax(0,1fr)]
            gap-3
            pt-4
            pb-[0.1rem]
          "
        >
          <Button
            type="button"
            size="lg"
            className="
              min-w-0
              w-full
              max-w-full
            "
            onClick={onReplay}
            disabled={!canReplay}
          >
            <Volume2Icon className="shrink-0" />
            <span className="min-w-0 truncate">
              Replay voice
            </span>
          </Button>

          <Button
            type="button"
            size="lg"
            className="
              min-w-0
              w-full
              max-w-full
            "
            onClick={onOpenLog}
          >
            <MessageSquareTextIcon className="shrink-0" />
            <span className="min-w-0 truncate">
              Conversation log
            </span>
          </Button>

          <Button
            type="button"
            size="lg"
            className="
              min-w-0
              w-full
              max-w-full
            "
            onClick={onReset}
          >
            <RotateCcwIcon className="shrink-0" />
            <span className="min-w-0 truncate">
              Reset conversation
            </span>
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
    <Card
      className="
        min-w-0
        w-full
        max-w-full
        gap-2
        overflow-hidden
        rounded-2xl
        border-border
        bg-muted
        py-3
        shadow-none
      "
    >
      <CardContent className="min-w-0 px-3">
        <div
          className="
            flex
            min-w-0
            items-center
            gap-2
            text-muted-foreground
          "
        >
          <span className="shrink-0">
            {icon}
          </span>

          <span
            className="
              min-w-0
              truncate
              text-[0.62rem]
              font-bold
              uppercase
              tracking-[0.12em]
            "
          >
            {label}
          </span>
        </div>

        <strong
          className="
            mt-1.5
            block
            min-w-0
            truncate
            text-[var(--avatar-telemetry-value-size)]
            font-semibold
            capitalize
            text-foreground
          "
        >
          {value}
        </strong>

        <small
          className="
            mt-0.5
            block
            min-w-0
            truncate
            text-[0.68rem]
            text-muted-foreground
          "
        >
          {detail}
        </small>
      </CardContent>
    </Card>
  );
}

function stateBadgeVariant(
  state: OperationalState,
): "secondary" | "destructive" | "outline" {
  switch (state) {
    case "thinking":
    case "synthesizing":
    case "speaking":
      return "secondary";

    case "error":
      return "destructive";

    default:
      return "outline";
  }
}