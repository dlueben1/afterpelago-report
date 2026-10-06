/* @refresh reload */
import { render } from "solid-js/web";
import App from "./App";
import { getAntiforgeryToken } from "./api/generated/afterpelago";
import { setCsrfTokenProvider } from "./api/transport";
import "./styles.css";

setCsrfTokenProvider(async () => (await getAntiforgeryToken()).token);

render(() => <App />, document.getElementById("root")!);
