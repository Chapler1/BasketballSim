using BasketballSim.Models;

namespace BasketballSim.Data;

// Height scale: (inches - 69) * 5 + 5  →  5'9"=5, 6'1"=25, 6'7"=55 (NBA avg), 7'0"=80, 7'1"=85, 7'3"=95
// All ratings: 5 = worst in league, 95 = best in league

public static class RosterData
{
    public static Team Knicks { get; } = new Team
    {
        Name = "New York Knicks",
        Abbreviation = "NYK",
        PrimaryColor = "#006BB6",
        SecondaryColor = "#F58426",
        Pace = 97.4,
        Roster =
        [
            // ── Starters ─────────────────────────────────────────────────────
            new Player {
                Name="Jalen Brunson", Team="New York Knicks", JerseyNumber=11, Position=Position.PG, Age=27,
                Height=25, Strength=58, Speed=82, Jumping=38, Endurance=90,
                Attr_Inside=82, Attr_Dunks=12, Attr_FreeThrow=90, Attr_MidRange=92, Attr_ThreePoint=80,
                Attr_BasketballIQ=95, Attr_Dribbling=92, Attr_Passing=82,
                Attr_Rebounding_Off=15, Attr_Rebounding_Def=22,
                Attr_PerimeterDefense=42, Attr_InteriorDefense=18
            },
            new Player {
                Name="Mikal Bridges", Team="New York Knicks", JerseyNumber=25, Position=Position.SG, Age=27,
                Height=50, Strength=65, Speed=72, Jumping=65, Endurance=93,
                Attr_Inside=62, Attr_Dunks=42, Attr_FreeThrow=80, Attr_MidRange=72, Attr_ThreePoint=75,
                Attr_BasketballIQ=80, Attr_Dribbling=68, Attr_Passing=58,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=48,
                Attr_PerimeterDefense=90, Attr_InteriorDefense=55
            },
            new Player {
                Name="Josh Hart", Team="New York Knicks", JerseyNumber=3, Position=Position.SF, Age=29,
                Height=45, Strength=82, Speed=70, Jumping=62, Endurance=95,
                Attr_Inside=65, Attr_Dunks=42, Attr_FreeThrow=60, Attr_MidRange=52, Attr_ThreePoint=50,
                Attr_BasketballIQ=72, Attr_Dribbling=55, Attr_Passing=62,
                Attr_Rebounding_Off=78, Attr_Rebounding_Def=72,
                Attr_PerimeterDefense=78, Attr_InteriorDefense=50
            },
            new Player {
                Name="OG Anunoby", Team="New York Knicks", JerseyNumber=8, Position=Position.PF, Age=26,
                Height=55, Strength=78, Speed=75, Jumping=70, Endurance=82,
                Attr_Inside=68, Attr_Dunks=58, Attr_FreeThrow=72, Attr_MidRange=58, Attr_ThreePoint=75,
                Attr_BasketballIQ=78, Attr_Dribbling=58, Attr_Passing=55,
                Attr_Rebounding_Off=42, Attr_Rebounding_Def=60,
                Attr_PerimeterDefense=95, Attr_InteriorDefense=72
            },
            new Player {
                Name="Karl-Anthony Towns", Team="New York Knicks", JerseyNumber=32, Position=Position.C, Age=28,
                Height=80, Strength=78, Speed=48, Jumping=60, Endurance=75,
                Attr_Inside=88, Attr_Dunks=70, Attr_FreeThrow=88, Attr_MidRange=78, Attr_ThreePoint=85,
                Attr_BasketballIQ=80, Attr_Dribbling=55, Attr_Passing=62,
                Attr_Rebounding_Off=70, Attr_Rebounding_Def=75,
                Attr_PerimeterDefense=32, Attr_InteriorDefense=65
            },
            // ── Bench ─────────────────────────────────────────────────────────
            new Player {
                Name="Donte DiVincenzo", Team="New York Knicks", JerseyNumber=0, Position=Position.SG, Age=27,
                Height=40, Strength=62, Speed=75, Jumping=62, Endurance=82,
                Attr_Inside=55, Attr_Dunks=35, Attr_FreeThrow=82, Attr_MidRange=60, Attr_ThreePoint=78,
                Attr_BasketballIQ=72, Attr_Dribbling=65, Attr_Passing=60,
                Attr_Rebounding_Off=32, Attr_Rebounding_Def=40,
                Attr_PerimeterDefense=72, Attr_InteriorDefense=35
            },
            new Player {
                Name="Julius Randle", Team="New York Knicks", JerseyNumber=30, Position=Position.PF, Age=29,
                Height=60, Strength=90, Speed=62, Jumping=60, Endurance=80,
                Attr_Inside=82, Attr_Dunks=58, Attr_FreeThrow=75, Attr_MidRange=78, Attr_ThreePoint=55,
                Attr_BasketballIQ=72, Attr_Dribbling=72, Attr_Passing=70,
                Attr_Rebounding_Off=62, Attr_Rebounding_Def=72,
                Attr_PerimeterDefense=40, Attr_InteriorDefense=60
            },
            new Player {
                Name="Isaiah Hartenstein", Team="New York Knicks", JerseyNumber=55, Position=Position.C, Age=26,
                Height=80, Strength=80, Speed=42, Jumping=55, Endurance=80,
                Attr_Inside=72, Attr_Dunks=60, Attr_FreeThrow=68, Attr_MidRange=42, Attr_ThreePoint=12,
                Attr_BasketballIQ=82, Attr_Dribbling=38, Attr_Passing=72,
                Attr_Rebounding_Off=68, Attr_Rebounding_Def=82,
                Attr_PerimeterDefense=38, Attr_InteriorDefense=82
            },
            new Player {
                Name="Miles McBride", Team="New York Knicks", JerseyNumber=2, Position=Position.PG, Age=23,
                Height=30, Strength=60, Speed=78, Jumping=58, Endurance=80,
                Attr_Inside=55, Attr_Dunks=25, Attr_FreeThrow=78, Attr_MidRange=60, Attr_ThreePoint=65,
                Attr_BasketballIQ=70, Attr_Dribbling=72, Attr_Passing=60,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=30,
                Attr_PerimeterDefense=70, Attr_InteriorDefense=25
            },
            new Player {
                Name="Precious Achiuwa", Team="New York Knicks", JerseyNumber=5, Position=Position.PF, Age=24,
                Height=60, Strength=82, Speed=70, Jumping=80, Endurance=80,
                Attr_Inside=62, Attr_Dunks=68, Attr_FreeThrow=52, Attr_MidRange=40, Attr_ThreePoint=28,
                Attr_BasketballIQ=60, Attr_Dribbling=42, Attr_Passing=38,
                Attr_Rebounding_Off=72, Attr_Rebounding_Def=68,
                Attr_PerimeterDefense=52, Attr_InteriorDefense=68
            },
            new Player {
                Name="Landry Shamet", Team="New York Knicks", JerseyNumber=14, Position=Position.SG, Age=26,
                Height=40, Strength=48, Speed=65, Jumping=48, Endurance=72,
                Attr_Inside=38, Attr_Dunks=12, Attr_FreeThrow=85, Attr_MidRange=62, Attr_ThreePoint=80,
                Attr_BasketballIQ=72, Attr_Dribbling=60, Attr_Passing=55,
                Attr_Rebounding_Off=18, Attr_Rebounding_Def=28,
                Attr_PerimeterDefense=58, Attr_InteriorDefense=20
            },
            new Player {
                Name="Deuce McBride", Team="New York Knicks", JerseyNumber=8, Position=Position.PG, Age=22,
                Height=30, Strength=58, Speed=76, Jumping=58, Endurance=78,
                Attr_Inside=50, Attr_Dunks=22, Attr_FreeThrow=72, Attr_MidRange=55, Attr_ThreePoint=62,
                Attr_BasketballIQ=68, Attr_Dribbling=70, Attr_Passing=58,
                Attr_Rebounding_Off=20, Attr_Rebounding_Def=28,
                Attr_PerimeterDefense=65, Attr_InteriorDefense=22
            },
        ]
    };

