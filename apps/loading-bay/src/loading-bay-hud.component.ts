import {
  type AfterViewInit,
  type ElementRef,
  ViewChild,
  ChangeDetectionStrategy,
  Component,
  type OnDestroy,
  inject,
  signal,
} from "@angular/core";
import {
  ENGINE_APPLICATION,
  type LoadingBayHudProjectionEnvelope,
} from "./engine-application";

import { mountLoadingBayDebug } from "./loading-bay-debug";

interface LoadingBayHudSnapshot {
  readonly content: string;
  readonly health: number;
  readonly armor: number;
  readonly bullets: number;
  readonly shells: number;
  readonly weapon: string;
  readonly kills: number;
  readonly totalEnemies: number;
  readonly collected: number;
  readonly totalPickups: number;
  readonly dead: boolean;
  readonly message: string;
  readonly weaponFlash: boolean;
  readonly damageFlash: boolean;
  readonly generation: number;
  readonly step: number;
  readonly complete: boolean;
  readonly facts: readonly LoadingBayHudFact[];
  readonly droppedFacts: number;
  readonly pendingSchedules: number;
  readonly exitVisibility: boolean;
  readonly exitVisibilityRevision: number;
  readonly presentationBillboards: number;
  readonly animationCue: string;
  readonly effectsMuted: boolean;
  readonly effectsVolume: number;
  readonly updateMode: string;
  readonly lifecycle: string;
  readonly admittedSteps: number;
  readonly droppedSteps: number;
  readonly catalogHash: string;
  readonly materialCount: number;
  readonly materialMappingCount: number;
  readonly voxelPresentationRealized: boolean;
  readonly skyPath: string;
  readonly skyHash: string;
  readonly skyResourceRealized: boolean;
  readonly skyBackgroundSelected: boolean;
}

interface LoadingBayHudFact {
  readonly kind: string;
}

const EMPTY_READOUT: LoadingBayHudSnapshot = {
  content: "doom-e1m1",
  health: 0,
  armor: 0,
  bullets: 0,
  shells: 0,
  weapon: "",
  kills: 0,
  totalEnemies: 0,
  collected: 0,
  totalPickups: 0,
  dead: false,
  message: "",
  weaponFlash: false,
  damageFlash: false,
  generation: 0,
  step: 0,
  complete: false,
  facts: [],
  droppedFacts: 0,
  pendingSchedules: 0,
  exitVisibility: false,
  exitVisibilityRevision: 0,
  presentationBillboards: 0,
  animationCue: "",
  effectsMuted: false,
  effectsVolume: 0,
  updateMode: "",
  lifecycle: "",
  admittedSteps: 0,
  droppedSteps: 0,
  catalogHash: "",
  materialCount: 0,
  materialMappingCount: 0,
  voxelPresentationRealized: false,
  skyPath: "",
  skyHash: "",
  skyResourceRealized: false,
  skyBackgroundSelected: false,
};

