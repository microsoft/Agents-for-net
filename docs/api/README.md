# API documentation

The SDK libraries already enable `GenerateDocumentationFile`, so each library build emits an XML
documentation file alongside its assembly. DocFX converts the public APIs and XML comments into a
searchable website using its modern template.

## Generate the documentation

From the repository root, run:

```powershell
dotnet tool restore
dotnet docfx .\docs\api\docfx.json
```

The generated website is written to `docs\api\_site`.

## Preview locally

Build and serve the site:

```powershell
dotnet docfx .\docs\api\docfx.json --serve
```

Open the URL printed by DocFX, which is `http://localhost:8080` by default. Press Ctrl+C to stop the
server.

DocFX is pinned in `.config\dotnet-tools.json`, so no machine-wide installation is required. New
library projects under `src\libraries` are included automatically unless they are analyzer
projects.
