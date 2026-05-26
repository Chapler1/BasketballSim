"""
Step 3: Compute league baselines and per-player durability ratings.

Algorithm:
  1. Detect injury episodes by grouping consecutive Out/Questionable rows for
     (player, body_part_key) within a season. Gap >10 days = new episode.
  2. Score each episode by: log(1 + duration_days) * severity_factor * recency_weight
  3. Normalize each player's score against the league median for that body part
  4. Map normalized score to 5–99 rating via log curve:
       0 (no injuries)    → 99
       1 (league avg)     → ~70
       2x avg             → ~52
       8x+ avg (Embiid)   → ~5–15

Outputs:
  - player_ratings table in injuries.db
  - injury_type_analysis.csv (league-wide body-part frequency + avg duration)
"""

import sqlite3
import os
import csv
import math
from collections import defaultdict
from datetime import datetime, date

DB_PATH     = os.path.join(os.path.dirname(__file__), "injuries.db")
ANALYSIS_CSV = os.path.join(os.path.dirname(__file__), "injury_type_analysis.csv")

# Recency multipliers by season (more recent = higher weight)
RECENCY = {"2021-22": 1.0, "2022-23": 1.5, "2023-24": 2.0, "2024-25": 3.0}

SCHEMA_RATINGS = """
CREATE TABLE IF NOT EXISTS player_ratings (
    player_norm     TEXT NOT NULL,
    body_part_key   TEXT NOT NULL,
    num_episodes    INT  DEFAULT 0,
    total_days      INT  DEFAULT 0,
    weighted_score  REAL DEFAULT 0,
    normalized      REAL DEFAULT 0,
    final_rating    INT  DEFAULT 99,
    PRIMARY KEY (player_norm, body_part_key)
);
"""

EPISODE_GAP_DAYS = 10   # gap larger than this = new episode

# Minimum weighted score to receive a sub-99 rating.
# A 2-day Out episode in the oldest season (2021-22, recency=1.0) scores ~1.65.
# Single-day Q-only entries score 0.69–2.08; Min_SCORE=1.5 filters those out.
MIN_SCORE = 4.0


def load_entries(conn: sqlite3.Connection) -> dict:
    """
    Return {(player_norm, body_part_key): [(game_date_str, status, season), ...]}
    sorted by date.
    """
    rows = conn.execute(
        "SELECT player_norm, body_part_key, game_date, status, season "
        "FROM parsed_entries ORDER BY player_norm, body_part_key, game_date"
    ).fetchall()

    grouped: dict = defaultdict(list)
    for player, bpk, gd, status, season in rows:
        grouped[(player, bpk)].append((gd, status, season))
    return grouped


def detect_episodes(entries: list[tuple]) -> list[dict]:
    """
    Detect distinct injury episodes from a list of (game_date, status, season) tuples.
    Returns list of episode dicts with keys: season, start, duration_days, had_out.
    """
    episodes = []
    current: list[tuple] = []

    def flush():
        if not current:
            return
        dates = [datetime.strptime(e[0], "%Y-%m-%d").date() for e in current]
        duration = (max(dates) - min(dates)).days + 1
        had_out   = any(e[1].lower() == "out" for e in current)
        season    = current[0][2]  # use first row's season
        episodes.append({
            "season":       season,
            "start":        current[0][0],
            "duration_days": duration,
            "had_out":      had_out,
        })

    for game_date, status, season in entries:
        gd = datetime.strptime(game_date, "%Y-%m-%d").date()
        if current:
            prev_date = datetime.strptime(current[-1][0], "%Y-%m-%d").date()
            if (gd - prev_date).days > EPISODE_GAP_DAYS:
                flush()
                current = []
        current.append((game_date, status, season))

    flush()
    return episodes


def score_episodes(episodes: list[dict]) -> float:
    """Compute the total weighted score for a player's episodes on one body part."""
    total = 0.0
    for ep in episodes:
        duration_factor  = math.log(1 + ep["duration_days"])
        severity_factor  = 1.5 if ep["had_out"] else 1.0
        recency          = RECENCY.get(ep["season"], 1.0)
        total += duration_factor * severity_factor * recency
    return total


def rating_from_normalized(normalized: float) -> int:
    """
    Convert normalized score (0=none, 1=league avg, 8+=extreme) to 5–99 rating.
    Curve: 0→99, 1→~70, 2→~53, 4→~31, 8+→~7
    Derived from: rating = 99 - 42*ln(1 + normalized)
    """
    if normalized <= 0:
        return 99
    raw = 99 - 42 * math.log(1 + normalized)
    return max(5, min(99, round(raw)))


