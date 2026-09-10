import { Chart, ChartConfiguration, ChartType, registerables } from 'chart.js';
import { Component, DestroyRef, ElementRef, effect, inject, input, viewChild } from '@angular/core';

Chart.register(...registerables);

/**
 * Thin wrapper around Chart.js - this app has zero existing charting dependency, so a plain `chart.js` (not
 * `ng2-charts`, whose Angular peer-dependency range risks a conflict against this app's very new Angular
 * version) hand-wrapped in one generic component is simplest. Rebuilds the chart wholesale on any input
 * change rather than diffing - fine since each chart here is fetched once per page load, never live-updated.
 */
@Component({
  selector: 'app-chart-canvas',
  standalone: true,
  template: `
    <div [style.height.px]="height()">
      <canvas #canvasRef></canvas>
    </div>
  `,
})
export class ChartCanvas {
  type = input.required<ChartType>();
  data = input.required<ChartConfiguration['data']>();
  options = input<ChartConfiguration['options']>();
  height = input(280);

  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvasRef');
  private chart: Chart | undefined;

  constructor() {
    effect(() => {
      const canvas = this.canvasRef();
      const type = this.type();
      const data = this.data();
      const options = this.options();
      if (!canvas) return;

      this.chart?.destroy();
      this.chart = new Chart(canvas.nativeElement, { type, data, options: options ?? {} });
    });

    inject(DestroyRef).onDestroy(() => this.chart?.destroy());
  }
}
