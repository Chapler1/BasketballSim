from body_part_map import parse_reason, normalize_name

tests = [
    ("Injury/Illness - Left Shoulder; Rotator Cuff", "InjLShoulder"),
    ("Injury/Illness - Right Achilles; Tendinopathy", "InjRAchilles"),
    ("Injury/Illness - Left Hamstring; Strain",       "InjLHamstring"),
    ("Injury/Illness - Right Wrist; Sprain",          "InjRWrist"),
    ("Injury/Illness - Concussion",                   "InjHead"),
    ("Injury/Illness - Lower Back; Tightness",        "InjLowerBack"),
    ("Injury/Illness - Right Hip; Flexor Strain",     "InjRHip"),
    ("Injury/Illness - Left Knee; ACL Tear",          "InjLKnee"),
    ("Injury/Illness - Right Ankle; High Ankle Sprain","InjRAnkle"),
    ("Injury/Illness - Left Oblique; Strain",         "InjLOblique"),
    ("Injury/Illness - Right Quad; Contusion",        "InjRQuad"),
    ("Injury/Illness - Left Toe; Turf Toe",           "InjLToes"),
    ("Rest",                                          None),
    ("Illness - Flu",                                 None),
]

name_tests = [
    ("Brown, Jaylen",  "Jaylen Brown"),
    ("James, LeBron",  "LeBron James"),
    ("LeBron James",   "LeBron James"),
]

fails = 0
for reason, expected in tests:
    got = parse_reason(reason)
    ok = got == expected
    if not ok:
        fails += 1
    print(("OK  " if ok else "FAIL") + reason[:55].ljust(56) + " -> " + str(got))

print()
for raw, expected in name_tests:
    from body_part_map import normalize_name
    got = normalize_name(raw)
    ok = got == expected
    if not ok:
        fails += 1
    print(("OK  " if ok else "FAIL") + raw.ljust(20) + " -> " + got)

print()
print("All tests passed." if fails == 0 else str(fails) + " test(s) FAILED.")
