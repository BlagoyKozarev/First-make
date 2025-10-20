# Contributing to FirstMake Agent

Благодарим за интереса към FirstMake Agent! Следвайте тези guidelines за допринасяне към проекта.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting](#issue-reporting)

## 📜 Code of Conduct

Този проект следва стандартен Code of Conduct. Като участници и contributors, ние се ангажираме да поддържаме уважителна и включваща среда.

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- Git
- Visual Studio Code (препоръчва се)
- DevContainer extension (опционално, за consistent development environment)

### Setup Development Environment

```bash
# 1. Fork repository на GitHub

# 2. Clone вашия fork
git clone https://github.com/YOUR_USERNAME/First-make.git
cd First-make

# 3. Add upstream remote
git remote add upstream https://github.com/BlagoyKozarev/First-make.git

# 4. Restore dependencies
dotnet restore
cd src/UI && npm install

# 5. Run tests
dotnet test

# 6. Start development servers
# Terminal 1
cd src/Api && dotnet run

# Terminal 2
cd src/AiGateway && dotnet run

# Terminal 3
cd src/UI && npm run dev
```

### Using DevContainer

```bash
# Open in VS Code
code .

# Command Palette (Ctrl+Shift+P)
> Dev Containers: Reopen in Container

# Wait for container build and setup
# All dependencies are pre-installed
```

## 🔄 Development Workflow

### Branch Naming

- **Feature**: `feature/short-description`
- **Bug Fix**: `fix/short-description`
- **Hotfix**: `hotfix/short-description`
- **Documentation**: `docs/short-description`
- **Refactor**: `refactor/short-description`

### Commit Messages

Следваме [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code formatting (no logic changes)
- `refactor`: Code restructuring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**
```bash
feat(core): add coefficient bounds validation
fix(api): handle null reference in export service
docs(readme): update installation instructions
test(optimizer): add edge cases for LP solver
```

### Development Process

1. **Sync with upstream**
   ```bash
   git checkout main
   git fetch upstream
   git merge upstream/main
   ```

2. **Create feature branch**
   ```bash
   git checkout -b feature/my-feature
   ```

3. **Make changes**
   - Write code
   - Add tests
   - Update documentation
   - Run linters

4. **Commit changes**
   ```bash
   git add .
   git commit -m "feat(scope): description"
   ```

5. **Push to fork**
   ```bash
   git push origin feature/my-feature
   ```

6. **Open Pull Request** on GitHub

## 💻 Coding Standards

### C# (.NET)

- **Style**: Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Formatting**: Use `dotnet format`
- **Async**: Suffix async methods with `Async`
- **Nullable**: Enable nullable reference types
- **Naming**:
  - PascalCase за public members
  - camelCase за private fields
  - _camelCase за private instance fields (опционално)

```csharp
// Good
public async Task<OptimizationResult> OptimizeAsync(
    List<MatchedItem> items, 
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(items);
    
    // Implementation
}

// Bad
public OptimizationResult optimize(List<MatchedItem> Items)
{
    if (Items == null) throw new Exception();
    // Implementation
}
```

### TypeScript/React

- **Style**: Follow [Airbnb React/JSX Style Guide](https://github.com/airbnb/javascript/tree/master/react)
- **Formatting**: Use Prettier (configured in project)
- **Components**: Functional components with hooks
- **Naming**:
  - PascalCase за components
  - camelCase за functions/variables
  - UPPER_CASE за constants

```typescript
// Good
export const OptimizationPage: React.FC = () => {
  const [loading, setLoading] = useState(false);
  
  const handleOptimize = async () => {
    setLoading(true);
    // Implementation
  };
  
  return <div>{/* JSX */}</div>;
};

// Bad
function optimization_page() {
  var Loading = false;
  return <div></div>;
}
```

### File Organization

```
src/
├── Api/
│   ├── Services/          # Business logic
│   ├── Middleware/        # Request pipeline
│   ├── Validation/        # Input validation
│   ├── Data/              # Database context
│   └── Program.cs         # Entry point
├── Core.Engine/
│   ├── Models/            # DTOs and domain models
│   └── Services/          # Core business services
└── UI/
    ├── components/        # Reusable components
    ├── pages/             # Route pages
    └── lib/               # Utilities
```

## 🧪 Testing Guidelines

### Unit Tests

- **Framework**: xUnit
- **Coverage**: Minimum 80% за Core.Engine
- **Naming**: `MethodName_Scenario_ExpectedResult`

```csharp
[Fact]
public void Match_WhenExactMatch_ReturnsScore100()
{
    // Arrange
    var matcher = new FuzzyMatcher();
    var boqItem = new ItemDto { Name = "Concrete C25/30" };
    var catalogueItem = new PriceBaseEntry { Name = "Concrete C25/30" };
    
    // Act
    var result = matcher.Match(boqItem, catalogueItem);
    
    // Assert
    Assert.Equal(100.0, result.Score);
}
```

### Integration Tests

```csharp
[Fact]
public async Task Optimize_ValidRequest_ReturnsOptimizedCoefficients()
{
    // Arrange
    var optimizer = new LpOptimizer();
    var request = CreateValidOptimizationRequest();
    
    // Act
    var result = await optimizer.OptimizeAsync(request);
    
    // Assert
    Assert.NotNull(result);
    Assert.True(result.Feasible);
    Assert.All(result.Coeffs, c => Assert.InRange(c, 0.4, 2.0));
}
```

### Running Tests

```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~LpOptimizerTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Watch mode
dotnet watch test
```

## 📥 Pull Request Process

### Before Opening PR

- [ ] Code compiles без errors
- [ ] All tests pass
- [ ] Added tests за new functionality
- [ ] Updated documentation
- [ ] Ran `dotnet format`
- [ ] No merge conflicts with `main`

### PR Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
Describe testing performed

## Checklist
- [ ] Tests pass locally
- [ ] Code follows project style
- [ ] Documentation updated
- [ ] No breaking changes (or documented)

## Related Issues
Closes #123
```

### Review Process

1. **Automated checks** must pass (build, tests)
2. **Code review** от maintainer
3. **Approval** required before merge
4. **Squash and merge** to `main`

## 🐛 Issue Reporting

### Bug Reports

```markdown
**Describe the bug**
Clear description of the bug

**To Reproduce**
1. Go to '...'
2. Click on '...'
3. See error

**Expected behavior**
What should happen

**Screenshots**
If applicable

**Environment:**
- OS: [e.g., Windows 11]
- .NET Version: [e.g., 8.0.1]
- Browser: [e.g., Chrome 120]

**Additional context**
Any other information
```

### Feature Requests

```markdown
**Is your feature request related to a problem?**
Description

**Describe the solution you'd like**
Clear description

**Describe alternatives you've considered**
Other approaches

**Additional context**
Mockups, examples, etc.
```

## 🎯 Areas for Contribution

### High Priority

- **Performance optimization** на LP solver
- **Unit normalization** за повече български мерни единици
- **UI/UX improvements** в React components
- **Error handling** и user feedback
- **Documentation** с примери на български

### Medium Priority

- **Additional file formats** (e.g., ODS, CSV)
- **Batch processing** за множество файлове
- **Export templates** customization
- **Metrics dashboard** enhancements

### Good First Issues

Look for issues labeled `good-first-issue` - тези са подходящи за начинаещи contributors.

## 📞 Contact

- **Issues**: [GitHub Issues](https://github.com/BlagoyKozarev/First-make/issues)
- **Discussions**: [GitHub Discussions](https://github.com/BlagoyKozarev/First-make/discussions)
- **Email**: blagoy.kozarev@example.com (ако е applicable)

## 🙏 Thank You!

Вашият принос прави FirstMake Agent по-добър за целия екип! 🎉
