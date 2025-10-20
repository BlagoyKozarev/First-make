import { logObservation, OperationObservation } from './api';

/**
 * Utility for tracking operations with automatic timing and error handling
 */
export class OperationTracker {
  private sourceFileName?: string;

  setSourceFile(fileName: string) {
    this.sourceFileName = fileName;
  }

  /**
   * Track an async operation with automatic timing and observation logging
   */
  async track<T>(
    operationType: string,
    operation: () => Promise<T>,
    metadata?: Record<string, unknown>
  ): Promise<T> {
    const startTime = performance.now();
    let success = false;
    let errorMessage: string | undefined;
    let result: T;

    try {
      result = await operation();
      success = true;
      return result;
    } catch (error) {
      errorMessage = error instanceof Error ? error.message : String(error);
      throw error;
    } finally {
      const durationMs = Math.round(performance.now() - startTime);

      // Log observation (fire-and-forget, don't block on errors)
      const observation: OperationObservation = {
        operationType,
        success,
        durationMs,
        sourceFileName: this.sourceFileName,
        errorMessage,
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        metadata: metadata as Record<string, any>,
      };

      logObservation(observation).catch((err) => {
        console.warn('Failed to log observation:', err);
      });
    }
  }

  /**
   * Compute hash of input for duplicate detection (simple client-side hash)
   */
  computeHash(input: unknown): string {
    const json = JSON.stringify(input);
    // Simple hash - in production, use crypto.subtle.digest
    let hash = 0;
    for (let i = 0; i < json.length; i++) {
      const char = json.charCodeAt(i);
      hash = (hash << 5) - hash + char;
      hash = hash & hash; // Convert to 32bit integer
    }
    return hash.toString(36);
  }
}

// Global tracker instance
export const tracker = new OperationTracker();
