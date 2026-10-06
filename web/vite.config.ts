import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import solid from "vite-plugin-solid";

// The frontend only ever calls relative /api paths; in dev Vite forwards them to the separately running ASP.NET server.
const apiTarget = process.env.AFTERPELAGO_API_URL ?? "http://localhost:5041";

export default defineConfig({
  plugins: [solid(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": { target: apiTarget, xfwd: true },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "es2022",
  },
});
