// This file defines the root HTML layout, metadata, language hint, and the shared website-style Tailwind/shadcn global theme.
import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";

export const metadata: Metadata = {
  title: "Local Qwen AI Avatar",
  description:
    "Text-driven local Qwen 4B avatar MVP using Next.js, shadcn/ui, Tailwind CSS, ASP.NET Core 10, and Ollama",
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="zh-CN">
      <body>{children}</body>
    </html>
  );
}
