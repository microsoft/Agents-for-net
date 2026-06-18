# DocFX API Documentation - Setup Notes

## Status: Initial Setup Complete (2026-06-17)

## Tool Choice
- **DocFX v2** (Microsoft's official .NET doc generator)
- Installed via: `dotnet tool install -g docfx`
- Tested version: 2.78.5

## What's Done
- [x] `docfx/docfx.json` — main config targeting 14 core library projects (net8.0)
- [x] `docfx/filterConfig.yml` — excludes Analyzers, Internal namespaces, EditorBrowsable(Never)
- [x] `docfx/index.md` — landing page with package table, quick links, code example
- [x] `docfx/toc.yml` — top-level nav (API Reference, Architecture, Getting Started)
- [x] `docfx/articles/index.md` — conceptual docs index page
- [x] `docfx/articles/toc.yml` — sidebar TOC for conceptual docs (7 articles from docs/)
- [x] `docfx/.gitignore` — ignores generated `_site/` and `api/` folders
- [x] `.github/workflows/docs.yml` — GitHub Actions workflow (build on PR, deploy on push to main)
- [x] Local build verified: 551 pages (541 API + 9 conceptual + 1 landing), 0 errors

## Libraries Included (14 projects)
1. Microsoft.Agents.Core
2. Microsoft.Agents.Authentication (core abstractions)
3. Microsoft.Agents.Authentication.Msal
4. Microsoft.Agents.Builder
5. Microsoft.Agents.Builder.Dialogs
6. Microsoft.Agents.Builder.Testing
7. Microsoft.Agents.Hosting.AspNetCore
8. Microsoft.Agents.Client
9. Microsoft.Agents.Connector
10. Microsoft.Agents.CopilotStudio.Client
11. Microsoft.Agents.Storage
12. Microsoft.Agents.Storage.Blobs
13. Microsoft.Agents.Storage.CosmosDb
14. Microsoft.Agents.Storage.Transcript

## Libraries NOT Included (by user choice)
- Microsoft.Agents.Extensions.Teams
- Microsoft.Agents.Extensions.Teams.AI
- Microsoft.Agents.Extensions.SharePoint
- Microsoft.Agents.Extensions.Slack
- Microsoft.Agents.Hosting.DirectLine.NamedPipes
- Microsoft.Agents.Hosting.AspNetCore.A2A
- Microsoft.Agents.Core.Analyzers

## Decisions Made
- **Trigger:** Build on PR (validation only), deploy on push to main
- **Template:** Default modern template (dark mode, responsive)
- **Theming:** Deferred — keep default for now, can customize later with logo + CSS
- **Conceptual docs:** Included from `docs/` folder (copied at CI time)

## What's Needed Before First Deploy
1. **Enable GitHub Pages** in repo Settings → Pages → Source → select "GitHub Actions"
2. The `github-pages` environment will auto-create on first deploy

## How to Build Locally
```bash
dotnet tool install -g docfx

# Copy conceptual docs (CI does this automatically)
cp docs/*.md docfx/articles/

# Build the site
docfx docfx/docfx.json

# Serve locally for preview
docfx serve docfx/_site --port 8080
```

## Known Warnings (62)
- All are pre-existing `InvalidCref` warnings from XML doc comments in source code
- Mostly `!:` prefixed cref values that DocFX can't resolve (e.g. `!:RestConnectorClient`, `!:ArgumentException`)
- Non-blocking — docs generate fine, just missing some cross-reference links
- Can be fixed later by correcting the cref values in source files

## Future Enhancements
- [ ] Add custom logo and branding (logo path + CSS override)
- [ ] Add more conceptual articles (getting started, tutorials)
- [ ] Include Extension libraries if desired
- [ ] Fix InvalidCref warnings in source XML doc comments
- [ ] Add custom favicon
- [ ] Consider versioned docs for release branches
