/* @refresh reload */
import { render } from "solid-js/web";
import App from "./App";
import { getAntiforgeryToken } from "./api/generated/afterpelago";
import { setCsrfTokenProvider } from "./api/transport";
import "@fontsource-variable/nunito-sans";
import "@fontsource/m-plus-rounded-1c/400.css";
import "@fontsource/m-plus-rounded-1c/700.css";
import "./styles.css";

setCsrfTokenProvider(async () => (await getAntiforgeryToken()).token);

render(() => <App />, document.getElementById("root")!);
