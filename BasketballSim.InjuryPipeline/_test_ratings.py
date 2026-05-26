from analyze import rating_from_normalized

# normalized=0 (no injuries) → 99
# normalized=1 (league avg)  → ~70
# normalized=2 (2x avg)      → ~52
# normalized=4 (4x avg)      → ~30
# normalized=8+ (Embiid)     → ~5-15

cases = [
    (0.0,   99,  None),
    (1.0,   None,(65, 74)),   # league avg -> ~70
    (2.0,   None,(48, 57)),   # 2x avg     -> ~53
    (4.0,   None,(26, 36)),   # 4x avg     -> ~31
    (8.0,   None,(5,  15)),   # Embiid-level
    (12.0,  None,(5,  12)),
]

all_ok = True
for normalized, exact, rng in cases:
    got = rating_from_normalized(normalized)
    if exact is not None:
        ok = got == exact
    else:
        ok = rng[0] <= got <= rng[1]
    if not ok:
        all_ok = False
    label = ("OK  " if ok else "FAIL")
    rng_str = f"exact={exact}" if exact is not None else f"range={rng}"
    print(f"{label} normalized={normalized:.1f} -> rating={got}  ({rng_str})")

print()
print("All tests passed." if all_ok else "Some tests FAILED.")
