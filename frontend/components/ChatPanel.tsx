// This component composes the avatar stage, chat composer, telemetry sidebar, and conversation log for one local chat session.
"use client";

import { AvatarStage } from "@/components/AvatarStage";
import { ChatComposer } from "@/components/ChatComposer";
import { ChatLogModal } from "@/components/ChatLogModal";
import { TelemetrySidebar } from "@/components/TelemetrySidebar";
import { useChatSession } from "@/hooks/useChatSession";

export function ChatPanel() {
  const chat = useChatSession();

  return (
    <main className="min-h-screen bg-muted p-4 text-foreground">
      <div className="mx-auto grid min-h-[calc(100vh-2rem)] max-w-[1600px] grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="grid min-h-0 grid-rows-[minmax(0,1fr)_auto] gap-4">
          <AvatarStage
            emotion={chat.decision.emotion}
            emotionIntensity={chat.decision.emotionIntensity}
            gesture={chat.decision.gesture}
            gestureIntensity={chat.decision.gestureIntensity}
            operationalState={chat.operationalState}
            animationKey={chat.animationKey}
            reply={chat.visibleReply}
          />

          <ChatComposer
            value={chat.input}
            canSend={chat.canSend}
            isThinking={chat.operationalState === "thinking"}
            isSpeechActive={chat.speechState !== "idle"}
            onChange={chat.setInput}
            onSend={() => void chat.sendMessage()}
          />
        </section>

        <TelemetrySidebar
          decision={chat.decision}
          telemetry={chat.telemetry}
          operationalState={chat.operationalState}
          ttsState={chat.speechState}
          error={chat.error}
          ttsError={chat.ttsError}
          canReplay={chat.canReplay}
          onReplay={() => void chat.replayVoice()}
          onOpenLog={() => chat.setIsLogOpen(true)}
          onReset={chat.resetConversation}
        />
      </div>

      <ChatLogModal
        open={chat.isLogOpen}
        messages={chat.messages}
        operationalState={chat.operationalState}
        onClose={() => chat.setIsLogOpen(false)}
      />
    </main>
  );
}
