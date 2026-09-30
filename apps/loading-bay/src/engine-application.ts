import { InjectionToken } from "@angular/core";

import type { RustyApplicationUiContext, RuntimeUiProjectionEnvelope } from "@rusty-engine/product-ui";
export type LoadingBayEngineApplication = RustyApplicationUiContext;
export type LoadingBayHudProjectionEnvelope = RuntimeUiProjectionEnvelope;

export const ENGINE_APPLICATION = new InjectionToken<LoadingBayEngineApplication>(
  "ENGINE_APPLICATION",
);
