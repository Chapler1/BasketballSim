"""
Step 4: Write computed durability ratings into players.json.

Matching strategy:
  1. Exact name match (case-insensitive)
  2. Normalized match (strip accents, suffixes like Jr./Sr./III)
  3. Fuzzy match via difflib (threshold 0.85)

Only sets body-part keys where final_rating < 99 (leaves unrated parts at default 99).
Saves an audit file computed_ratings.json alongside the DB.
"""

import json
import sqlite3
import os
import unicodedata
import difflib
import re

DB_PATH         = os.path.join(os.path.dirname(__file__), "injuries.db")
PLAYERS_JSON    = os.path.join(os.path.dirname(__file__),
                               "..", "BasketballSim", "Data", "players.json")
AUDIT_JSON      = os.path.join(os.path.dirname(__file__), "computed_ratings.json")

FUZZY_THRESHOLD = 0.85


def normalize_for_match(name: str) -> str:
    """Lowercase, strip accents, remove common suffixes."""
    n = unicodedata.normalize("NFD", name)
    n = "".join(c for c in n if unicodedata.category(c) != "Mn")
    n = n.lower().strip()
    n = re.sub(r"\b(jr\.?|sr\.?|ii|iii|iv)\b", "", n).strip()
    n = re.sub(r"\s+", " ", n)
    return n


def build_ratings_map(conn: sqlite3.Connection) -> dict[str, dict[str, int]]:
    """Return {player_norm: {body_part_key: final_rating}} for ALL players in the DB.
    Includes 99-rated entries so stale injections from prior runs can be cleared."""
    rows = conn.execute(
        "SELECT player_norm, body_part_key, final_rating FROM player_ratings"
    ).fetchall()

    ratings: dict[str, dict[str, int]] = {}
    for player, bpk, rating in rows:
        ratings.setdefault(player, {})[bpk] = rating
    return ratings


def match_player(
    db_name: str,
    json_names: list[str],
    norm_map: dict[str, str],
) -> str | None:
    """
    Find the best matching player name in players.json for a DB player name.
    Returns the matched json name or None.
    """
    db_norm = normalize_for_match(db_name)

    # Exact normalized match
    if db_norm in norm_map:
        return norm_map[db_norm]

    # Fuzzy match
    best_ratio = 0.0
    best_match = None
    for jn_norm, jn_orig in norm_map.items():
        ratio = difflib.SequenceMatcher(None, db_norm, jn_norm).ratio()
        if ratio > best_ratio:
            best_ratio = ratio
            best_match = jn_orig

    if best_ratio >= FUZZY_THRESHOLD:
        return best_match
    return None


def run(verbose: bool = True, dry_run: bool = False) -> None:
    conn = sqlite3.connect(DB_PATH)
    ratings_map = build_ratings_map(conn)
    conn.close()

    if not ratings_map:
        print("No ratings found. Run analyze.py first.")
        return

    players_path = os.path.abspath(PLAYERS_JSON)
    if not os.path.exists(players_path):
        print(f"players.json not found at: {players_path}")
        return

    with open(players_path, "r", encoding="utf-8") as f:
        db = json.load(f)

    players = db.get("Players", [])
    json_names   = [p["Name"] for p in players]
    norm_map     = {normalize_for_match(n): n for n in json_names}

    matched_players  = 0
    unmatched_players: list[str] = []
    total_keys_set   = 0
    audit: dict[str, dict[str, int]] = {}

    for db_player, part_ratings in sorted(ratings_map.items()):
        json_name = match_player(db_player, json_names, norm_map)

        if json_name is None:
            unmatched_players.append(db_player)
            continue

        # Find the player record
        player_rec = next((p for p in players if p["Name"] == json_name), None)
        if player_rec is None:
            continue

        keys_set = 0
        audit_entry: dict[str, int] = {}

        # Clear all existing Inj* keys first so stale prior-run values are reset
        if not dry_run:
            attrs = player_rec.setdefault("Attrs", {})
            for k in [k for k in list(attrs.keys()) if k.startswith("Inj")]:
                del attrs[k]

        for bpk, rating in part_ratings.items():
            if rating >= 99:
                continue
            if not dry_run:
                player_rec["Attrs"][bpk] = rating
            audit_entry[bpk] = rating
            keys_set += 1

        if keys_set > 0:
            matched_players += 1
            total_keys_set  += keys_set
            audit[json_name] = audit_entry

    if not dry_run:
        with open(players_path, "w", encoding="utf-8") as f:
            json.dump(db, f, indent=2)
        with open(AUDIT_JSON, "w", encoding="utf-8") as f:
            json.dump(audit, f, indent=2, sort_keys=True)

    if verbose:
        print(f"\nInjection {'(DRY RUN) ' if dry_run else ''}complete:")
        print(f"  Players updated:      {matched_players}")
        print(f"  Body-part keys set:   {total_keys_set}")
        print(f"  Unmatched DB players: {len(unmatched_players)}")

        if unmatched_players:
            print("\nUnmatched players (DB name -> no players.json match):")
            for name in sorted(unmatched_players)[:30]:
                print(f"  {name}")
            if len(unmatched_players) > 30:
                print(f"  ... and {len(unmatched_players)-30} more")

        if not dry_run:
            print(f"\nSaved: {players_path}")
            print(f"Audit: {AUDIT_JSON}")


if __name__ == "__main__":
    import sys
    dry = "--dry-run" in sys.argv
    run(dry_run=dry)
