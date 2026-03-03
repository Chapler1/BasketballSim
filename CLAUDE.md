# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the app (HTTP)
dotnet run --project BasketballSim/BasketballSim.csproj --launch-profile http

# Run the app (HTTPS)
dotnet run --project BasketballSim/BasketballSim.csproj --launch-profile https

# Build
dotnet build BasketballSim/BasketballSim.csproj

# Watch mode (hot reload)
dotnet watch --project BasketballSim/BasketballSim.csproj
```

- HTTP: http://localhost:5094
- HTTPS: https://localhost:7232

## Architecture

This is a **Blazor Web App** targeting **.NET 10.0** using **Interactive Server** render mode (SignalR-based, server-side rendering with real-time interactivity).

**Key entry points:**
- `BasketballSim/Program.cs` — service registration and middleware pipeline
- `BasketballSim/Components/App.razor` — root HTML document
- `BasketballSim/Components/Routes.razor` — router with `MainLayout` as the default layout
- `BasketballSim/Components/_Imports.razor` — global Razor `@using` directives

**Component organization:**
- `Components/Layout/` — shell components: `MainLayout`, `NavMenu`, `ReconnectModal`
- `Components/Pages/` — routable pages decorated with `@page`

**Rendering:** Components use Interactive Server mode by default (add `@rendermode InteractiveServer` to a page/component to opt into interactivity). The `BlazorDisableThrowNavigationException` flag is set so navigation exceptions are swallowed silently.

**Styling:** Bootstrap 5 (via static assets), plus `app.css` for global styles and scoped `.razor.css` files co-located with components.
