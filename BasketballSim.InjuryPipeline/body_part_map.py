"""
Maps NBA injury report reason text to the 40 body-part keys used in InjuryTables.BodyParts.

Key format: Inj{Side?}{BodyPartType}
  - Bilateral parts: InjLKnee, InjRKnee, InjLAnkle, InjRAnkle, ...
  - Single parts:    InjLowerBack, InjHead, InjNeck, InjChest, InjUpperBack, InjAbdominals

"upper back" must come before "back" to avoid mis-mapping.
"""

# Each entry: (keyword_list, body_part_type, is_bilateral)
# Checked in order — first match wins.
KEYWORD_MAP = [
    # ── Upper body — bilateral ────────────────────────────────────────────────
    (["shoulder", "rotator", "labrum", "ac joint", "acromioclavicular"], "Shoulder",  True),
    (["upper arm", "bicep", "tricep", "humerus"],                         "UpperArm",  True),
    (["elbow", "ulnar", "olecranon", "ucl"],                              "Elbow",     True),
    (["forearm", "radius", "ulna"],                                       "Forearm",   True),
    (["wrist", "scaphoid", "carpal"],                                     "Wrist",     True),
    (["finger", "thumb", "phalang", "metacarpal"],                        "Fingers",   True),
    (["hand", "palm"],                                                    "Hand",      True),

    # ── Core — single ─────────────────────────────────────────────────────────
    (["upper back", "thoracic", "thorac"],                                "UpperBack", False),
    (["lower back", "lumbar", "lumbosacral", "disc", "l-spine"],          "LowerBack", False),
    (["back", "spine", "spinal", "vertebra"],                             "LowerBack", False),  # generic "back" → lower back
    (["oblique"],                                                         "Oblique",   True),
    (["abdom", "hernia", "core"],                                          "Abdominals",False),
    (["rib", "chest", "pec", "pectoral", "sternum", "costochondral"],     "Chest",     False),
    (["neck", "cervical", "stinger"],                                     "Neck",      False),
    (["head", "face", "nose", "concussion", "eye", "orbital",
      "laceration", "facial", "jaw"],                                     "Head",      False),

    # ── Lower body — bilateral ────────────────────────────────────────────────
    (["hip", "groin", "adductor", "iliopsoas", "hip flexor",
      "labrum tear", "iliac"],                                             "Hip",       True),
    (["hamstring"],                                                        "Hamstring", True),
    (["quad", "quadricep", "thigh"],                                       "Quad",      True),
    (["acl", "pcl", "mcl", "lcl", "meniscus", "patellar",
      "knee", "patella", "femoral condyle"],                               "Knee",      True),
    (["shin", "calf", "gastrocnemius", "soleus", "fibular", "fibula", "tibia", "lower leg", "leg; contusion"],  "ShinCalf",  True),
    (["achilles"],                                                         "Achilles",  True),
    (["ankle", "peroneal", "high ankle", "syndesmosis"],                  "Ankle",     True),
    (["plantar", "heel", "foot", "metatarsal", "navicular",
      "lisfranc", "sesamoid"],                                             "Foot",      True),
    (["toe", "bunion", "turf toe"],                                       "Toes",      True),
]

# Valid body-part keys from InjuryTables.BodyParts (for validation)
VALID_KEYS = {
    "InjHead", "InjNeck",
    "InjLShoulder", "InjRShoulder", "InjLUpperArm", "InjRUpperArm",
    "InjLElbow", "InjRElbow", "InjLForearm", "InjRForearm",
    "InjLWrist", "InjRWrist", "InjLHand", "InjRHand",
    "InjLFingers", "InjRFingers",
    "InjChest", "InjUpperBack", "InjAbdominals", "InjLowerBack",
    "InjLOblique", "InjROblique",
    "InjLHip", "InjRHip", "InjLHamstring", "InjRHamstring",
    "InjLQuad", "InjRQuad", "InjLKnee", "InjRKnee",
    "InjLShinCalf", "InjRShinCalf", "InjLAchilles", "InjRAchilles",
    "InjLAnkle", "InjRAnkle", "InjLFoot", "InjRFoot",
    "InjLToes", "InjRToes",
}


def parse_reason(reason: str) -> str | None:
    """
    Parse an NBA injury report reason string and return a body-part key.

    Examples:
        "Injury/Illness - Left Knee; Soreness"     → "InjLKnee"
        "Injury/Illness - Right Ankle; Sprain"     → "InjRAnkle"
        "Injury/Illness - Back; Spasms"            → "InjLowerBack"
        "Injury/Illness - Left Shoulder; Strain"   → "InjLShoulder"
        "Rest"                                      → None
    """
    if not reason:
        return None

    r = reason.lower()

    # Skip non-injury entries
    if any(skip in r for skip in ["rest", "illness", "flu", "covid", "not injury"]):
        if "injury" not in r:
            return None

    side = "L" if "left" in r else ("R" if "right" in r else None)

    for keywords, part_type, bilateral in KEYWORD_MAP:
        if any(kw in r for kw in keywords):
            if bilateral:
                # Use detected side; default to L when ambiguous
                s = side if side else "L"
                key = f"Inj{s}{part_type}"
            else:
                key = f"Inj{part_type}"
            return key if key in VALID_KEYS else None

    return None


def normalize_name(raw: str) -> str:
    """
    Normalize NBA API name format to "First Last".
    Input:  "Brown, Jaylen" or "Jaylen Brown"
    Output: "Jaylen Brown"
    """
    raw = raw.strip()
    if ", " in raw:
        parts = raw.split(", ", 1)
        return f"{parts[1]} {parts[0]}"
    return raw