def compute_league_medians(
    player_scores: dict[tuple, float]
) -> dict[str, float]:
    """
    Compute the median score per body-part key across players who cleared the
    MIN_SCORE threshold (exclude trivial Q-only spam from the median denominator).
    """
    by_part: dict[str, list[float]] = defaultdict(list)
    for (_, bpk), score in player_scores.items():
        if score >= MIN_SCORE:
            by_part[bpk].append(score)

    medians: dict[str, float] = {}
    for bpk, scores in by_part.items():
        scores_sorted = sorted(scores)
        n = len(scores_sorted)
        if n == 0:
            medians[bpk] = 1.0
        elif n % 2 == 1:
            medians[bpk] = scores_sorted[n // 2]
        else:
            medians[bpk] = (scores_sorted[n // 2 - 1] + scores_sorted[n // 2]) / 2

    return medians


def write_analysis_csv(
    all_episodes_by_part: dict[str, list[dict]],
    total_players: int
) -> None:
    rows = []
    total_ep = sum(len(v) for v in all_episodes_by_part.values())

    for bpk, eps in sorted(all_episodes_by_part.items()):
        if not eps:
            continue
        avg_dur = sum(e["duration_days"] for e in eps) / len(eps)
        pct_all = 100 * len(eps) / total_ep if total_ep else 0
        rows.append({
            "body_part_key":      bpk,
            "total_episodes":     len(eps),
            "avg_duration_days":  round(avg_dur, 1),
            "pct_of_all_episodes": round(pct_all, 2),
        })

    rows.sort(key=lambda r: -r["total_episodes"])

    with open(ANALYSIS_CSV, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()) if rows else [])
        w.writeheader()
        w.writerows(rows)

    print(f"\nSaved injury_type_analysis.csv ({len(rows)} body parts).")


def run(verbose: bool = True) -> None:
    conn = sqlite3.connect(DB_PATH)
    conn.executescript(SCHEMA_RATINGS)
    conn.commit()

    # Drop existing ratings to recompute cleanly
    conn.execute("DELETE FROM player_ratings")
    conn.commit()

    entries_by_key = load_entries(conn)
    if verbose:
        print(f"Loaded entries for {len(entries_by_key)} (player, body_part) pairs.")

    # Detect episodes and compute raw scores
    player_scores:    dict[tuple, float]       = {}
    episode_counts:   dict[tuple, int]         = {}
    total_days_map:   dict[tuple, int]         = {}
    all_eps_by_part:  dict[str, list[dict]]    = defaultdict(list)

    for (player, bpk), entries in entries_by_key.items():
        eps = detect_episodes(entries)
        score = score_episodes(eps)
        player_scores[(player, bpk)] = score
        episode_counts[(player, bpk)] = len(eps)
        total_days_map[(player, bpk)] = sum(e["duration_days"] for e in eps)
        all_eps_by_part[bpk].extend(eps)

    # Compute league medians (for normalization)
    medians = compute_league_medians(player_scores)
    if verbose:
        print(f"Computed medians for {len(medians)} body parts.")

    # Compute final ratings and insert
    for (player, bpk), score in player_scores.items():
        # Require MIN_SCORE — filters Q-only spam and single-day appearances
        if score < MIN_SCORE:
            normalized = 0.0
            rating = 99
        else:
            median = medians.get(bpk, 1.0)
            normalized = score / max(median, 0.001)
            rating = rating_from_normalized(normalized)

        conn.execute(
            """INSERT OR REPLACE INTO player_ratings
               (player_norm, body_part_key, num_episodes, total_days,
                weighted_score, normalized, final_rating)
               VALUES (?,?,?,?,?,?,?)""",
            (
                player, bpk,
                episode_counts.get((player, bpk), 0),
                total_days_map.get((player, bpk), 0),
                round(score, 4),
                round(normalized, 4),
                rating,
            ),
        )

    conn.commit()

    total_ratings = conn.execute("SELECT COUNT(*) FROM player_ratings").fetchone()[0]
    low_ratings   = conn.execute(
        "SELECT COUNT(*) FROM player_ratings WHERE final_rating < 50"
    ).fetchone()[0]

    if verbose:
        print(f"\nRatings computed: {total_ratings} rows "
              f"({low_ratings} below 50 — high-risk body parts).")

        # Show top 20 most fragile (player, body part) pairs
        top_fragile = conn.execute(
            """SELECT player_norm, body_part_key, num_episodes,
                      total_days, final_rating
               FROM player_ratings
               ORDER BY final_rating ASC
               LIMIT 20"""
        ).fetchall()
        print("\nTop 20 most fragile (player, body part):")
        print(f"  {'Player':<25} {'Body Part':<20} {'Ep':>3} {'Days':>5} {'Rating':>6}")
        print("  " + "-"*65)
        for row in top_fragile:
            print(f"  {row[0]:<25} {row[1]:<20} {row[2]:>3} {row[3]:>5} {row[4]:>6}")

    # Write CSV analysis
    write_analysis_csv(all_eps_by_part, total_players=len(
        {k[0] for k in player_scores}
    ))

    conn.close()


if __name__ == "__main__":
    run()
