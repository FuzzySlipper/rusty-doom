import {
  createLiveDebugHttpTransport,
  mountLiveDebugPanel,
  mountRendererMetricsWidget,
  type LiveDebugPanelMount,
} from "@rusty-engine/live-debug";

/** Mounts only Engine-owned diagnostic widgets into the product's DOM block. */
export function mountLoadingBayDebug(panel: HTMLElement): { dispose(): void } {
  const metricsToggle = button("Show metrics");
  metricsToggle.setAttribute("aria-pressed", "false");
  const status = document.createElement("p");
  status.hidden = true;
  status.setAttribute("role", "alert");
  panel.append(metricsToggle, status);
  const transport = createLiveDebugHttpTransport();
  const metricsHost = document.createElement("div");
  metricsHost.id = "loading-bay-renderer-metrics";
  metricsHost.setAttribute("aria-label", "Renderer performance metrics");
  metricsHost.style.cssText =
    "margin-top:.3rem;max-width:min(24rem,calc(100vw - 2rem));overflow:auto;";
  panel.append(metricsHost);
  const metrics = mountRendererMetricsWidget(metricsHost, {
    initiallyVisible: false,
    transport,
  });
  metricsToggle.setAttribute("aria-controls", metricsHost.id);
  const debugButton = button("Open live debug");
  debugButton.setAttribute("aria-expanded", "false");
  const debugHost = document.createElement("div");
  debugHost.id = "loading-bay-live-debug";
  debugHost.hidden = true;
  debugButton.setAttribute("aria-controls", debugHost.id);
  panel.append(debugButton, debugHost);
  let debugPanel: LiveDebugPanelMount | null = null;
  let disposed = false;
  let metricsVisible = false;
  metricsToggle.addEventListener("click", () => {
    if (disposed) return;
    const nextVisible = !metricsVisible;
    metricsToggle.disabled = true;
    status.hidden = true;
    void transport
      .execute(nextVisible ? "engine.renderer.show" : "engine.renderer.hide")
      .then((result) => {
        if (disposed) return;
        if (!result.succeeded) {
          status.textContent = message(
            new Error(result.message),
            "Renderer metrics command failed.",
          );
          status.hidden = false;
          return;
        }

        metricsVisible = nextVisible;
        metricsToggle.textContent = metricsVisible
          ? "Hide metrics"
          : "Show metrics";
        metricsToggle.setAttribute("aria-pressed", String(metricsVisible));
      })
      .catch((error: unknown) => {
        if (disposed) return;
        status.textContent = message(error, "Renderer metrics command failed.");
        status.hidden = false;
      })
      .finally(() => {
        if (!disposed) metricsToggle.disabled = false;
      });
  });
  debugButton.addEventListener("click", () => {
    if (disposed) return;
    if (debugPanel !== null) {
      debugPanel.dispose();
      debugPanel = null;
      debugHost.replaceChildren();
      debugHost.hidden = true;
      debugButton.textContent = "Open live debug";
      debugButton.setAttribute("aria-expanded", "false");
      return;
    }
    debugButton.disabled = true;
    status.hidden = true;
    debugHost.hidden = false;
    void mountLiveDebugPanel(debugHost, {
      enabled: true,
      presentation: "inline",
      transport,
    })
      .then((mounted) => {
        if (disposed) {
          mounted.dispose();
          return;
        }
        debugPanel = mounted;
        debugButton.disabled = false;
        debugButton.textContent = "Close live debug";
        debugButton.setAttribute("aria-expanded", "true");
      })
      .catch((error: unknown) => {
        if (disposed) return;
        debugButton.disabled = false;
        debugHost.replaceChildren();
        debugHost.hidden = true;
        status.textContent = message(
          error,
          "Live debug panel could not start.",
        );
        status.hidden = false;
      });
  });
  return {
    dispose: () => {
      disposed = true;
      debugPanel?.dispose();
      metrics.dispose();
      panel.replaceChildren();
    },
  };
}

function button(label: string): HTMLButtonElement {
  const element = document.createElement("button");
  element.type = "button";
  element.textContent = label;
  element.style.cssText =
    "background:#142821;border:1px solid #6f948a;border-radius:3px;color:#e8f3ef;cursor:pointer;font:inherit;margin:4px 8px 4px 0;padding:6px 10px;";
  return element;
}

function message(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}
