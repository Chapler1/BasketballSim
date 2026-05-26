"""
Step 5 (optional): Fetch NBA free agents and inject them into players.json.

Uses nba_api.CommonAllPlayers to get every player active in the current season,
then cross-references against the ESPN team rosters in nba_roster.json.
Players in the NBA dataset but NOT on any ESPN team roster = free agents.

Adds them to players.json with Team="" and attributes from the injury DB
(if they already have ratings there) or all-50 defaults.
"""

import json
import sqlite3
import os
import time
import unicodedata
import re
import difflib

PLAYERS_JSON = os.path.join(os.path.dirname(__file__),
                             "..", "BasketballSim", "Data", "players.json")
ROSTER_JSON  = os.path.join(os.path.dirname(__file__),
                             "..", "BasketballSim", "Data", "nba_roster.json")
DB_PATH      = os.path.join(os.path.dirname(__file__), "injuries.db")

SEASON       = "2025-26"
FUZZY_THRESH = 0.82


def normalize(name: str) -> str:
    n = unicodedata.normalize("NFD", name)
    n = "".join(c for c in n if unicodedata.category(c) != "Mn")
    n = n.lower().strip()
    n = re.sub(r"\b(jr\.?|sr\.?|ii|iii|iv)\b", "", n).strip()
    n = re.sub(r"[.’‘']", "", n)
    n = re.sub(r"-", " ", n)
    n = re.sub(r"\s+", " ", n)
    return n


def fuzzy_match(name: str, pool: dict[str, str], threshold: float = FUZZY_THRESH) -> str | None:
    norm = normalize(name)
    if norm in pool:
        return pool[norm]
    best_ratio, best = 0.0, None
    for key, orig in pool.items():
        r = difflib.SequenceMatcher(None, norm, key).ratio()
        if r > best_ratio:
            best_ratio, best = r, orig
    return best if best_ratio >= threshold else None


def fetch_nba_players() -> list[dict]:
    """Fetch all active NBA players this season via nba_api."""
    try:
        from nba_api.stats.endpoints import CommonAllPlayers
        time.sleep(0.5)
        df = CommonAllPlayers(
            is_only_current_season=1,
            season=SEASON
        ).get_data_frames()[0]

        players = []
        for _, row in df.iterrows():
            players.append({
                "name":    row["DISPLAY_FIRST_LAST"],
                "team_id": int(row.get("TEAM_ID", 0)),
                "team":    row.get("TEAM_NAME", ""),
            })
        return players
    except Exception as e:
        print(f"Error fetching NBA players: {e}")
        return []


def run(verbose: bool = True) -> None:
    # Load current ESPN rosters to know who's already on a team
    if not os.path.exists(ROSTER_JSON):
        print("nba_roster.json not found — run 'Fetch Rosters from ESPN' in the UI first.")
        return

    with open(ROSTER_JSON, encoding="utf-8") as f:
        roster_data = json.load(f)

    rostered_norms: set[str] = set()
    for team in roster_data.get("teams", []):
        for p in team.get("players", []):
            rostered_norms.add(normalize(p["name"]))

    if verbose:
        print(f"ESPN roster: {len(rostered_norms)} rostered players across 30 teams.")

    # Fetch NBA players from API
    if verbose:
        print(f"Fetching active NBA players for {SEASON}…")
    nba_players = fetch_nba_players()
    if not nba_players:
        print("No players returned from nba_api.")
        return

    # Free agents = in NBA dataset but not in any ESPN roster (team_id == 0 or team blank)
    free_agents = [
        p for p in nba_players
        if p["team_id"] == 0 or not p["team"]
    ]

    if verbose:
        print(f"NBA API returned {len(nba_players)} active players, "
              f"{len(free_agents)} have no team (free agents).")

    # Load players.json
    players_path = os.path.abspath(PLAYERS_JSON)
    if not os.path.exists(players_path):
        print(f"players.json not found at {players_path}")
        return

    with open(players_path, encoding="utf-8") as f:
        db = json.load(f)

    players = db.get("Players", [])
    existing_norms = {normalize(p["Name"]): p for p in players}

    added = 0
    already_present = 0
    updated_to_fa = 0

    for fa in free_agents:
        name = fa["name"]
        norm = normalize(name)

        if norm in existing_norms:
            # Already in DB — ensure marked as free agent if they were on a team
            rec = existing_norms[norm]
            if rec.get("Team"):
                rec["Team"]     = ""
                rec["TeamAbbr"] = ""
                updated_to_fa += 1
            else:
                already_present += 1
            continue

        # New free agent — add with default attributes
        # (injury ratings from DB if available, else all 99)
        inj_attrs = {}
        if os.path.exists(DB_PATH):
            try:
                conn = sqlite3.connect(DB_PATH)
                rows = conn.execute(
                    "SELECT body_part_key, final_rating FROM player_ratings WHERE player_norm=?",
                    (name,)
                ).fetchall()
                conn.close()
                for bpk, rating in rows:
                    if rating < 99:
                        inj_attrs[bpk] = rating
            except Exception:
                pass

        record = {
            "Name":            name,
            "Team":            "",
            "TeamAbbr":        "",
            "Positions":       ["PG"],
            "Height":          "6'6\"",
            "Overall":         75,
            "Attrs": {
                "Height": 50, "Strength": 50, "Speed": 50, "Jumping": 50,
                "Endurance": 50, "Inside": 50, "Dunks": 50, "FreeThrow": 50,
                "MidRange": 50, "ThreePoint": 50, "oBBIQ": 50, "dBBIQ": 50,
                "Hustle": 50, "Dribbling": 50, "Passing": 50, "RebOff": 50,
                "RebDef": 50, "PerimDef": 50, "IntDef": 50, "FoulTend": 50,
                "DomHand": 0,
                # All injury keys default to 99
                **{k: 99 for k in [
                    "InjHead","InjNeck",
                    "InjLShoulder","InjRShoulder","InjLUpperArm","InjRUpperArm",
                    "InjLElbow","InjRElbow","InjLForearm","InjRForearm",
                    "InjLWrist","InjRWrist","InjLHand","InjRHand",
                    "InjLFingers","InjRFingers",
                    "InjChest","InjUpperBack","InjAbdominals","InjLowerBack",
                    "InjLOblique","InjROblique",
                    "InjLHip","InjRHip","InjLHamstring","InjRHamstring",
                    "InjLQuad","InjRQuad","InjLKnee","InjRKnee",
                    "InjLShinCalf","InjRShinCalf","InjLAchilles","InjRAchilles",
                    "InjLAnkle","InjRAnkle","InjLFoot","InjRFoot",
                    "InjLToes","InjRToes",
                ]},
                # Overwrite with any computed injury ratings
                **inj_attrs,
            },
            "Tends": {
                "Touches": 50, "Drive": 50, "ThreePt": 50, "MidRange": 50,
                "PostUp": 50, "Iso": 50, "PullUp": 50, "Cut": 50,
                "OffReb": 50, "Steal": 50, "Block": 50,
            },
        }

        players.append(record)
        existing_norms[norm] = record
        added += 1

    db["Players"] = players

    with open(players_path, "w", encoding="utf-8") as f:
        json.dump(db, f, indent=2)

    if verbose:
        print(f"\nFree agent injection complete:")
        print(f"  Added new free agents:       {added}")
        print(f"  Updated to FA (had a team):  {updated_to_fa}")
        print(f"  Already FA in DB:            {already_present}")
        print(f"\nSaved: {players_path}")


if __name__ == "__main__":
    run()
