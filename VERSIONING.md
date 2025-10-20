# Versioning Guide

DaoStudio uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) for automatic version management.

## Single Source of Truth

**`version.json`** at the repository root is the single source of truth for all version numbers.

## How It Works

### Automatic Versioning
- Nerdbank.GitVersioning automatically generates version numbers based on `version.json` and git history
- Version format: `MAJOR.MINOR.PATCH.HEIGHT` where HEIGHT is the number of commits since the version was set
- Example: `0.1.0.42` means version 0.1.0 with 42 commits since last version bump

### For Releases

1. **Update version.json** when you want to bump the version:
   ```json
   {
     "version": "1.2.0"
   }
   ```

2. **Commit and push** the change:
   ```bash
   git add version.json
   git commit -m "Bump version to 1.2.0"
   git push
   ```

3. **Create a release tag**:
   ```bash
   git tag v1.2.0
   git push --tags
   ```
   
   This will trigger the GitHub workflow to:
   - Extract version from `version.json`
   - Build for all platforms
   - Create a GitHub release with the version number

4. **Manual workflow dispatch** (alternative):
   - Go to Actions → Release workflow → Run workflow
   - Enter the version number manually
   - This overrides the version.json value for that specific run

## Version Structure

```json
{
  "version": "MAJOR.MINOR.PATCH",
  "assemblyVersion": {
    "precision": "revision"
  }
}
```

- **MAJOR**: Breaking changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

## Integration Points

### .NET Projects
All projects automatically get version information through:
- Assembly version attributes
- NuGet package version (if applicable)
- File version information

### GitHub Workflow
The `.github/workflows/release.yml` reads directly from `version.json` to:
- Name releases
- Tag artifacts
- Set build metadata

## Best Practices

1. **Never hardcode versions** in project files or workflows
2. **Bump version.json** before creating release tags
3. **Use semantic versioning** conventions (MAJOR.MINOR.PATCH)
4. **Create git tags** that match the version: `v1.2.0`
5. **Keep version.json in the repository root** for easy access

## Checking Current Version

```bash
# Install nbgv tool (one time)
dotnet tool install -g nbgv

# Check current version
nbgv get-version
```

## Resources

- [Nerdbank.GitVersioning Documentation](https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md)
- [Semantic Versioning](https://semver.org/)
