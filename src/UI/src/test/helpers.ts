/**
 * Common test helpers and utilities for FirstMake UI tests
 */

import { vi } from 'vitest';

/**
 * Default test project ID used across test suite
 * Use this constant for consistency
 */
export const TEST_PROJECT_ID = 'test-project-123';

/**
 * Creates a mock navigation function for react-router-dom
 * @returns Mock navigate function that can be used in tests
 * 
 * @example
 * const mockNavigate = createMockNavigate();
 * vi.mock('react-router-dom', async () => ({
 *   ...await vi.importActual('react-router-dom'),
 *   useNavigate: () => mockNavigate,
 * }));
 */
export const createMockNavigate = () => vi.fn();

/**
 * Sets up sessionStorage with a test projectId
 * Useful for tests that require a valid project context
 * 
 * @param projectId - The project ID to set (defaults to TEST_PROJECT_ID)
 * 
 * @example
 * beforeEach(() => {
 *   setupProjectSession();
 * });
 */
export const setupProjectSession = (projectId = TEST_PROJECT_ID) => {
  sessionStorage.setItem('currentProjectId', projectId);
};

/**
 * Cleans up test environment - clears all mocks and sessionStorage
 * Should be called in beforeEach/afterEach hooks
 * 
 * @example
 * beforeEach(() => {
 *   cleanupTestEnvironment();
 * });
 */
export const cleanupTestEnvironment = () => {
  vi.clearAllMocks();
  sessionStorage.clear();
};

/**
 * Waits for a confirmation dialog to appear and clicks the confirm button
 * Common pattern for override/delete operations
 * 
 * @param confirmButtonText - Text to find in the confirm button (defaults to /да, замени/i)
 * 
 * @example
 * import { confirmDialog } from '../../test/helpers';
 * 
 * await confirmDialog();
 * // or with custom button text
 * await confirmDialog(/yes, delete/i);
 */
export const confirmDialog = async (confirmButtonText: RegExp = /да, замени/i) => {
  const { screen, fireEvent, waitFor } = await import('./test-utils');
  const { expect } = await import('vitest');
  
  // Wait for dialog to appear
  await waitFor(() => {
    expect(screen.getByText(/Потвърждение/i)).toBeInTheDocument();
  });
  
  // Click confirm button
  const confirmButton = screen.getByRole('button', { name: confirmButtonText });
  fireEvent.click(confirmButton);
};

/**
 * Finds a button by className pattern (useful for styled buttons without accessible names)
 * 
 * @param container - The container element to search within
 * @param classNamePattern - The className pattern to match (e.g., 'bg-blue-600')
 * @returns The button element or null if not found
 * 
 * @example
 * const uploadButton = findButtonByClassName(container, 'bg-blue-600');
 */
export const findButtonByClassName = (
  container: HTMLElement,
  classNamePattern: string
): HTMLButtonElement | null => {
  const buttons = container.querySelectorAll('button');
  return Array.from(buttons).find((btn) =>
    btn.className.includes(classNamePattern)
  ) as HTMLButtonElement | null;
};