/** A disposable DOM readout over the immutable C# Engine UI projection. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: "red-loading-bay-hud",
  standalone: true,
  styles: [
    `
      :host {
        display: block;
        inset: 0;
        pointer-events: none;
        position: fixed;
        z-index: 2;
      }
      .hud {
        color: #f5e8d2;
        font:
          700 12px/1.35 ui-monospace,
          SFMono-Regular,
          Consolas,
          monospace;
        inset: 0;
        position: absolute;
        text-shadow: 2px 2px 0 #090504;
      }
      .readout {
        background: #160d0bcc;
        border: 1px solid #947e62;
        display: grid;
        gap: 4px;
        letter-spacing: 0.06em;
        padding: 10px 12px;
        position: absolute;
        text-transform: uppercase;
      }
      .debug {
        display: block;
        left: 20px;
        top: 18px;
        max-width: calc(100vw - 40px);
        max-height: calc(100vh - 110px);
        overflow: auto;
        pointer-events: auto;
        text-transform: none;
        letter-spacing: normal;
      }
      .debug[open] {
        width: min(42rem, calc(100vw - 66px));
      }
      summary {
        cursor: pointer;
        font-weight: 700;
        padding: 2px;
      }
      summary:focus-visible {
        outline: 2px solid #f4bd58;
        outline-offset: 3px;
      }
      .identity {
        display: grid;
        gap: 4px;
        margin-top: 12px;
      }
      .debug-widgets {
        margin-top: 12px;
        text-shadow: none;
      }
      .status {
        color: #d6c6ad;
        margin-top: 12px;
        overflow-wrap: anywhere;
      }
      .complete {
        color: #f4bd58;
      }
      .fault {
        color: #ffb4a9;
      }
      .crosshair {
        height: 27px;
        left: 50%;
        position: absolute;
        top: 50%;
        transform: translate(-50%, -50%);
        width: 27px;
      }
      .crosshair::before,
      .crosshair::after {
        background: #f4e6cfcc;
        box-shadow: 1px 1px 0 #090504;
        content: "";
        position: absolute;
      }
      .crosshair::before {
        height: 1px;
        left: 0;
        top: 13px;
        width: 27px;
      }
      .crosshair::after {
        height: 27px;
        left: 13px;
        top: 0;
        width: 1px;
      }
      .crosshair-mark {
        border: 1px solid #f4e6cf;
        border-radius: 50%;
        height: 4px;
        left: 10px;
        position: absolute;
        top: 10px;
        width: 4px;
      }
      .damage-flash {
        background: radial-gradient(circle, #9f171500 35%, #bb1e19a6 100%);
        inset: 0;
        opacity: 0;
        position: absolute;
        transition: opacity 90ms linear;
      }
      .damage-flash.active {
        opacity: 1;
      }
      .doom-status {
        align-items: stretch;
        background:
          linear-gradient(135deg, #6e5a43 25%, transparent 25%) 0 0 / 8px 8px,
          linear-gradient(315deg, #4b372c 25%, #30201d 25%) 0 0 / 8px 8px,
          #30201d;
        border-top: 3px solid #aa9678;
        bottom: 0;
        box-shadow:
          0 -2px 0 #1a0f0d,
          inset 0 2px #d0baa0,
          inset 0 -2px #170d0c;
        display: grid;
        grid-template-columns:
          minmax(84px, 1fr) minmax(92px, 1fr) minmax(120px, 1.25fr)
          minmax(92px, 1fr) minmax(84px, 1fr);
        left: 0;
        height: 88px;
        box-sizing: border-box;
        position: absolute;
        right: 0;
      }
      .status-cell {
        align-items: center;
        border-left: 2px solid #160d0b;
        border-right: 1px solid #9c866d;
        display: flex;
        flex-direction: column;
        justify-content: center;
        min-width: 0;
        padding: 8px;
      }
      .status-cell:first-child {
        border-left: 0;
      }
      .status-label {
        color: #d6c6ad;
        font-size: 10px;
        letter-spacing: 0.13em;
      }
      .status-value {
        color: #f0d4a2;
        font-size: clamp(24px, 3.1vw, 38px);
        letter-spacing: -0.08em;
        line-height: 1;
      }
      .ammo .status-value {
        color: #f7c75b;
      }
      .ammo-reserves {
        color: #d6c6ad;
        font-size: 9px;
        letter-spacing: 0.06em;
        line-height: 1.2;
      }
      .armor .status-value {
        color: #9ccfca;
      }
      .marine-cell {
        background: linear-gradient(#5c493a, #2b1d19);
        gap: 4px;
        position: relative;
      }
      .marine-face {
        background: #d4a675;
        border: 3px solid #1b100d;
        border-radius: 46% 46% 42% 42%;
        box-shadow:
          inset 0 0 0 2px #845837,
          0 2px 0 #e5c194;
        height: 43px;
        position: relative;
        width: 37px;
      }
      .marine-face::before {
        background: #1b100d;
        box-shadow: 16px 0 #1b100d;
        content: "";
        height: 5px;
        left: 8px;
        position: absolute;
        top: 14px;
        width: 5px;
      }
      .marine-face::after {
        background: #653820;
        bottom: 8px;
        content: "";
        height: 3px;
        left: 10px;
        position: absolute;
        width: 13px;
      }
      .marine-face.wounded {
        background: #bd7054;
      }
      .marine-face.critical,
      .marine-face.dead {
        background: #7a312d;
      }
      .marine-face.dead::after {
        background: #1b100d;
        height: 5px;
        width: 17px;
      }
      .weapon {
        align-items: stretch;
        display: grid;
        grid-template-columns: auto minmax(0, 1fr);
        gap: 7px;
        text-align: left;
        width: min(100%, 168px);
      }
      .weapon-mark {
        align-self: center;
        border: 2px solid #cfb28a;
        height: 17px;
        position: relative;
        width: 28px;
      }
      .weapon-mark::after {
        background: #cfb28a;
        content: "";
        height: 5px;
        position: absolute;
        right: -9px;
        top: 4px;
        width: 9px;
      }
      .weapon.flash .weapon-mark,
      .weapon.flash .weapon-name {
        color: #fff1be;
        filter: brightness(1.7);
      }
      .weapon-name {
        color: #f0d4a2;
        font-size: 12px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .progress {
        color: #e7d5bc;
        display: grid;
        font-size: 11px;
        gap: 3px;
        letter-spacing: 0.04em;
        text-align: center;
      }
      .status-message {
        background: #160d0be8;
        border: 1px solid #8f775d;
        bottom: 100px;
        color: #f4d29a;
        left: 50%;
        max-width: min(80vw, 580px);
        overflow: hidden;
        padding: 5px 10px;
        position: absolute;
        text-align: center;
        text-overflow: ellipsis;
        transform: translateX(-50%);
        white-space: nowrap;
      }
      .status-message.dead {
        color: #ffaaa0;
      }
      .controls-hint {
        bottom: 100px;
        color: #f0dfc4;
        font-size: 10px;
        left: 16px;
        letter-spacing: 0.08em;
        position: absolute;
      }
      @media (max-width: 620px) {
        .doom-status {
          grid-template-columns: 1fr 1fr 1.15fr 1fr;
        }
        .weapon-cell {
          display: none;
        }
        .status-cell {
          padding: 6px 4px;
        }
        .controls-hint {
          bottom: 96px;
          font-size: 8px;
        }
      }
    `,
  ],
  template: `
    <section class="hud" aria-label="Loading Bay game readout">
      <div
        class="damage-flash"
        [class.active]="snapshot().damageFlash"
        aria-hidden="true"
      ></div>
      <div class="crosshair" aria-hidden="true">
        <span class="crosshair-mark"></span>
      </div>
      <details
        #debugBlock
        class="readout debug"
        data-rusty-ui-interactive
        (toggle)="toggleDebug($event)"
      >
        <summary>Debug · F3</summary>
        <div class="identity">
          <span>{{
            snapshot().content === "doom-room-study"
              ? "DOOM E1M1 / RECIPE GAMEPLAY"
              : "DOOM E1M1 / HANGAR"
          }}</span>
          <strong [class.complete]="snapshot().complete">
            {{ snapshot().complete ? "EXIT SECURED" : "ENGINE RUNTIME ACTIVE" }}
          </strong>
          <span
            >{{ snapshot().updateMode }} · {{ snapshot().lifecycle }} · GEN
            {{ snapshot().generation }} · STEP {{ snapshot().step }}</span
          >
        </div>

        <div class="status">
          @if (projectionFault() !== null) {
            <span class="fault"
              >HUD projection unavailable: {{ projectionFault() }}</span
            >
          } @else if (ready()) {
            <span>
              FACTS {{ snapshot().facts.length }} · DROPPED
              {{ snapshot().droppedFacts }} · SCHEDULES
              {{ snapshot().pendingSchedules }} · EXIT
              {{ snapshot().exitVisibility ? "VISIBLE" : "OCCLUDED" }} ({{
                snapshot().exitVisibilityRevision
              }}) · BILLBOARDS {{ snapshot().presentationBillboards }} · AUDIO
              {{ snapshot().effectsMuted ? "MUTED" : snapshot().effectsVolume }}
              · ADMITTED {{ snapshot().admittedSteps }} · DROPPED STEPS
              {{ snapshot().droppedSteps }}
              · MATERIALS {{ snapshot().materialCount }} / MAP
              {{ snapshot().materialMappingCount }} · SKY
              {{
                snapshot().skyResourceRealized &&
                snapshot().skyBackgroundSelected
                  ? snapshot().skyPath
                  : "UNREALIZED"
              }}
              @if (snapshot().animationCue) {
                · CUE {{ snapshot().animationCue }}
              }
            </span>
          } @else {
            <span>WAITING FOR ENGINE HUD PROJECTION…</span>
          }
        </div>
        <div
          #debugWidgets
          class="debug-widgets"
          aria-label="Engine diagnostics"
        ></div>
      </details>

      <div class="controls-hint">
        WASD MOVE · MOUSE LOOK · CLICK FIRE · E USE · R RESTART · 1 FIST · 2 PISTOL · 3 SHOTGUN
      </div>
      @if (statusText(); as text) {
        <div class="status-message" [class.dead]="snapshot().dead">
          {{ text }}
        </div>
      }
      <div class="doom-status" aria-label="Current player status">
        <div class="status-cell ammo">
          <span class="status-label">{{ ammoLabel() }}</span>
          <strong class="status-value">{{ currentAmmo() }}</strong>
          <span class="ammo-reserves"
            >B {{ snapshot().bullets }} · S {{ snapshot().shells }}</span
          >
        </div>
        <div class="status-cell armor">
          <span class="status-label">ARMOR</span>
          <strong class="status-value">{{ snapshot().armor }}%</strong>
        </div>
        <div class="status-cell marine-cell">
          <div
            class="marine-face"
            [class.wounded]="snapshot().health > 25 && snapshot().health <= 60"
            [class.critical]="snapshot().health > 0 && snapshot().health <= 25"
            [class.dead]="snapshot().dead"
            aria-hidden="true"
          ></div>
          <span class="status-label"
            >HEALTH <strong>{{ snapshot().health }}%</strong></span
          >
        </div>
        <div class="status-cell weapon-cell">
          <span class="status-label">WEAPON</span>
          <div class="weapon" [class.flash]="snapshot().weaponFlash">
            <span class="weapon-mark" aria-hidden="true"></span>
            <span class="weapon-name">{{ weaponLabel() }}</span>
          </div>
        </div>
        <div class="status-cell">
          <div class="progress">
            <span
              >KILLS {{ snapshot().kills }}/{{ snapshot().totalEnemies }}</span
            >
            <span
              >ITEMS {{ snapshot().collected }}/{{
                snapshot().totalPickups
              }}</span
            >
          </div>
        </div>
      </div>
    </section>
  `,
})
export class LoadingBayHudComponent implements AfterViewInit, OnDestroy {
  @ViewChild("debugBlock", { static: true })
  private debugBlock!: ElementRef<HTMLDetailsElement>;
  @ViewChild("debugWidgets", { static: true })
  private debugWidgets!: ElementRef<HTMLElement>;
  private debugMount: ReturnType<typeof mountLoadingBayDebug> | null = null;
  private readonly debugEvents = new AbortController();

  ngAfterViewInit(): void {
    const stop = (event: Event): void => event.stopPropagation();
    for (const type of [
      "pointerdown",
      "pointermove",
      "pointerup",
      "pointercancel",
      "mousedown",
      "mousemove",
      "mouseup",
      "wheel",
      "keydown",
      "keyup",
      "click",
    ]) {
      this.debugBlock.nativeElement.addEventListener(type, stop, {
        signal: this.debugEvents.signal,
      });
    }
    document.addEventListener("keydown", this.toggleDebugShortcut, {
      capture: true,
      signal: this.debugEvents.signal,
    });
  }

  protected toggleDebug(event: Event): void {
    this.setDebugOpen((event.target as HTMLDetailsElement).open);
  }

  protected readonly snapshot = signal<LoadingBayHudSnapshot>(EMPTY_READOUT);
  protected readonly ready = signal(false);
  protected readonly projectionFault = signal<string | null>(null);

  protected readonly weaponLabel = (): string => this.snapshot().weapon || "—";
  protected readonly ammoLabel = (): string =>
    this.isFist() ? "MELEE" : this.usesShells() ? "SHELLS" : "BULLETS";
  protected readonly currentAmmo = (): number | string =>
    this.isFist() ? "—" : this.usesShells() ? this.snapshot().shells : this.snapshot().bullets;
  protected readonly statusText = (): string => {
    const snapshot = this.snapshot();
    if (snapshot.message) return snapshot.message;
    if (snapshot.dead) return "YOU DIED · PRESS R TO RESTART";
    return snapshot.complete ? "EXIT SECURED" : "";
  };

  private readonly isFist = (): boolean => this.snapshot().weapon === "Fist";

  private readonly usesShells = (): boolean =>
    this.snapshot().weapon.trim().toLocaleLowerCase() === "shotgun";

  private readonly toggleDebugShortcut = (event: KeyboardEvent): void => {
    if (
      event.repeat ||
      (event.code !== "F3" && event.code !== "Backquote") ||
      (event.code === "Backquote" && isEditable(event.target))
    ) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    const nextOpen = !this.debugBlock.nativeElement.open;
    this.debugBlock.nativeElement.open = nextOpen;
    this.setDebugOpen(nextOpen);
  };

  private setDebugOpen(open: boolean): void {
    this.application.ui?.setInteractionMode(open ? "interface" : "gameplay");
    if (!open) this.application.ui?.focusGameplay();
    if (open) {
      this.debugMount ??= mountLoadingBayDebug(this.debugWidgets.nativeElement);
    } else {
      this.debugMount?.dispose();
      this.debugMount = null;
    }
  }

  private readonly application = inject(ENGINE_APPLICATION);
  private readonly unsubscribe = this.application.projection?.subscribe(
    (envelope) => {
      if (envelope === null) {
        this.ready.set(false);
        return;
      }
      const value = readHudSnapshot(envelope);
      if (value === null) {
        this.projectionFault.set("rejected malformed product value");
        return;
      }
      this.snapshot.set(value);
      this.projectionFault.set(null);
      this.ready.set(true);
    },
  );

  ngOnDestroy(): void {
    this.debugEvents.abort();
    this.debugMount?.dispose();
    this.unsubscribe?.();
  }
}

function readHudSnapshot(
  envelope: LoadingBayHudProjectionEnvelope,
): LoadingBayHudSnapshot | null {
  if (
    envelope.stream !== "loading-bay.hud" ||
    envelope.contract !== "loading-bay.hud.snapshot.v1"
  ) {
    return null;
  }
  const value = envelope.value;
  if (!isRecord(value)) return null;
  const health = finite(value.health);
  const armor = finite(value.armor);
  const bullets = finite(value.bullets);
  const shells = finite(value.shells);
  const kills = optionalFinite(value.kills, 0);
  const totalEnemies = optionalFinite(value.totalEnemies, 0);
  const collected = optionalFinite(value.collected, 0);
  const totalPickups = optionalFinite(value.totalPickups, 0);
  const weapon = optionalString(value.weapon, "");
  const dead = optionalBoolean(value.dead, false);
  const message = optionalString(value.message, "");
  const weaponFlash = optionalBoolean(value.weaponFlash, false);
  const damageFlash = optionalBoolean(value.damageFlash, false);
  const generation = finite(value.generation);
  const step = finite(value.step);
  const droppedFacts = finite(value.droppedFacts);
  const pendingSchedules = finite(value.pendingSchedules);
  const exitVisibilityRevision = finite(value.exitVisibilityRevision);
  const presentationBillboards = finite(value.presentationBillboards);
  const effectsVolume = finite(value.effectsVolume);
  const admittedSteps = finite(value.admittedSteps);
  const droppedSteps = finite(value.droppedSteps);
  const materialCount = finite(value.materialCount);
  const materialMappingCount = finite(value.materialMappingCount);
  if (
    health === null ||
    armor === null ||
    bullets === null ||
    shells === null ||
    kills === null ||
    totalEnemies === null ||
    collected === null ||
    totalPickups === null ||
    weapon === null ||
    dead === null ||
    message === null ||
    weaponFlash === null ||
    damageFlash === null ||
    generation === null ||
    step === null ||
    droppedFacts === null ||
    pendingSchedules === null ||
    exitVisibilityRevision === null ||
    presentationBillboards === null ||
    effectsVolume === null ||
    admittedSteps === null ||
    droppedSteps === null ||
    materialCount === null ||
    materialMappingCount === null ||
    typeof value.complete !== "boolean" ||
    typeof value.exitVisibility !== "boolean" ||
    typeof value.animationCue !== "string" ||
    typeof value.updateMode !== "string" ||
    typeof value.lifecycle !== "string" ||
    typeof value.effectsMuted !== "boolean" ||
    typeof value.catalogHash !== "string" ||
    typeof value.voxelPresentationRealized !== "boolean" ||
    typeof value.skyPath !== "string" ||
    typeof value.skyHash !== "string" ||
    typeof value.skyResourceRealized !== "boolean" ||
    typeof value.skyBackgroundSelected !== "boolean" ||
    !Array.isArray(value.facts) ||
    !value.facts.every(isHudFact)
  ) {
    return null;
  }
  return {
    content: typeof value.content === "string" ? value.content : "doom-e1m1",
    health,
    armor,
    bullets,
    shells,
    weapon,
    kills,
    totalEnemies,
    collected,
    totalPickups,
    dead,
    message,
    weaponFlash,
    damageFlash,
    generation,
    step,
    complete: value.complete,
    facts: value.facts,
    droppedFacts,
    pendingSchedules,
    exitVisibility: value.exitVisibility,
    exitVisibilityRevision,
    presentationBillboards,
    animationCue: value.animationCue,
    effectsMuted: value.effectsMuted,
    effectsVolume,
    updateMode: value.updateMode,
    lifecycle: value.lifecycle,
    admittedSteps,
    droppedSteps,
    catalogHash: value.catalogHash,
    materialCount,
    materialMappingCount,
    voxelPresentationRealized: value.voxelPresentationRealized,
    skyPath: value.skyPath,
    skyHash: value.skyHash,
    skyResourceRealized: value.skyResourceRealized,
    skyBackgroundSelected: value.skyBackgroundSelected,
  };
}

function isRecord(value: unknown): value is Readonly<Record<string, unknown>> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function finite(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function optionalFinite(value: unknown, fallback: number): number | null {
  return value === undefined ? fallback : finite(value);
}

function optionalString(value: unknown, fallback: string): string | null {
  if (value === undefined) return fallback;
  return typeof value === "string" ? value : null;
}

function optionalBoolean(value: unknown, fallback: boolean): boolean | null {
  if (value === undefined) return fallback;
  return typeof value === "boolean" ? value : null;
}

function isEditable(target: EventTarget | null): boolean {
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable ||
      target instanceof HTMLInputElement ||
      target instanceof HTMLTextAreaElement ||
      target instanceof HTMLSelectElement)
  );
}

function isHudFact(value: unknown): value is LoadingBayHudFact {
  return isRecord(value) && typeof value.kind === "string";
}
