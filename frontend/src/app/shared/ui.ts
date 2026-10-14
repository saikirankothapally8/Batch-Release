import { Component, ElementRef, EventEmitter, Injectable, Input, Output, ViewChild, forwardRef, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { dmyToIso, isoToDmy } from '../core/dates';
import { LabelPipe } from './pipes';

/** Date field that is typed and displayed as DD-MM-YYYY, with a calendar picker; the model value is ISO (YYYY-MM-DD). */
@Component({
  selector: 'brq-date',
  imports: [FormsModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DateInputComponent), multi: true }],
  template: `
    <div class="date-input" [class.invalid]="invalid()">
      <input type="text" [id]="inputId" [placeholder]="'DD-MM-YYYY'" [value]="text()" [disabled]="disabled" inputmode="numeric"
             (input)="onText($any($event.target).value)" (blur)="onBlur()" [attr.aria-invalid]="invalid()" autocomplete="off" />
      <button type="button" class="icon-btn" (click)="openPicker()" [disabled]="disabled" aria-label="Open calendar">
        <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path fill="currentColor" d="M7 2v2H5a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2V2h-2v2H9V2H7zm-2 7h14v10H5V9z"/></svg>
      </button>
      <input #native type="date" class="native" tabindex="-1" aria-hidden="true" [value]="iso ?? ''" (change)="onNative($any($event.target).value)" />
    </div>`,
})
export class DateInputComponent implements ControlValueAccessor {
  @Input() inputId = '';
  @ViewChild('native') native!: ElementRef<HTMLInputElement>;
  iso: string | null = null;
  text = signal('');
  invalid = signal(false);
  disabled = false;
  private onChange: (v: string | null) => void = () => { };
  private onTouched: () => void = () => { };

  writeValue(v: string | null) { this.iso = v || null; this.text.set(isoToDmy(v)); this.invalid.set(false); }
  registerOnChange(fn: (v: string | null) => void) { this.onChange = fn; }
  registerOnTouched(fn: () => void) { this.onTouched = fn; }
  setDisabledState(d: boolean) { this.disabled = d; }

  onText(v: string) {
    // Auto-insert dashes while typing digits: 14102026 -> 14-10-2026
    const digits = v.replace(/\D/g, '');
    if (/^\d+$/.test(v) && digits.length === 8) v = `${digits.slice(0, 2)}-${digits.slice(2, 4)}-${digits.slice(4)}`;
    this.text.set(v);
    const iso = dmyToIso(v);
    this.invalid.set(!!v && !iso);
    if (iso || !v) { this.iso = iso; this.onChange(iso); }
  }
  onBlur() { this.onTouched(); if (this.iso) this.text.set(isoToDmy(this.iso)); }
  onNative(v: string) { this.writeValue(v); this.onChange(this.iso); }
  openPicker() {
    const el = this.native.nativeElement as HTMLInputElement & { showPicker?: () => void };
    try { el.showPicker ? el.showPicker() : el.click(); } catch { el.click(); }
  }
}

@Component({
  selector: 'brq-readiness',
  template: `
    <div class="readiness" [attr.aria-label]="value + '% ready'">
      <div class="readiness-track"><div class="readiness-fill" [style.width.%]="value" [class.full]="value >= 100"></div></div>
      <span class="readiness-value">{{ value | number: '1.0-0' }}%</span>
    </div>`,
  imports: [DecimalPipe],
})
export class ReadinessComponent { @Input() value = 0; }

const FLAG_TONE: Record<string, string> = {
  Overdue: 'critical', Expired: 'critical', ShelfLifeBreached: 'critical', AtRisk: 'serious', ExpiryNear: 'warning', Stuck: 'serious',
  Blocked: 'critical', OnHold: 'warning', OpenQualityEvents: 'warning', AtVendor: 'info', PendingChange: 'info'
};
const FLAG_ICON: Record<string, string> = { critical: '!', serious: '▲', warning: '◆', info: 'i' };

@Component({
  selector: 'brq-flags',
  imports: [LabelPipe],
  template: `@for (f of flags; track f) {<span class="chip tone-{{ tone(f) }}" [title]="f | label"><span class="chip-icon" aria-hidden="true">{{ icon(f) }}</span>{{ f | label }}</span>}`,
})
export class FlagsComponent {
  @Input() flags: string[] = [];
  tone(f: string) { return FLAG_TONE[f] ?? 'info'; }
  icon(f: string) { return FLAG_ICON[this.tone(f)]; }
}

