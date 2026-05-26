"""
Step 1: Download NBA injury reports (2021-22 through 2024-25) into SQLite.
Pure Python via pdfplumber — no Java required.
"""

import sqlite3
import time
import os
import threading
import requests
import pdfplumber
from io import BytesIO
from datetime import datetime
from concurrent.futures import ThreadPoolExecutor, as_completed

DB_PATH = os.path.join(os.path.dirname(__file__), "injuries.db")
SEASONS = ["2021-22", "2022-23", "2023-24", "2024-25"]

URL_STEM = "https://ak-static.cms.nba.com/referee/injury/Injury-Report_*.pdf"
HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                  "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
}
REPORT_HOURS = [20, 19, 21, 18, 17, 22]   # try latest-first for most complete report

SCHEMA = """
CREATE TABLE IF NOT EXISTS raw_reports (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    season      TEXT    NOT NULL,
    game_date   TEXT    NOT NULL,
    team        TEXT,
    player_raw  TEXT    NOT NULL,
    status      TEXT,
    reason      TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_report
    ON raw_reports(game_date, player_raw,
                   COALESCE(team,''), COALESCE(status,''), COALESCE(reason,''));

CREATE TABLE IF NOT EXISTS fetch_log (
    game_date  TEXT PRIMARY KEY,
    season     TEXT,
    status     TEXT,
    row_count  INT,
    fetched_at TEXT
);
"""

COL_KEYWORDS = {
    "Game Date":       ["date"],
    "Game Time":       ["time"],
    "Matchup":         ["matchup"],
    "Team":            ["team"],
    "Player Name":     ["player", "name"],
    "Current Status":  ["status", "current"],
    "Reason":          ["reason"],
}

EXPECTED_COLS = ["Game Date", "Game Time", "Matchup", "Team",
                 "Player Name", "Current Status", "Reason"]


# ─── URL generation ──────────────────────────────────────────────────────────

def gen_url(date_str: str, hour: int) -> str:
    dt = datetime(int(date_str[:4]), int(date_str[5:7]), int(date_str[8:10]), hour, 0)
    time_str = dt.strftime("%I%p")   # "06PM", "07PM", etc.
    return URL_STEM.replace("*", f"{date_str}_{time_str}")


def fetch_pdf(url: str) -> bytes | None:
    try:
        resp = requests.get(url, headers=HEADERS, timeout=20)
        if resp.status_code == 200 and len(resp.content) > 5000:
            return resp.content
    except Exception:
        pass
    return None


# ─── PDF parsing ─────────────────────────────────────────────────────────────

def _words_by_y(page, y_tol: float = 3.5) -> dict[int, list[dict]]:
    """Group page words into horizontal lines by y-coordinate."""
    words = page.extract_words(x_tolerance=2, y_tolerance=y_tol,
                               keep_blank_chars=False, use_text_flow=False)
    by_y: dict[int, list[dict]] = {}
    for w in words:
        bucket = round(w["top"] / y_tol) * round(y_tol)
        if bucket not in by_y:
            by_y[bucket] = []
        by_y[bucket].append(w)
    return by_y


def _detect_col_bounds(header_words: list[dict], page_width: float) -> list[tuple[str, float, float]]:
    """
    Given words from the header row, return [(col_name, x_start, x_end), ...].
    Columns are identified by keyword matching on header text, then bounded by midpoints
    between adjacent columns.
    """
    assigned: list[tuple[str, float]] = []  # (col_name, x0)

    merged_text = " ".join(w["text"] for w in header_words).lower()

    # Try single-pass: each header word → column
    for w in header_words:
        t = w["text"].lower().strip()
        for col, kws in COL_KEYWORDS.items():
            if any(kw in t for kw in kws):
                # Avoid overwriting a closer-to-left assignment
                if not any(c == col for c, _ in assigned):
                    assigned.append((col, w["x0"]))
                break

    # If a column wasn't matched, fall back to positional defaults
    assigned.sort(key=lambda x: x[1])
    found_cols = [c for c, _ in assigned]
    for col in EXPECTED_COLS:
        if col not in found_cols:
            pass  # missing columns get skipped — forward-fill handles them

    # Build (name, x_start, x_end) triples
    result: list[tuple[str, float, float]] = []
    for i, (col, x0) in enumerate(assigned):
        x1 = assigned[i + 1][1] if i + 1 < len(assigned) else page_width + 100
        result.append((col, x0, x1))

    return result


def _assign_word(x: float, col_bounds: list[tuple[str, float, float]]) -> str | None:
    for col, x0, x1 in col_bounds:
        if x0 - 5 <= x < x1 - 5:
            return col
    return None


