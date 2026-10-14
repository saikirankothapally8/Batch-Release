import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, ViewChild, signal } from '@angular/core';
import {
  BarController, BarElement, CategoryScale, Chart, ChartConfiguration, Legend, LineController, LineElement, LinearScale, PointElement, Tooltip
} from 'chart.js';

Chart.register(BarController, LineController, BarElement, LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend);

export interface Series {
  label: string;
  data: number[];
  /** CSS custom property holding the colour, e.g. --series-1, --status-critical, --ordinal-3 */
  color: string;
  /** Optional per-bar colours (CSS custom properties), e.g. for ordinal or status buckets */
  colors?: string[];
}

export interface ChartPick { index: number; series: number; }

/**
 * Thin Chart.js wrapper that applies the dashboard's mark specs (thin bars, 4px rounded data ends, 2px lines,
 * hairline grid, recessive axes), reads colours from CSS tokens so light/dark both work, makes every mark
 * clickable for drill-down, and offers a table view of the same numbers.
 */
@Component({
  selector: 'brq-chart',
  template: `
    <div class="chart-wrap">
      <div class="chart-tools">
        <button type="button" class="link small" (click)="table.set(!table())" [attr.aria-pressed]="table()">{{ table() ? 'Chart view' : 'Table view' }}</button>
      </div>
      <div class="chart-canvas" [style.height.px]="height" [hidden]="table()"><canvas #canvas [attr.aria-label]="ariaLabel" role="img"></canvas></div>
      @if (table()) {
        <div class="table-scroll" [style.max-height.px]="height + 24">
          <table class="data compact">
            <thead><tr><th>{{ categoryLabel }}</th>@for (s of series; track s.label) {<th class="num">{{ s.label }}</th>}</tr></thead>
            <tbody>
              @for (l of labels; track $index; let i = $index) {
                <tr><td>{{ l }}</td>@for (s of series; track s.label; let j = $index) {
                  <td class="num"><button type="button" class="link" (click)="pick.emit({ index: i, series: j })">{{ s.data[i] }}{{ unit }}</button></td>}</tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>`,
})
export class ChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() type: 'bar' | 'line' = 'bar';
  @Input() labels: string[] = [];
  @Input() series: Series[] = [];
  @Input() horizontal = false;
  @Input() stacked = false;
  @Input() height = 240;
  @Input() unit = '';
  @Input() categoryLabel = 'Category';
  @Input() ariaLabel = 'Chart';
  @Output() pick = new EventEmitter<ChartPick>();
  @ViewChild('canvas') canvas!: ElementRef<HTMLCanvasElement>;
  table = signal(false);
  private chart?: Chart;
  private media = window.matchMedia('(prefers-color-scheme: dark)');
  private onScheme = () => this.render();
  private themeObserver = new MutationObserver(() => this.render());

  ngAfterViewInit() {
    this.render();
    this.media.addEventListener('change', this.onScheme);
    this.themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  }
  private signature = '';
  /** Inputs are often fresh arrays on every change detection; only re-render when the content really changed. */
  ngOnChanges() {
    const sig = JSON.stringify([this.type, this.labels, this.series, this.horizontal, this.stacked, this.unit]);
    if (sig === this.signature) return;
    this.signature = sig;
    if (this.canvas) this.render();
  }
  ngOnDestroy() { this.chart?.destroy(); this.media.removeEventListener('change', this.onScheme); this.themeObserver.disconnect(); }

  private token(name: string) { return getComputedStyle(this.canvas.nativeElement).getPropertyValue(name).trim() || '#888'; }

  private render() {
    if (!this.canvas) return;
    this.chart?.destroy();
    const surface = this.token('--surface-1');
    const muted = this.token('--text-muted');
    const secondary = this.token('--text-secondary');
    const grid = this.token('--grid');
    const baseline = this.token('--baseline');
    const isLine = this.type === 'line';
    const multi = this.series.length > 1;

    const datasets = this.series.map(s => {
      const color = this.token(s.color);
      const bg = s.colors ? s.colors.map(c => this.token(c)) : color;
      return isLine
        ? { type: 'line' as const, label: s.label, data: s.data, borderColor: color, backgroundColor: color, borderWidth: 2, tension: 0.25,
            pointRadius: 4, pointHoverRadius: 6, pointBackgroundColor: color, pointBorderColor: surface, pointBorderWidth: 2 }
        : { type: 'bar' as const, label: s.label, data: s.data, backgroundColor: bg, hoverBackgroundColor: bg,
            borderColor: surface, borderWidth: this.stacked ? 1 : 0, borderRadius: this.stacked ? 2 : 4, borderSkipped: this.stacked ? false : 'start' as const,
            maxBarThickness: 28, categoryPercentage: multi && !this.stacked ? 0.7 : 0.75, barPercentage: multi && !this.stacked ? 0.92 : 0.9 };
    });

    const valueAxis = { beginAtZero: true, stacked: this.stacked, grid: { color: grid, drawTicks: false }, border: { display: false },
      ticks: { color: muted, precision: 0, padding: 6, font: { size: 11 } } };
    const catAxis = { stacked: this.stacked, grid: { display: false }, border: { color: baseline },
      ticks: { color: secondary, font: { size: 11 }, autoSkip: true, maxRotation: 0 } };

    const config: ChartConfiguration = {
      type: this.type,
      data: { labels: this.labels, datasets: datasets as any },
      options: {
        responsive: true, maintainAspectRatio: false, animation: { duration: 250 },
        indexAxis: this.horizontal ? 'y' : 'x',
        interaction: { mode: isLine || (multi && !this.stacked) ? 'index' : 'nearest', intersect: !isLine && !multi, axis: this.horizontal ? 'y' : 'x' },
        scales: this.horizontal ? { x: valueAxis, y: catAxis } as any : { x: catAxis, y: valueAxis } as any,
        plugins: {
          legend: { display: multi, position: 'bottom', align: 'start',
            labels: { color: secondary, boxWidth: 12, boxHeight: isLine ? 2 : 12, font: { size: 12 }, padding: 14 } },
          tooltip: { backgroundColor: this.token('--tooltip-bg'), titleColor: this.token('--tooltip-muted'), bodyColor: this.token('--tooltip-fg'),
            padding: 10, cornerRadius: 6, boxWidth: 10, boxHeight: 2, usePointStyle: false, displayColors: multi,
            callbacks: { label: c => `${c.formattedValue}${this.unit}  ${multi ? c.dataset.label : ''}`.trim(), footer: () => 'Click to see batches' },
            footerColor: this.token('--tooltip-muted'), footerFont: { weight: 'normal', size: 11 } }
        },
        onHover: (e, els) => { (e.native?.target as HTMLElement).style.cursor = els.length ? 'pointer' : 'default'; },
        onClick: (_e, els) => { if (els.length) this.pick.emit({ index: els[0].index, series: els[0].datasetIndex }); }
      }
    };
    this.chart = new Chart(this.canvas.nativeElement, config);
  }
}
