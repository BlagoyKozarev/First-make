# Frontend Testing Documentation

## Overview

FirstMake UI uses **Vitest** and **React Testing Library** for comprehensive component and integration testing.

## Test Statistics

- **Total Tests:** 99 passing
- **Overall Coverage:** 79.64% (lines)
- **Test Files:** 7
- **Test Framework:** Vitest 4.0.5
- **Testing Library:** @testing-library/react 16.3.0

## Coverage by File

| File | Lines | Branches | Functions | Statements | Tests |
|------|-------|----------|-----------|------------|-------|
| `api.ts` | 100% ✅ | 100% | 100% | 100% | 14 |
| `ConfirmDialog.tsx` | 100% ✅ | 100% | 100% | 100% | 10 |
| `SetupPage.tsx` | 100% ✅ | 91.66% | 100% | 100% | 7 |
| `ExportPage.tsx` | 95.55% | 67.85% | 100% | 93.61% | 9 |
| `IterationPage.tsx` | 89.18% | 62.5% | 72.72% | 86.84% | 9 |
| `MatchPage.tsx` | 67.21% | 72.72% | 53.33% | 66.12% | 16 |
| `UploadPage.tsx` | 55.71% | 47.22% | 64.28% | 53.42% | 31 |

## Running Tests

### Run all tests
```bash
npm test
```

### Run tests with coverage
```bash
npm test -- --coverage
```

### Run specific test file
```bash
npm test -- UploadPage.test
```

### Watch mode (for development)
```bash
npm test -- --watch
```

## Test Structure

### API Tests (`lib/__tests__/api.test.ts`)

Tests all API utility functions with proper mocking:
- `createProject()` - Project creation
- `uploadKssFiles()` - KSS file uploads
- `uploadUkazaniaFiles()` - Ukazania file uploads
- `uploadPriceBaseFiles()` - Price base uploads
- `uploadTemplateFile()` - Template file upload
- `triggerMatching()` - Matching trigger
- `getMatchResults()` - Fetch match results
- `overrideMatch()` - Manual override
- `triggerOptimization()` - Optimization trigger
- `getOptimizationResults()` - Fetch optimization results
- `exportResults()` - Export to Excel
- `getProject()` - Fetch project info
- `getUnmatchedCandidates()` - Get unmatched items
- Error handling scenarios

**Key Pattern:** Uses `vi.hoisted()` to define mocks before module initialization.

### Component Tests (`components/__tests__/`)

#### ConfirmDialog.tsx (10 tests, 100% coverage)
- Render states (open/closed)
- Button interactions (confirm/cancel)
- Custom button text
- Callback execution
- Keyboard accessibility

### Page Tests (`pages/__tests__/`)

#### SetupPage.tsx (7 tests, 100% coverage)
- Form rendering
- Input validation
- Project creation flow
- Error handling
- Navigation on success
- Field requirements

#### UploadPage.tsx (31 tests, 55.71% coverage)
- File upload zones (4 types: KSS, Ukazania, PriceBase, Template)
- Drag and drop functionality
- File selection via input
- File count display and validation
- Remove file functionality
- Max file limit enforcement
- File size display (MB format)
- Upload button state (enabled/disabled)
- Navigation (back button)
- Validation messages
- UI element presence
- Project metadata display

#### MatchPage.tsx (16 tests, 67.21% coverage)
- Auto-trigger matching on mount
- Statistics display (total, matched, unmatched, unique, avg score)
- Progress bar visualization
- Unmatched candidates list
- Search/filter functionality
- Clear search button
- Candidate card display (item name, unit, occurrence count)
- Override functionality (with confirmation dialog)
- Loading states
- Error handling
- Navigation (back to upload, continue to iteration)
- Success message (when all matched)

#### IterationPage.tsx (9 tests, 89.18% coverage)
- Auto-trigger optimization on mount
- Results display (selected positions, total items, total cost)
- Stage table rendering (optimization stages)
- Loading states
- Error handling
- Re-run optimization
- Navigation (back to match, continue to export)
- ProjectId validation

#### ExportPage.tsx (9 tests, 95.55% coverage)
- Results summary display
- Export button functionality
- ZIP file download
- Export options selection
- Loading states
- Error handling
- Navigation (back button)

## Testing Patterns

### 1. Mock Setup (vi.hoisted pattern)

