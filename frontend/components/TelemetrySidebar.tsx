// This file renders the right-side control rail; layout and presentation are centralized in globals.css while this component only describes telemetry structure and behavior.
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
    <Card className="avatar-sidebar-card">
      <CardHeader className="avatar-sidebar-header">
        <CardDescription className="avatar-sidebar-kicker">
          <SparklesIcon className="avatar-icon-xs" />
          Local Avatar
        </CardDescription>
        <CardTitle className="avatar-sidebar-title">Control Panel</CardTitle>
        <div className="avatar-sidebar-statuses">
          <Badge variant="secondary">LOCAL</Badge>
          <Badge
            variant={stateBadgeVariant(operationalState)}
            className="avatar-status-badge"
          >
            {operationalState}
          </Badge>
        </div>
      </CardHeader>

      <CardContent className="avatar-sidebar-content">
        <div className="avatar-telemetry-grid">
          <TelemetryItem
            icon={<BrainCircuitIcon className="avatar-icon-sm" />}
            label="Expression"
            value={visibleEmotion}
            detail={`${Math.round((isThinking ? 0.35 : decision.emotionIntensity) * 100)}% intensity`}
          />
          <TelemetryItem
            icon={<ActivityIcon className="avatar-icon-sm" />}
            label="Gesture"
            value={visibleGesture}
            detail={`${Math.round((isThinking ? 0 : decision.gestureIntensity) * 100)}% intensity`}
          />
          <TelemetryItem
            icon={<CpuIcon className="avatar-icon-sm" />}
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
            icon={<MessageSquareTextIcon className="avatar-icon-sm" />}
            label="Tokens"
            value={
              telemetry
                ? `${telemetry.promptTokens ?? "?"} → ${telemetry.outputTokens ?? "?"}`
                : "—"
            }
            detail="prompt → output"
          />
        </div>

        <Card size="sm" className="avatar-model-card">
          <CardContent className="avatar-compact-card-content">
            <span className="avatar-model-label">Model</span>
            <p className="avatar-model-value">
              {telemetry?.model ?? DEFAULT_MODEL}
            </p>
          </CardContent>
        </Card>

        {error && (
          <Card size="sm" className="avatar-error-card" role="alert">
            <CardContent className="avatar-error-content">
              <strong>Local model error</strong>
              <span>{error}</span>
            </CardContent>
          </Card>
        )}

        <div className="avatar-sidebar-actions">
          <Button
            type="button"
            size="lg"
            className="avatar-sidebar-action-button"
            onClick={onOpenLog}
          >
            <MessageSquareTextIcon data-icon="inline-start" />
            Conversation log
          </Button>
          <Button
            type="button"
            size="lg"
            className="avatar-sidebar-action-button"
            onClick={onReset}
            disabled={operationalState === "thinking"}
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
    <Card size="sm" className="avatar-telemetry-item">
      <CardContent className="avatar-compact-card-content">
        <div className="avatar-telemetry-label">
          {icon}
          <span>{label}</span>
        </div>
        <strong className="avatar-telemetry-value">{value}</strong>
        <small className="avatar-telemetry-detail">{detail}</small>
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
