import { BarChart, LineChart } from "echarts/charts";
import {
  BrushComponent,
  DataZoomComponent,
  DataZoomInsideComponent,
  GridComponent,
  ToolboxComponent,
  TooltipComponent,
} from "echarts/components";
import * as echarts from "echarts/core";
import type { EChartsCoreOption } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import {
  createEffect,
  createSignal,
  onCleanup,
  onMount,
  untrack,
} from "solid-js";

echarts.use([
  BarChart,
  LineChart,
  BrushComponent,
  DataZoomComponent,
  DataZoomInsideComponent,
  GridComponent,
  ToolboxComponent,
  TooltipComponent,
  CanvasRenderer,
]);

interface EChartProps {
  option: EChartsCoreOption;
  class?: string;
  label: string;
  /** ECharts event handlers, e.g. { click: (params) => ... }. Bound on mount. */
  onEvents?: Record<string, (params: unknown) => void>;
  onUpdate?: (chart: echarts.ECharts) => void;
}

/** Thin ECharts host: canvas renderer, reactive option updates, resize tracking and disposal. */
export function EChart(props: EChartProps) {
  let host!: HTMLDivElement;
  const [chart, setChart] = createSignal<echarts.ECharts>();

  onMount(() => {
    const instance = echarts.init(host, undefined, { renderer: "canvas" });
    if (props.onEvents) {
      for (const [event, handler] of Object.entries(props.onEvents)) {
        instance.on(event, handler);
      }
    }
    setChart(instance);
    const observer = new ResizeObserver(() => instance.resize());
    observer.observe(host);

    onCleanup(() => {
      observer.disconnect();
      instance.dispose();
      setChart(undefined);
    });
  });

  createEffect(() => {
    const instance = chart();
    const option = props.option;
    if (!instance) return;
    instance.setOption(option, { notMerge: true });
    untrack(() => props.onUpdate?.(instance));
  });

  return (
    <div
      ref={host}
      class={props.class}
      role="img"
      aria-label={props.label}
      data-testid="echart"
    />
  );
}
