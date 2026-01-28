# Git Hooks

This directory contains git hooks that help maintain code quality.

## Pre-commit Hook

The pre-commit hook runs `dotnet format --verify-no-changes` to ensure all C# code follows formatting rules before allowing a commit.

### Installation

To install the pre-commit hook, run one of the following commands from the repository root:

**Windows (PowerShell):**
```powershell
Copy-Item .githooks\pre-commit .git\hooks\pre-commit
```

**Linux/Mac:**
```bash
cp .githooks/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

**Or configure git to use this hooks directory:**
```bash
git config core.hooksPath .githooks
```

### Usage

Once installed, the hook will automatically run when you commit. If formatting issues are detected:

1. Fix them automatically by running:
   ```bash
   dotnet format src/Microsoft.Agents.SDK.sln
   ```

2. Then commit again

To bypass the hook (not recommended):
```bash
git commit --no-verify
```