    public static Team Thunder { get; } = new Team
    {
        Name = "Oklahoma City Thunder",
        Abbreviation = "OKC",
        PrimaryColor = "#007AC1",
        SecondaryColor = "#EF3B24",
        Pace = 100.2,
        Roster =
        [
            // ── Starters ─────────────────────────────────────────────────────
            new Player {
                Name="Shai Gilgeous-Alexander", Team="Oklahoma City Thunder", JerseyNumber=2, Position=Position.PG, Age=25,
                Height=50, Strength=68, Speed=90, Jumping=72, Endurance=92,
                Attr_Inside=90, Attr_Dunks=55, Attr_FreeThrow=90, Attr_MidRange=92, Attr_ThreePoint=72,
                Attr_BasketballIQ=95, Attr_Dribbling=95, Attr_Passing=82,
                Attr_Rebounding_Off=28, Attr_Rebounding_Def=40,
                Attr_PerimeterDefense=80, Attr_InteriorDefense=40
            },
            new Player {
                Name="Luguentz Dort", Team="Oklahoma City Thunder", JerseyNumber=5, Position=Position.SG, Age=25,
                Height=40, Strength=85, Speed=78, Jumping=70, Endurance=88,
                Attr_Inside=60, Attr_Dunks=48, Attr_FreeThrow=68, Attr_MidRange=58, Attr_ThreePoint=68,
                Attr_BasketballIQ=70, Attr_Dribbling=62, Attr_Passing=45,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=45,
                Attr_PerimeterDefense=95, Attr_InteriorDefense=62
            },
            new Player {
                Name="Jalen Williams", Team="Oklahoma City Thunder", JerseyNumber=8, Position=Position.SF, Age=22,
                Height=50, Strength=72, Speed=80, Jumping=70, Endurance=85,
                Attr_Inside=80, Attr_Dunks=60, Attr_FreeThrow=87, Attr_MidRange=82, Attr_ThreePoint=75,
                Attr_BasketballIQ=86, Attr_Dribbling=82, Attr_Passing=70,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=42,
                Attr_PerimeterDefense=70, Attr_InteriorDefense=45
            },
            new Player {
                Name="Chet Holmgren", Team="Oklahoma City Thunder", JerseyNumber=7, Position=Position.PF, Age=22,
                Height=85, Strength=30, Speed=65, Jumping=80, Endurance=72,
                Attr_Inside=72, Attr_Dunks=68, Attr_FreeThrow=82, Attr_MidRange=75, Attr_ThreePoint=78,
                Attr_BasketballIQ=82, Attr_Dribbling=50, Attr_Passing=65,
                Attr_Rebounding_Off=50, Attr_Rebounding_Def=72,
                Attr_PerimeterDefense=55, Attr_InteriorDefense=92
            },
            new Player {
                Name="Isaiah Hartenstein", Team="Oklahoma City Thunder", JerseyNumber=55, Position=Position.C, Age=26,
                Height=80, Strength=80, Speed=42, Jumping=55, Endurance=80,
                Attr_Inside=72, Attr_Dunks=60, Attr_FreeThrow=68, Attr_MidRange=42, Attr_ThreePoint=12,
                Attr_BasketballIQ=82, Attr_Dribbling=38, Attr_Passing=72,
                Attr_Rebounding_Off=68, Attr_Rebounding_Def=82,
                Attr_PerimeterDefense=38, Attr_InteriorDefense=82
            },
            // ── Bench ─────────────────────────────────────────────────────────
            new Player {
                Name="Aaron Wiggins", Team="Oklahoma City Thunder", JerseyNumber=21, Position=Position.SG, Age=24,
                Height=45, Strength=65, Speed=75, Jumping=65, Endurance=82,
                Attr_Inside=58, Attr_Dunks=40, Attr_FreeThrow=76, Attr_MidRange=60, Attr_ThreePoint=74,
                Attr_BasketballIQ=70, Attr_Dribbling=62, Attr_Passing=52,
                Attr_Rebounding_Off=30, Attr_Rebounding_Def=42,
                Attr_PerimeterDefense=75, Attr_InteriorDefense=42
            },
            new Player {
                Name="Kenrich Williams", Team="Oklahoma City Thunder", JerseyNumber=34, Position=Position.SF, Age=28,
                Height=50, Strength=72, Speed=68, Jumping=58, Endurance=85,
                Attr_Inside=58, Attr_Dunks=35, Attr_FreeThrow=70, Attr_MidRange=58, Attr_ThreePoint=60,
                Attr_BasketballIQ=75, Attr_Dribbling=58, Attr_Passing=62,
                Attr_Rebounding_Off=45, Attr_Rebounding_Def=55,
                Attr_PerimeterDefense=78, Attr_InteriorDefense=50
            },
            new Player {
                Name="Josh Giddey", Team="Oklahoma City Thunder", JerseyNumber=3, Position=Position.PG, Age=21,
                Height=60, Strength=65, Speed=68, Jumping=52, Endurance=80,
                Attr_Inside=60, Attr_Dunks=35, Attr_FreeThrow=62, Attr_MidRange=58, Attr_ThreePoint=45,
                Attr_BasketballIQ=82, Attr_Dribbling=72, Attr_Passing=85,
                Attr_Rebounding_Off=52, Attr_Rebounding_Def=60,
                Attr_PerimeterDefense=55, Attr_InteriorDefense=40
            },
            new Player {
                Name="Ousmane Dieng", Team="Oklahoma City Thunder", JerseyNumber=13, Position=Position.SF, Age=21,
                Height=65, Strength=60, Speed=72, Jumping=68, Endurance=75,
                Attr_Inside=55, Attr_Dunks=45, Attr_FreeThrow=65, Attr_MidRange=55, Attr_ThreePoint=58,
                Attr_BasketballIQ=65, Attr_Dribbling=65, Attr_Passing=58,
                Attr_Rebounding_Off=40, Attr_Rebounding_Def=48,
                Attr_PerimeterDefense=58, Attr_InteriorDefense=45
            },
            new Player {
                Name="Jaylin Williams", Team="Oklahoma City Thunder", JerseyNumber=6, Position=Position.C, Age=22,
                Height=70, Strength=72, Speed=52, Jumping=60, Endurance=78,
                Attr_Inside=60, Attr_Dunks=48, Attr_FreeThrow=62, Attr_MidRange=42, Attr_ThreePoint=22,
                Attr_BasketballIQ=68, Attr_Dribbling=38, Attr_Passing=55,
                Attr_Rebounding_Off=62, Attr_Rebounding_Def=68,
                Attr_PerimeterDefense=40, Attr_InteriorDefense=68
            },
            new Player {
                Name="Isaiah Joe", Team="Oklahoma City Thunder", JerseyNumber=11, Position=Position.SG, Age=24,
                Height=45, Strength=50, Speed=72, Jumping=60, Endurance=75,
                Attr_Inside=42, Attr_Dunks=25, Attr_FreeThrow=80, Attr_MidRange=58, Attr_ThreePoint=80,
                Attr_BasketballIQ=68, Attr_Dribbling=62, Attr_Passing=50,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=30,
                Attr_PerimeterDefense=65, Attr_InteriorDefense=28
            },
            new Player {
                Name="Tre Mann", Team="Oklahoma City Thunder", JerseyNumber=23, Position=Position.PG, Age=23,
                Height=45, Strength=58, Speed=74, Jumping=62, Endurance=75,
                Attr_Inside=55, Attr_Dunks=30, Attr_FreeThrow=74, Attr_MidRange=68, Attr_ThreePoint=70,
                Attr_BasketballIQ=68, Attr_Dribbling=70, Attr_Passing=58,
                Attr_Rebounding_Off=25, Attr_Rebounding_Def=32,
                Attr_PerimeterDefense=60, Attr_InteriorDefense=28
            },
        ]
    };
}
