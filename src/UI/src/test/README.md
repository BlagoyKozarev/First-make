# Test Utilities and Helpers

This directory contains shared testing utilities and configuration for the FirstMake UI test suite.

## Files

### `setup.ts`
Vitest global setup file that configures the testing environment:
- Initializes jsdom environment
- Configures React Testing Library
- Sets up global test utilities
- Mocks browser APIs (sessionStorage, localStorage, etc.)

### `test-utils.tsx`
Custom render function and re-exports from React Testing Library:
- Wraps components with necessary providers (Router, Context, etc.)
- Exports all RTL utilities for convenience
- Use this instead of importing directly from `@testing-library/react`

**Usage:**
```typescript
import { render, screen, fireEvent } from '../../test/test-utils';
```

### `helpers.ts`
Common test helper functions and utilities:

#### `setupProjectSession(projectId?)`
Sets up sessionStorage with a test projectId. Useful for tests requiring project context.

```typescript
beforeEach(() => {
  setupProjectSession(); // Uses 'test-project-123'
  // or
  setupProjectSession('custom-id');
});
```

#### `cleanupTestEnvironment()`
Clears all mocks and sessionStorage. Should be called in beforeEach/afterEach hooks.

```typescript
beforeEach(() => {
  cleanupTestEnvironment();
});
```

#### `confirmDialog(confirmButtonText?)`
Waits for a confirmation dialog and clicks the confirm button. Common pattern for override/delete operations.

```typescript
// Click default confirm button
await confirmDialog();

// Or with custom button text
await confirmDialog(/yes, delete/i);
```

#### `findButtonByClassName(container, classNamePattern)`
Finds a button by className pattern (useful for styled buttons without accessible names).

```typescript
const uploadButton = findButtonByClassName(container, 'bg-blue-600');
```

## Testing Patterns

### 1. Mock Setup with vi.hoisted()

For API mocking, use `vi.hoisted()` to ensure mocks are defined before module imports:

```typescript
const mockApiInstance = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => mockApiInstance),
  },
}));
```

### 2. Navigation Mocking

Always mock `useNavigate` from react-router-dom:

```typescript
const mockNavigate = vi.fn();

vi.mock('react-router-dom', async () => ({
  ...await vi.importActual('react-router-dom'),
  useNavigate: () => mockNavigate,
}));
```

### 3. SessionStorage Setup

Use the helper function for consistent setup:

```typescript
beforeEach(() => {
  setupProjectSession('test-project-123');
});
```

### 4. Component Cleanup

Always clean up after each test:

```typescript
beforeEach(() => {
  cleanupTestEnvironment();
});
```

## Best Practices

1. **Test User Behavior, Not Implementation**
   - Use `screen.getByRole()` and `screen.getByLabelText()` over `getByTestId()`
   - Test what users see and interact with

2. **Mock External Dependencies**
   - Always mock API calls
   - Mock React Router navigation
   - Mock browser APIs (sessionStorage, localStorage)

3. **Use Descriptive Test Names**
   - Start with action: "renders", "displays", "navigates"
   - Include context: "when user clicks button", "after successful upload"

4. **Clean Up After Tests**
   - Clear sessionStorage in `beforeEach()`
   - Reset all mocks with `cleanupTestEnvironment()`

5. **Test Edge Cases**
   - Empty states
   - Error states
   - Loading states
   - Validation errors

6. **Use waitFor for Async Operations**
   ```typescript
   await waitFor(() => {
     expect(screen.getByText('Success')).toBeInTheDocument();
   });
   ```

7. **Group Related Tests**
   ```typescript
   describe('UploadPage', () => {
     describe('file selection', () => {
       it('allows selecting KSS files', () => { ... });
       it('validates file types', () => { ... });
     });
     
     describe('upload workflow', () => {
       it('uploads successfully', () => { ... });
       it('handles errors', () => { ... });
     });
   });
   ```

## Running Tests

See [TESTING.md](../TESTING.md) for full testing documentation.

```bash
# Run all tests
npm test

# Run with coverage
npm test -- --coverage

# Run specific test file
npm test -- UploadPage.test

# Watch mode
npm test -- --watch
```
