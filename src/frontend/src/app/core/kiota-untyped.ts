import type { UntypedNode } from '@microsoft/kiota-abstractions';

/**
 * Kiota's TypeScript generator (still in preview) falls back to `UntypedNode` instead of
 * `number` for schema properties with OpenAPI's `format: int32` — see PagedResponse<T>'s
 * PageNumber/PageSize/TotalCount/TotalPages. This unwraps the raw value.
 */
export function untypedNumber(node: UntypedNode | null | undefined, fallback = 0): number {
  const value = node?.value;
  return typeof value === 'number' ? value : fallback;
}