def _parse_pdf(pdf_bytes: bytes) -> list[dict]:
    """
    Extract injury report rows from a PDF using word-coordinate bucketing.
    Returns list of dicts with keys matching EXPECTED_COLS.
    """
    col_bounds: list[tuple[str, float, float]] | None = None
    raw_lines: list[dict] = []   # each dict: {col_name: text, ...}

    with pdfplumber.open(BytesIO(pdf_bytes)) as pdf:
        for page in pdf.pages:
            by_y = _words_by_y(page)

            for y_key in sorted(by_y.keys()):
                words = by_y[y_key]
                words.sort(key=lambda w: w["x0"])

                combined = " ".join(w["text"] for w in words).lower()

                # Detect header row
                if col_bounds is None and "reason" in combined and (
                        "player" in combined or "status" in combined):
                    col_bounds = _detect_col_bounds(words, page.width)
                    continue

                if col_bounds is None:
                    continue

                # Skip page-footer lines (page numbers)
                if len(words) <= 2 and any("page" in w["text"].lower() for w in words):
                    continue

                # Skip report-title lines ("Injury Report: 01/15/24 08:30 PM")
                combined_text = " ".join(w["text"] for w in words).lower()
                if "report:" in combined_text:
                    continue

                # Assign words to columns
                line: dict[str, list[str]] = {}
                for w in words:
                    col = _assign_word(w["x0"], col_bounds)
                    if col:
                        line.setdefault(col, []).append(w["text"])

                if not line:
                    continue

                # Collapse each column's word list to a string
                row = {col: " ".join(parts) for col, parts in line.items()}
                raw_lines.append(row)

    return raw_lines


def _build_records(raw_lines: list[dict]) -> list[dict]:
    """
    Forward-fill sparse columns and merge multiline reason fragments.
    Returns clean records ready for insertion.
    """
    # ── pass 1: forward-fill game date / time / matchup / team ──────────────
    last = {col: "" for col in EXPECTED_COLS}
    padded: list[dict] = []
    for line in raw_lines:
        for col in ["Game Date", "Game Time", "Matchup", "Team"]:
            if line.get(col, "").strip():
                last[col] = line[col].strip()
        row = {**last, **{k: v for k, v in line.items()
                          if k not in ("Game Date", "Game Time", "Matchup", "Team")}}
        padded.append(row)

    # ── pass 2: merge multiline reason fragments ─────────────────────────────
    # Orphan reason lines (no player, no status) may appear BEFORE and/or AFTER
    # the player row they belong to. The player row itself has no reason when split.
    # Use a used[] array so backward and forward scans don't steal fragments.
    n = len(padded)
    used = [False] * n
    merged: list[dict] = []

    for i in range(n):
        if used[i]:
            continue

        row = padded[i]
        player = row.get("Player Name", "").strip()
        status = row.get("Current Status", "").strip()
        reason = row.get("Reason", "").strip()

        is_orphan = not player and not status
        if is_orphan:
            continue  # handled when processing adjacent player row

        if not player or not status:
            used[i] = True
            continue

        used[i] = True

        # Scan backwards for pre-orphan reason fragments
        pre: list[str] = []
        j = i - 1
        while j >= 0 and not used[j]:
            prev = padded[j]
            if (not prev.get("Player Name", "").strip() and
                    not prev.get("Current Status", "").strip() and
                    prev.get("Reason", "").strip()):
                pre.insert(0, prev["Reason"].strip())
                used[j] = True
                j -= 1
            else:
                break

        # Scan forwards for post-orphan reason fragments (only when player has no reason).
        # Stop if the orphan starts a NEW reason ("Injury/Illness" / "G League") —
        # that fragment belongs to the NEXT player as their pre-orphan.
        post: list[str] = []
        if not reason:
            j = i + 1
            while j < n and not used[j]:
                nxt = padded[j]
                nxt_player = nxt.get("Player Name", "").strip()
                nxt_status = nxt.get("Current Status", "").strip()
                nxt_reason = nxt.get("Reason", "").strip()
                if not nxt_player and not nxt_status and nxt_reason:
                    r_lower = nxt_reason.lower()
                    if "injury/illness" in r_lower or r_lower.startswith("g league"):
                        break   # next player's pre-orphan — stop here
                    post.append(nxt_reason)
                    used[j] = True
                    j += 1
                else:
                    break

        full_reason = " ".join(pre + ([reason] if reason else []) + post).strip()
        row = dict(row)
        row["Reason"] = full_reason
        merged.append(row)

    # ── pass 3: emit valid records ────────────────────────────────────────────
    records = []
    for row in merged:
        player = row.get("Player Name", "").strip()
        status = row.get("Current Status", "").strip()
        reason = row.get("Reason", "").strip()

        if not player or not status:
            continue
        if "not yet submitted" in reason.lower():
            continue

        records.append({
            "game_date": row.get("Game Date", "").strip(),
            "team":      row.get("Team", "").strip(),
            "player_raw": player,
            "status":    status,
            "reason":    reason,
        })

    return records


