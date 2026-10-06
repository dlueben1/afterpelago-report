import { BarChart } from "echarts/charts";
import { GridComponent, TooltipComponent } from "echarts/components";
import * as echarts from "echarts/core";
import type { EChartsCoreOption } from "echarts/core";
import { CanvasRenderer } from "echarts/renderers";
import { createEffect, onCleanup, onMount } from "solid-js";

echarts.use([BarChart, GridComponent, TooltipComponent, CanvasRenderer]);

interface EChartProps {
  option: EChartsCoreOption;
  class?: string;
  label: string;
}

/** Thin ECharts host: canvas renderer, reactive option updates, resize tracking and disposal. */
export function EChart(props: EChartProps) {
  let host!: HTMLDivElement;
  let chart: echarts.ECharts | undefined;

  onMount(() => {
    chart = echarts.init(host, undefined, { renderer: "canvas" });
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
