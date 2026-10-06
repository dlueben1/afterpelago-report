import { defineConfig } from "orval";

// ORVAL_INPUT / ORVAL_OUT let `npm run api:check` regenerate into a scratch folder of the same depth.
const input = process.env.ORVAL_INPUT ?? "./openapi/afterpelago.json";
const out = process.env.ORVAL_OUT ?? "src/api/generated";

export default defineConfig({
  afterpelago: {
    input: { target: input },
    output: {
      mode: "single",
      target: `${out}/afterpelago.ts`,
      schemas: `${out}/model`,
      client: "solid-query",
      httpClient: "fetch",
      clean: true,
      override: {
        mutator: { path: "./src/api/transport.ts", name: "apiFetch" },
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
});
