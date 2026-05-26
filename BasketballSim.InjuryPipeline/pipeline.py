"""
NBA Injury History Pipeline — orchestrator.

Usage:
    python pipeline.py                        # run all 4 steps
    python pipeline.py --step fetch           # re-download only
    python pipeline.py --step parse           # re-parse raw → body parts only
    python pipeline.py --step analyze         # recompute ratings from existing DB
    python pipeline.py --step inject          # write ratings into players.json
    python pipeline.py --step inject --dry-run
    python pipeline.py --step free_agents     # add current free agents to players.json

Steps:
    1. fetch        — Download NBA injury reports 2021-22 to 2024-25 → injuries.db
    2. parse        — Map reason text to 40 body-part keys → parsed_entries table
    3. analyze      — Detect episodes, compute ratings, save injury_type_analysis.csv
    4. inject       — Write ratings < 99 into BasketballSim/Data/players.json
    5. free_agents  — Fetch current NBA free agents (nba_api) and add to players.json

Requirements: Java 8+ (for nbainjuries/tabula-py), Python 3.10+
Install:      pip install -r requirements.txt
"""

import sys
import time


def main() -> None:
    args = sys.argv[1:]
    step_filter = None
    dry_run     = "--dry-run" in args

    for i, arg in enumerate(args):
        if arg == "--step" and i + 1 < len(args):
            step_filter = args[i + 1].lower()
            break

    steps = ["fetch", "parse", "analyze", "inject", "free_agents"]

    if step_filter and step_filter not in steps:
        print(f"Unknown step '{step_filter}'. Choose from: {', '.join(steps)}")
        sys.exit(1)

    def should_run(s: str) -> bool:
        return step_filter is None or step_filter == s

    # ── Step 1: Fetch ─────────────────────────────────────────────────────────
    if should_run("fetch"):
        print("=" * 60)
        print("STEP 1: FETCH — Downloading injury reports from NBA.com")
        print("=" * 60)
        print("Note: requires Java 8+ for nbainjuries/tabula-py PDF parsing.")
        print("This step makes ~5000 HTTP requests and may take 30-60 minutes.")
        print("It is safe to interrupt and re-run — already-fetched dates are cached.\n")
        t0 = time.time()
        import fetch
        fetch.run()
        print(f"\nFetch completed in {(time.time()-t0)/60:.1f} minutes.\n")

    # ── Step 2: Parse ─────────────────────────────────────────────────────────
    if should_run("parse"):
        print("=" * 60)
        print("STEP 2: PARSE — Mapping reason text to body-part keys")
        print("=" * 60)
        t0 = time.time()
        import parse
        parse.run()
        print(f"\nParse completed in {time.time()-t0:.1f}s.\n")

    # ── Step 3: Analyze ───────────────────────────────────────────────────────
    if should_run("analyze"):
        print("=" * 60)
        print("STEP 3: ANALYZE — Computing league baselines and player ratings")
        print("=" * 60)
        t0 = time.time()
        import analyze
        analyze.run()
        print(f"\nAnalysis completed in {time.time()-t0:.1f}s.\n")

    # ── Step 4: Inject ────────────────────────────────────────────────────────
    if should_run("inject"):
        print("=" * 60)
        print(f"STEP 4: INJECT — Writing ratings to players.json"
              + (" (DRY RUN)" if dry_run else ""))
        print("=" * 60)
        t0 = time.time()
        import inject
        inject.run(dry_run=dry_run)
        print(f"\nInjection completed in {time.time()-t0:.1f}s.\n")

    # ── Step 5: Free Agents (optional, run separately) ────────────────────────
    if should_run("free_agents"):
        print("=" * 60)
        print("STEP 5: FREE AGENTS — Fetching current NBA free agents")
        print("=" * 60)
        t0 = time.time()
        import free_agents
        free_agents.run()
        print(f"\nFree agent step completed in {time.time()-t0:.1f}s.\n")

    print("Pipeline done.")


if __name__ == "__main__":
    main()