def parse_pdf(pdf_bytes: bytes) -> list[dict]:
    """Public entry point: parse PDF bytes → list of injury report records."""
    raw = _parse_pdf(pdf_bytes)
    return _build_records(raw)


# ─── Database helpers ─────────────────────────────────────────────────────────

def init_db(conn: sqlite3.Connection) -> None:
    conn.executescript(SCHEMA)
    conn.commit()


def get_unfetched_dates(conn: sqlite3.Connection, season: str,
                        all_dates: list[str]) -> list[str]:
    already = {row[0] for row in conn.execute(
        "SELECT game_date FROM fetch_log WHERE season=?", (season,)
    )}
    return [d for d in all_dates if d not in already]


def fetch_season_dates(season: str) -> list[str]:
    try:
        from nba_api.stats.endpoints import LeagueGameFinder
        print(f"  Fetching schedule for {season} via nba_api...", flush=True)
        finder = LeagueGameFinder(season_nullable=season)
        df = finder.get_data_frames()[0]
        dates = sorted(df["GAME_DATE"].unique().tolist())
        print(f"  Found {len(dates)} unique game dates.", flush=True)
        return dates
    except Exception as e:
        print(f"  ERROR fetching schedule for {season}: {e}", flush=True)
        return []


def fetch_date(date_str: str) -> list[dict] | None:
    """Try multiple report hours; return records from the first valid PDF."""
    for hour in REPORT_HOURS:
        url = gen_url(date_str, hour)
        pdf_bytes = fetch_pdf(url)
        if pdf_bytes:
            records = parse_pdf(pdf_bytes)
            if records:
                return records
    return None


def insert_rows(conn: sqlite3.Connection, season: str, game_date: str,
                records: list[dict]) -> int:
    inserted = 0
    for rec in records:
        status = rec.get("status", "").strip()
        if status.lower() == "available":
            continue
        try:
            conn.execute(
                """INSERT OR IGNORE INTO raw_reports
                   (season, game_date, team, player_raw, status, reason)
                   VALUES (?, ?, ?, ?, ?, ?)""",
                (season, game_date,
                 rec.get("team", ""), rec.get("player_raw", ""),
                 status, rec.get("reason", "")),
            )
            inserted += conn.execute("SELECT changes()").fetchone()[0]
        except Exception:
            pass
    conn.commit()
    return inserted


# ─── Main runner ──────────────────────────────────────────────────────────────

WORKERS = 1       # serial to avoid CDN rate-limits
RATE_DELAY = 1.2  # seconds between requests

_db_lock = threading.Lock()


def _fetch_and_store(args: tuple) -> tuple[str, str, int]:
    """Worker: fetch one date, return (date_str, status, row_count)."""
    date_str, season, db_path = args
    records = fetch_date(date_str)
    time.sleep(RATE_DELAY)

    if records:
        log_status = "ok"
    else:
        records = []
        log_status = "empty"

    n = 0
    with _db_lock:
        conn = sqlite3.connect(db_path)
        if records:
            n = insert_rows(conn, season, date_str, records)
        conn.execute(
            "INSERT OR REPLACE INTO fetch_log"
            "(game_date, season, status, row_count, fetched_at)"
            " VALUES(?,?,?,?,?)",
            (date_str, season, log_status, n, datetime.now().isoformat()),
        )
        conn.commit()
        conn.close()

    return (date_str, log_status, n)


def run(verbose: bool = True) -> None:
    conn = sqlite3.connect(DB_PATH)
    init_db(conn)

    total_new = 0
    for season in SEASONS:
        all_dates = fetch_season_dates(season)
        if not all_dates:
            continue

        to_fetch = get_unfetched_dates(conn, season, all_dates)
        conn.close()   # release main conn before parallel work

        if not to_fetch:
            if verbose:
                print(f"[{season}] All {len(all_dates)} dates already fetched — skipping.")
            conn = sqlite3.connect(DB_PATH)
            continue

        if verbose:
            print(f"[{season}] Fetching {len(to_fetch)} dates "
                  f"(skipping {len(all_dates)-len(to_fetch)} cached)...", flush=True)

        season_new = 0
        done = 0
        args = [(d, season, DB_PATH) for d in to_fetch]

        with ThreadPoolExecutor(max_workers=WORKERS) as pool:
            futs = {pool.submit(_fetch_and_store, a): a[0] for a in args}
            for fut in as_completed(futs):
                date_str, status, n = fut.result()
                season_new += n
                done += 1
                if verbose and done % 50 == 0:
                    print(f"  [{season}] {done}/{len(to_fetch)} done, "
                          f"{season_new} new rows...", flush=True)

        total_new += season_new
        conn = sqlite3.connect(DB_PATH)

    row_count = conn.execute("SELECT COUNT(*) FROM raw_reports").fetchone()[0]
    print(f"\nFetch complete. raw_reports: {row_count} total rows, {total_new} new.")
    conn.close()


if __name__ == "__main__":
    run()
