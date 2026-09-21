// This file renders the right-side control rail using shadcn/ui primitives plus local Tailwind layout/presentation utilities.
"use client";

import type { ReactNode } from "react";
import {
  ActivityIcon,
  BrainCircuitIcon,
  CpuIcon,
  MessageSquareTextIcon,
  RotateCcwIcon,
  SparklesIcon,
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
  error: string | null;
  onOpenLog: () => void;
  onReset: () => void;
}

export function TelemetrySidebar({
  decision,
  telemetry,
  operationalState,
  error,
  onOpenLog,
  onReset,
}: TelemetrySidebarProps) {
  const isThinking = operationalState === "thinking";
  const visibleEmotion = isThinking ? "thinking" : decision.emotion;
  const visibleGesture = isThinking ? "local" : decision.gesture;

  return (
    <Card className="min-h-0 gap-4 p-(--avatar-sidebar-pad) desktop:h-full">
      <CardHeader className="border-b border-border pb-[clamp(0.9rem,0.9vw,1.15rem)]">
        <CardDescription className="flex items-center gap-2 text-[0.68rem] font-bold uppercase tracking-[0.16em] text-primary">
          <SparklesIcon className="size-3.5" />
          Local Avatar
        </CardDescription>
        <CardTitle className="text-[clamp(1.15rem,0.5vw+0.9rem,1.45rem)]">
          Control Panel
        </CardTitle>
        <div className="flex flex-wrap items-center gap-2 pt-1">
          <Badge variant="secondary">LOCAL</Badge>
          <Badge
            variant={stateBadgeVariant(operationalState)}
            className="capitalize"
          >
            {operationalState}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="flex min-h-0 flex-1 flex-col gap-(--avatar-control-gap) pt-[clamp(0.9rem,0.9vw,1.1rem)]">
        <div className="grid gap-[clamp(0.5rem,0.5vw,0.75rem)] sm:max-desktop:grid-cols-2">
          <TelemetryItem
            icon={<BrainCircuitIcon className="size-4" />}
            label="Expression"
            value={visibleEmotion}
            detail={`${Math.round((isThinking ? 0.35 : decision.emotionIntensity) * 100)}% intensity`}
          />
          <TelemetryItem
            icon={<ActivityIcon className="size-4" />}
            label="Gesture"
            value={visibleGesture}
            detail={`${Math.round((isThinking ? 0 : decision.gestureIntensity) * 100)}% intensity`}
          />
          <TelemetryItem
            icon={<CpuIcon className="size-4" />}
            label="Inference"
            value={
              telemetry?.totalDurationMs != null
                ? `${(telemetry.totalDurationMs / 1000).toFixed(2)} s`
                : "—"
            }
            detail={
              telemetry?.loadDurationMs != null
                ? `load ${(telemetry.loadDurationMs / 1000).toFixed(2)} s`
                : "local Ollama"
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

        <Card size="sm" className="gap-2 bg-muted py-3 shadow-none">
          <CardContent className="px-3">
            <span className="block text-[0.62rem] font-bold uppercase tracking-[0.12em] text-muted-foreground">
              Model
            </span>
            <p className="mt-1 wrap-break-words text-xs leading-5 text-foreground/80">
              {telemetry?.model ?? DEFAULT_MODEL}
            </p>
          </CardContent>
        </Card>

        {error && (
          <Card
            size="sm"
            className="border-destructive/35 bg-destructive/5 py-3 text-destructive shadow-none"
            role="alert"
          >
            <CardContent className="px-3 text-xs leading-5">
              <strong className="block">Local model error</strong>
              <span className="mt-1 block wrap-break-words opacity-80">{error}</span>
            </CardContent>
          </Card>
        )}

        <div className="mt-auto grid gap-3 pt-4">
          <Button
            type="button"
            size="lg"
            className="min-h-13 w-full"
            onClick={onOpenLog}
          >
            <MessageSquareTextIcon data-icon="inline-start" />
            Conversation log
          </Button>
          <Button
            type="button"
            size="lg"
            className="min-h-13 w-full"
            onClick={onReset}
            disabled={isThinking}
          >
            <RotateCcwIcon data-icon="inline-start" />
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
    <Card size="sm" className="gap-2 bg-muted py-3 shadow-none">
      <CardContent className="px-3">
        <div className="flex items-center gap-2 text-muted-foreground">
          {icon}
          <span className="text-[0.62rem] font-bold uppercase tracking-[0.12em]">
            {label}
          </span>
        </div>
        <strong className="mt-1.5 block truncate text-[clamp(0.95rem,0.25vw+0.85rem,1.1rem)] font-semibold capitalize text-foreground">
          {value}
        </strong>
        <small className="mt-0.5 block truncate text-[0.68rem] text-muted-foreground">
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
      return "secondary";
    case "error":
      return "destructive";
    default:
      return "outline";
  }
}
