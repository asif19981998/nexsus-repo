import { Geist_Mono, Plus_Jakarta_Sans } from "next/font/google";
import { AppProviders } from "@/providers/AppProviders";
import "./globals.css";

const plusJakarta = Plus_Jakarta_Sans({
  variable: "--font-plus-jakarta",
  subsets: ["latin"],
  display: "swap",
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL || "http://localhost:3000"),
  title: {
    default: "Nexus — National Digital Matchmaking Hub",
    template: "%s | Nexus",
  },
  description:
    "Prototype of Bangladesh's national digital education, skills, and opportunity hub connecting students, teachers, faculty, researchers, institutions, companies, and UGC.",
  robots: { index: false, follow: false },
  icons: {
    icon: "/brand/nexus-mark.svg",
    apple: "/brand/nexus-mark.svg",
  },
  openGraph: {
    title: "Nexus — National Digital Matchmaking Hub",
    description: "Frontend-only prototype. All data is simulated.",
    type: "website",
    images: [{ url: "/brand/nexus-logo.svg", width: 220, height: 40, alt: "Nexus Matchmaking Hub" }],
  },
};

export default function RootLayout({ children }) {
  return (
    <html lang="en" className={`${plusJakarta.variable} ${geistMono.variable} h-full`} suppressHydrationWarning>
      {/* Browser extensions (e.g. ColorZilla's cz-shortcut-listen) add attributes to <body> before React loads. */}
      <body className={`${plusJakarta.className} flex min-h-full flex-col antialiased`} suppressHydrationWarning>
        <AppProviders>{children}</AppProviders>
      </body>
    </html>
  );
}
