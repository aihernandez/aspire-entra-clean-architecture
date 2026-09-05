import type { ProblemDetails } from '../api-client/models';

/**
 * Every Users endpoint declares its failure responses in the OpenAPI document (see
 * Web.Api/Extensions/EndpointExtensions.cs's ProducesProblemResponses), so Kiota generates real
 * `errorMappings` and throws a deserialized ProblemDetails object on failure instead of a bare,
 * bodiless exception. This pulls the backend's actual message out of that object, falling back
 * to a caller-supplied default for anything unexpected (network failure, a status code with no
 * mapping, ...).
 */
export function extractErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object') {
    const problem = error as Partial<ProblemDetails>;

    // FluentValidation failures land in the (undeclared, so Kiota parks it in additionalData)
    // "errors" array with a generic `detail` — prefer the specific per-field messages when present.
    const validationMessage = extractValidationMessage(problem.additionalData);

    if (validationMessage) {
      return validationMessage;
    }

    if (typeof problem.detail === 'string' && problem.detail.length > 0) {
      return problem.detail;
    }

    if (typeof problem.title === 'string' && problem.title.length > 0) {
      return problem.title;
    }
  }

  return fallback;
}

function extractValidationMessage(additionalData: Record<string, unknown> | undefined): string | null {
  const errors = additionalData?.['errors'];

  if (!Array.isArray(errors) || errors.length === 0) {
    return null;
  }

  const descriptions = errors
    .map(e => (e && typeof e === 'object' ? (e as { description?: unknown }).description : null))
    .filter((d): d is string => typeof d === 'string' && d.length > 0);

  return descriptions.length > 0 ? descriptions.join(' ') : null;
}
