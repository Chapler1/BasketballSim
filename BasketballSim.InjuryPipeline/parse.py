"""
Step 2: Parse raw_reports → parsed_entries.

For each row in raw_reports:
  - Normalize player name ("Brown, Jaylen" → "Jaylen Brown")
  - Parse reason text → body_part_key via body_part_map.KEYWORD_MAP
  - Insert into parsed_entries (skips rows that can't be mapped)

Also produces an unparsed_reasons report so you can improve the keyword map.
"""

import sqlite3
import os
from collections import Counter

from body_part_map import parse_reason, normalize_name

DB_PATH = os.path.join(os.path.dirname(__file__), "injuries.db")

SCHEMA_PARSED = """
CREATE TABLE IF NOT EXISTS parsed_entries (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    raw_id          INTEGER NOT NULL,
    player_norm     TEXT    NOT NULL,
    season          TEXT    NOT NULL,
    game_date       TEXT    NOT NULL,
    body_part_key   TEXT    NOT NULL,
    status          TEXT,
    reason          TEXT,
    FOREIGN KEY(raw_id) REFERENCES raw_reports(id)
);
CREATE INDEX IF NOT EXISTS idx_parsed_player ON parsed_entries(player_norm, body_part_key);
CREATE UNIQUE INDEX IF NOT EXISTS idx_parsed_unique ON parsed_entries(raw_id, body_part_key);
"""


def run(verbose: bool = True) -> None:
    conn = sqlite3.connect(DB_PATH)
    conn.executescript(SCHEMA_PARSED)
    conn.commit()

    # Fetch all raw rows (only Out/Questionable matter)
    rows = conn.execute(
        """SELECT id, season, game_date, player_raw, status, reason
           FROM raw_reports
           WHERE status IN ('Out','Questionable','Probable','Doubtful')
             AND player_raw != ''"""
    ).fetchall()

    if verbose:
        print(f"Parsing {len(rows)} raw rows...")

    inserted = 0
    skipped_no_map = 0
    unparsed: Counter = Counter()

    for raw_id, season, game_date, player_raw, status, reason in rows:
        body_part_key = parse_reason(reason or "")
        if body_part_key is None:
            unparsed[reason] += 1
            skipped_no_map += 1
            continue

        player_norm = normalize_name(player_raw)

        try:
            conn.execute(
                """INSERT OR IGNORE INTO parsed_entries
                   (raw_id, player_norm, season, game_date, body_part_key, status, reason)
                   VALUES (?,?,?,?,?,?,?)""",
                (raw_id, player_norm, season, game_date, body_part_key, status, reason),
            )
            inserted += conn.execute("SELECT changes()").fetchone()[0]
        except Exception:
            pass

    conn.commit()

    if verbose:
        total_parsed = conn.execute("SELECT COUNT(*) FROM parsed_entries").fetchone()[0]
        print(f"Parsed {inserted} new entries; parsed_entries now has {total_parsed} rows.")
        print(f"Skipped (no body-part match): {skipped_no_map}")

        # Show top unparsed reasons for keyword map improvement
        if unparsed:
            print("\nTop 20 unparsed reason strings (improve body_part_map.py if significant):")
            for reason, count in unparsed.most_common(20):
                print(f"  {count:5d}x  {reason!r:.100s}")

        # Distribution of body-part keys
        dist = conn.execute(
            "SELECT body_part_key, COUNT(*) as n FROM parsed_entries "
            "GROUP BY body_part_key ORDER BY n DESC"
        ).fetchall()
        print("\nBody-part distribution in parsed_entries:")
        for key, n in dist[:20]:
            print(f"  {key:<20s}  {n:6d}")

    conn.close()


if __name__ == "__main__":
    run()
