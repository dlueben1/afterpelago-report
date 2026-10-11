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
import { createEffect, onCleanup, onMount } from "solid-js";

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
}

/** Thin ECharts host: canvas renderer, reactive option updates, resize tracking and disposal. */
export function EChart(props: EChartProps) {
  let host!: HTMLDivElement;
  let chart: echarts.ECharts | undefined;

  onMount(() => {
    chart = echarts.init(host, undefined, { renderer: "canvas" });
    if (props.onEvents) {
      for (const [event, handler] of Object.entries(props.onEvents)) {
        chart.on(event, handler);
      }
    }
    const observer = new ResizeObserver(() => chart?.resize());
    observer.observe(host);

    onCleanup(() => {
      observer.disconnect();
      chart?.dispose();
      chart = undefined;
    });
  });

  createEffect(() => {
    const option = props.option;
    chart?.setOption(option, { notMerge: true });
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
