# Outing Leaderboard

ASCII/CRT-terminal-themed live leaderboard for company outings, built with Blazor WebAssembly (C#).
Runs entirely in the browser as static files — no server required.

## Scoring

| Game                | Points per win | Why |
|---------------------|----------------|-----|
| Smash               | 1              | |
| Pickleball          | 1              | |
| Magic (Commander)   | 3              | You beat three people |

Ties share rank 1 and are shown as co-champions under the crown.

## Updating scores

Open **`/admin`** in a separate tab (there's an `[ ADMIN CONSOLE ]` link at the bottom
of the dashboard). From there you can rename the event, add/remove players, and bump
wins with +/- buttons.

- **This device:** edits save to localStorage instantly; a dashboard tab in the same
  browser picks them up within 5 seconds.
- **Everyone else:** fill in the GitHub sync panel (owner, repo, branch, and a
  fine-grained personal access token scoped to this repo with *Contents: read/write*)
  and hit **PUSH TO GITHUB**. That commits `wwwroot/data/scores.json`, the deploy
  action republishes in about a minute, and every open browser updates on its next
  poll. The token is stored only in your browser's localStorage.

You can also still edit [`wwwroot/data/scores.json`](wwwroot/data/scores.json) by hand:

```json
{
  "eventName": "COMPANY OUTING 2026",
  "updatedAt": "2026-06-11T00:00:00Z",
  "players": [
    { "name": "Matt", "smashWins": 2, "magicWins": 1, "pickleballWins": 1 }
  ]
}
```

Bump `updatedAt` when editing by hand — the app shows whichever copy (published file
vs. local edits) has the newer timestamp.

## Run locally

```
dotnet run
```

## Deploy to GitHub Pages

1. Create a GitHub repo and push this folder to `main`.
2. In the repo: **Settings → Pages → Source → GitHub Actions**.
3. Push (or re-run the action). The site appears at
   `https://<user>.github.io/<repo>/`.

The included workflow (`.github/workflows/deploy.yml`) builds the app,
fixes the `<base href>` for project pages, and adds `.nojekyll` so the
`_framework` folder isn't stripped.