const STATUS_TONE: Record<string, string> = {
  Completed: 'good', NotApplicable: 'neutral', InProgress: 'info', NotStarted: 'neutral', Blocked: 'critical',
  Released: 'good', ConditionallyReleased: 'good', PartiallyReleased: 'info', ReleasedForFurtherProcessing: 'info', InProgressBatch: 'info',
  OnHold: 'warning', Quarantine: 'warning', UnderInvestigation: 'serious', Rejected: 'critical', Recalled: 'critical',
  Open: 'warning', Closed: 'good', Approved: 'good', Pending: 'neutral', Submitted: 'info', NotRequired: 'neutral',
  Critical: 'critical', High: 'serious', Medium: 'warning', Low: 'neutral', Major: 'serious', Minor: 'neutral',
  AtVendor: 'warning', SentOut: 'warning', Returned: 'good', Planned: 'neutral'
};

@Component({
  selector: 'brq-status',
  imports: [LabelPipe],
  template: `<span class="status tone-{{ tone }}"><span class="dot" aria-hidden="true"></span>{{ label || (value | label) }}</span>`,
})
export class StatusComponent {
  @Input() value = '';
  @Input() label = '';
  get tone() { return STATUS_TONE[this.value] ?? 'neutral'; }
}

@Component({
  selector: 'brq-modal',
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="close.emit()"></div>
      <div class="modal" role="dialog" aria-modal="true" [attr.aria-label]="title" [class.wide]="wide">
        <header class="modal-head"><h2>{{ title }}</h2><button class="icon-btn" (click)="close.emit()" aria-label="Close">✕</button></header>
        <div class="modal-body"><ng-content /></div>
      </div>
    }`,
})
export class ModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() wide = false;
  @Output() close = new EventEmitter<void>();
}

/**
 * Electronic signature (21 CFR Part 11 §11.50/§11.200): shows who is signing and the meaning of the signature,
 * and requires the password to be re-entered for every signing.
 */
@Component({
  selector: 'brq-sign',
  imports: [FormsModule, ModalComponent],
  template: `
    <brq-modal [open]="open" [title]="title" (close)="cancel.emit()">
      <form (ngSubmit)="submit()" class="form">
        <ng-content />
        <div class="sign-manifest">
          <div><span class="muted">Signing as</span> <strong>{{ signer }}</strong></div>
          <div><span class="muted">Meaning</span> <strong>{{ meaning }}</strong></div>
        </div>
        @if (commentLabel) {
          <label class="field"><span>{{ commentLabel }}@if (commentRequired) {<em class="req">*</em>}</span>
            <textarea name="comment" [(ngModel)]="comment" rows="3" [required]="commentRequired"></textarea></label>
        }
        <label class="field"><span>Password<em class="req">*</em></span>
          <input type="password" name="password" [(ngModel)]="password" required autocomplete="current-password" /></label>
        @if (error) {<p class="error" role="alert">{{ error }}</p>}
        <p class="muted small">Your signature is legally binding, linked to this record and recorded in the audit trail.</p>
        <div class="actions">
          <button type="button" class="btn" (click)="cancel.emit()">Cancel</button>
          <button type="submit" class="btn primary" [disabled]="busy || !password || (commentRequired && !comment.trim())">{{ busy ? 'Signing…' : 'Sign' }}</button>
        </div>
      </form>
    </brq-modal>`,
})
export class SignatureComponent {
  @Input() open = false;
  @Input() title = 'Electronic signature';
  @Input() meaning = '';
  @Input() signer = '';
  @Input() commentLabel = '';
  @Input() commentRequired = false;
  @Input() busy = false;
  @Input() error = '';
  @Output() signed = new EventEmitter<{ password: string; comment: string }>();
  @Output() cancel = new EventEmitter<void>();
  password = '';
  comment = '';
  submit() { this.signed.emit({ password: this.password, comment: this.comment }); this.password = ''; }
}

export interface Toast { id: number; text: string; kind: 'ok' | 'error'; }

@Injectable({ providedIn: 'root' })
export class Toasts {
  readonly items = signal<Toast[]>([]);
  private n = 0;
  show(text: string, kind: 'ok' | 'error' = 'ok') {
    const t = { id: ++this.n, text, kind };
    this.items.update(x => [...x, t]);
    setTimeout(() => this.items.update(x => x.filter(i => i.id !== t.id)), kind === 'error' ? 7000 : 3500);
  }
}
