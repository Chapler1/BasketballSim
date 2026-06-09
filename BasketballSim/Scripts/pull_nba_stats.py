"""
Pull historical NBA per-game + advanced player stats from stats.nba.com via nba_api.
Saves to:
  BasketballSim/Data/historical_player_stats.json  — full per-season stats
  BasketballSim/Data/seasons_played.json           — {normalizedName: seasonCount}

Install:  pip install nba_api
Run from repo root:  python BasketballSim/Scripts/pull_nba_stats.py

Pulls seasons 2000-01 through 2024-25 (all real completed seasons).
The 2025-26 season is your sim season — it won't appear here.
Players absent from this file are treated as 2025-26 rookies by the sim.
"""

import json
import os
import time
import unicodedata

try:
    from nba_api.stats.endpoints import leaguedashplayerstats
except ImportError:
    print("ERROR: nba_api not installed. Run: pip install nba_api")
    raise

SEASONS  = [f"{y}-{str(y + 1)[2:]:0>2}" for y in range(2000, 2025)]
OUT_PATH = os.path.join(os.path.dirname(__file__), "..", "Data", "historical_player_stats.json")
SP_PATH  = os.path.join(os.path.dirname(__file__), "..", "Data", "seasons_played.json")

def normalize_name(name: str) -> str:
    """Lowercase, strip diacritics, remove . ' - so names match across data sources."""
    nfd = unicodedata.normalize("NFD", name)
    stripped = "".join(c for c in nfd if unicodedata.category(c) != "Mn")
    return stripped.lower().replace(".", "").replace("'", "").replace("-", " ").strip()

# key: (player_name, season) → season record dict
index: dict[tuple, dict] = {}
result: dict[str, list]  = {}

def pull(season: str, measure: str) -> list:
    return leaguedashplayerstats.LeagueDashPlayerStats(
        season=season,
        measure_type_detailed_defense=measure,
        per_mode_detailed="PerGame",
        timeout=30,
    ).get_normalized_dict()["LeagueDashPlayerStats"]

# ── Pass 1: Base stats ────────────────────────────────────────────────────────
print("=== PASS 1: Base stats ===")
for season in SEASONS:
    print(f"  {season} base...", end=" ", flush=True)
    try:
        rows = pull(season, "Base")
        for p in rows:
            name = p["PLAYER_NAME"]
            rec = {
                "season": season,
                "team":   p["TEAM_ABBREVIATION"],
                "gp":     p["GP"],
                "min":    round(float(p["MIN"]),    1),
                "pts":    round(float(p["PTS"]),    1),
                "reb":    round(float(p["REB"]),    1),
                "ast":    round(float(p["AST"]),    1),
                "stl":    round(float(p["STL"]),    1),
                "blk":    round(float(p["BLK"]),    1),
                "tov":    round(float(p["TOV"]),    1),
                "fgm":    round(float(p["FGM"]),    1),
                "fga":    round(float(p["FGA"]),    1),
                "fgPct":  round(float(p["FG_PCT"]), 3),
                "fg3m":   round(float(p["FG3M"]),   1),
                "fg3a":   round(float(p["FG3A"]),   1),
                "fg3Pct": round(float(p["FG3_PCT"]),3),
                "ftm":    round(float(p["FTM"]),    1),
                "fta":    round(float(p["FTA"]),    1),
                "ftPct":  round(float(p["FT_PCT"]), 3),
            }
            if name not in result:
                result[name] = []
            result[name].append(rec)
            index[(name, season)] = rec
        print(f"{len(rows)} players")
    except Exception as e:
        print(f"ERROR: {e}")
    time.sleep(1.5)

# ── Pass 2: Advanced stats — merge into existing records ──────────────────────
print("\n=== PASS 2: Advanced stats ===")
for season in SEASONS:
    print(f"  {season} advanced...", end=" ", flush=True)
    try:
        rows = pull(season, "Advanced")
        merged = 0
        for p in rows:
            name = p["PLAYER_NAME"]
            rec  = index.get((name, season))
            if rec is None:
                continue
            def safe(key):
                v = p.get(key)
                return round(float(v), 3) if v is not None else None
            rec["tsPct"]    = safe("TS_PCT")
            rec["efgPct"]   = safe("EFG_PCT")
            rec["usgPct"]   = safe("USG_PCT")
            rec["astPct"]   = safe("AST_PCT")
            rec["orebPct"]  = safe("OREB_PCT")
            rec["drebPct"]  = safe("DREB_PCT")
            rec["rebPct"]   = safe("REB_PCT")
            rec["offRating"] = safe("OFF_RATING")
            rec["defRating"] = safe("DEF_RATING")
            rec["netRating"] = safe("NET_RATING")
            merged += 1
        print(f"{merged} merged")
    except Exception as e:
        print(f"ERROR: {e}")
    time.sleep(1.5)

# ── Write historical_player_stats.json ───────────────────────────────────────
os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(result, f, separators=(",", ":"))

total_seasons = sum(len(v) for v in result.values())
print(f"\nDone. {len(result)} players, {total_seasons} player-seasons.")
print(f"Saved to: {os.path.abspath(OUT_PATH)}")

# ── Write seasons_played.json ─────────────────────────────────────────────────
# Keyed by normalized name so the C# loader can match despite diacritics / punctuation.
# Players absent from this file have no real NBA history and are treated as 2025-26 rookies.
seasons_played: dict[str, int] = {}
for name, season_list in result.items():
    key = normalize_name(name)
    # If multiple raw names normalize to the same key, keep the highest count
    seasons_played[key] = max(seasons_played.get(key, 0), len(season_list))

with open(SP_PATH, "w", encoding="utf-8") as f:
    json.dump(seasons_played, f, separators=(",", ":"), sort_keys=True)

print(f"Seasons played ({len(seasons_played)} players) → {os.path.abspath(SP_PATH)}")