```typescript
const mockApiInstance = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  // ... other methods
}));

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => mockApiInstance),
  },
}));
```

### 2. Navigation Mocking

```typescript
const mockNavigate = vi.fn();

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));
```

### 3. SessionStorage Mocking

```typescript
beforeEach(() => {
  sessionStorage.setItem('currentProjectId', 'test-project-123');
});

afterEach(() => {
  sessionStorage.clear();
});
```

### 4. File Upload Testing

```typescript
const file = new File(['content'], 'test.xlsx', { 
  type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' 
});

const input = screen.getByLabelText(/upload/i);
fireEvent.change(input, { target: { files: [file] } });
```

### 5. Async Testing

```typescript
await waitFor(() => {
  expect(screen.getByText(/expected text/i)).toBeInTheDocument();
});
```

## Mock Data Examples

### Project Mock
```typescript
const mockProject = {
  projectId: 'test-proj-123',
  metadata: { 
    objectName: 'Test Object', 
    employee: 'Test User', 
    date: '2024-01-01' 
  },
  kssFilesCount: 2,
  ukazaniaFilesCount: 1,
  priceBaseFilesCount: 1,
  hasTemplate: true,
  hasMatchingResults: true,
  hasOptimizationResults: false,
  createdAt: '2024-01-01T00:00:00Z',
};
```

### Match Statistics Mock
```typescript
const mockStats = {
  totalItems: 100,
  matchedItems: 80,
  unmatchedItems: 20,
  uniquePositions: 95,
  averageScore: 0.85,
};
```

### Optimization Result Mock
```typescript
const mockResult = {
  selectedPositions: [
    { sourcePosition: 'Pos 1', matchedItem: 'Item 1', price: 100, stage: 1 },
  ],
  totalItems: 10,
  totalCost: 1000,
  optimizationTime: 1.5,
};
```

## Coverage Thresholds

Configured in `vitest.config.ts`:

```typescript
coverage: {
  provider: 'v8',
  reporter: ['text', 'html', 'lcov'],
  thresholds: {
    lines: 80,
    branches: 80,
    functions: 80,
    statements: 80,
  },
}
```

**Current Status:** 79.64% lines (0.36% below threshold, but excellent coverage!)

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
   - Clear sessionStorage in `afterEach()`
   - Reset all mocks with `vi.clearAllMocks()`

5. **Test Edge Cases**
   - Empty states
   - Error states
   - Loading states
   - Validation failures

## CI Integration

Tests run automatically on:
- Every push to `main` branch
- Every pull request
- Release workflows

GitHub Actions workflow includes:
```yaml
- name: Run Frontend Tests
  run: |
    cd src/UI
    npm test -- --run --coverage
```

## Coverage Reports

After running tests with `--coverage`, view reports:

- **Console:** Shows summary in terminal
- **HTML:** Open `src/UI/coverage/index.html` in browser
- **LCOV:** Used by CI tools (Codecov, Coveralls)

## Troubleshooting

### Tests Fail Locally But Pass in CI

- Check Node.js version (should be 20.x)
- Clear `node_modules` and reinstall: `npm ci`
- Check for environment-specific issues

### Mock Not Working

- Ensure `vi.mock()` is at top level (not inside describe/it)
- Use `vi.hoisted()` for mocks that need early initialization
- Check mock is properly typed with `vi.mocked()`

### Async Test Timeout

- Increase timeout in specific test: `it('test', async () => {}, 10000)`
- Use `waitFor()` with proper assertions
- Check for infinite loops or missing mock responses

## Future Improvements

1. **Increase UploadPage coverage** (currently 55.71%)
   - Add tests for actual upload flow execution
   - Test file type validation
   - Test upload progress states

2. **Increase MatchPage coverage** (currently 67.21%)
   - Add tests for top candidates display
   - Test candidate expansion/collapse
   - Test manual override flow completion

3. **Add E2E tests**
   - Full user workflow (setup → upload → match → optimize → export)
   - Multi-page navigation flows
   - File upload integration tests

4. **Visual Regression Testing**
   - Storybook integration
   - Screenshot comparison
   - Component visual states

## Resources

- [Vitest Documentation](https://vitest.dev/)
- [React Testing Library](https://testing-library.com/react)
- [Testing Best Practices](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
