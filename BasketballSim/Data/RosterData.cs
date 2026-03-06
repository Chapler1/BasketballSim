using BasketballSim.Models;

namespace BasketballSim.Data;

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
            new Player {
                Name="Jalen Brunson", Team="New York Knicks", JerseyNumber=11, Position=Position.PG, Age=27,
                Height=74, Strength=68, Speed=72, Jumping=55, Endurance=88,
                Attr_Inside=72, Attr_Dunks=30, Attr_FreeThrow=87, Attr_MidRange=88, Attr_ThreePoint=74,
                Attr_BasketballIQ=92, Attr_Dribbling=88, Attr_Passing=78,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=28,
                Attr_PerimeterDefense=52, Attr_InteriorDefense=30
            },
            new Player {
                Name="OG Anunoby", Team="New York Knicks", JerseyNumber=8, Position=Position.SF, Age=26,
                Height=79, Strength=78, Speed=80, Jumping=74, Endurance=85,
                Attr_Inside=68, Attr_Dunks=58, Attr_FreeThrow=72, Attr_MidRange=62, Attr_ThreePoint=71,
                Attr_BasketballIQ=75, Attr_Dribbling=65, Attr_Passing=55,
                Attr_Rebounding_Off=38, Attr_Rebounding_Def=52,
                Attr_PerimeterDefense=92, Attr_InteriorDefense=70
            },
            new Player {
                Name="Mikal Bridges", Team="New York Knicks", JerseyNumber=25, Position=Position.SG, Age=27,
                Height=78, Strength=72, Speed=78, Jumping=68, Endurance=90,
                Attr_Inside=65, Attr_Dunks=48, Attr_FreeThrow=78, Attr_MidRange=70, Attr_ThreePoint=68,
                Attr_BasketballIQ=80, Attr_Dribbling=70, Attr_Passing=60,
                Attr_Rebounding_Off=32, Attr_Rebounding_Def=48,
                Attr_PerimeterDefense=88, Attr_InteriorDefense=55
            },
            new Player {
                Name="Julius Randle", Team="New York Knicks", JerseyNumber=30, Position=Position.PF, Age=29,
                Height=81, Strength=88, Speed=66, Jumping=65, Endurance=82,
                Attr_Inside=80, Attr_Dunks=62, Attr_FreeThrow=75, Attr_MidRange=78, Attr_ThreePoint=58,
                Attr_BasketballIQ=74, Attr_Dribbling=72, Attr_Passing=68,
                Attr_Rebounding_Off=58, Attr_Rebounding_Def=70,
                Attr_PerimeterDefense=45, Attr_InteriorDefense=62
            },
            new Player {
                Name="Karl-Anthony Towns", Team="New York Knicks", JerseyNumber=32, Position=Position.C, Age=28,
                Height=84, Strength=82, Speed=60, Jumping=68, Endurance=78,
                Attr_Inside=85, Attr_Dunks=70, Attr_FreeThrow=86, Attr_MidRange=75, Attr_ThreePoint=82,
                Attr_BasketballIQ=78, Attr_Dribbling=55, Attr_Passing=60,
                Attr_Rebounding_Off=62, Attr_Rebounding_Def=75,
                Attr_PerimeterDefense=38, Attr_InteriorDefense=68
            },
            new Player {
                Name="Donte DiVincenzo", Team="New York Knicks", JerseyNumber=0, Position=Position.SG, Age=27,
                Height=76, Strength=65, Speed=76, Jumping=66, Endurance=82,
                Attr_Inside=58, Attr_Dunks=38, Attr_FreeThrow=80, Attr_MidRange=64, Attr_ThreePoint=78,
                Attr_BasketballIQ=72, Attr_Dribbling=65, Attr_Passing=58,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=42,
                Attr_PerimeterDefense=70, Attr_InteriorDefense=38
            },
            new Player {
                Name="Josh Hart", Team="New York Knicks", JerseyNumber=3, Position=Position.SF, Age=29,
                Height=77, Strength=75, Speed=74, Jumping=65, Endurance=92,
                Attr_Inside=65, Attr_Dunks=45, Attr_FreeThrow=65, Attr_MidRange=55, Attr_ThreePoint=55,
                Attr_BasketballIQ=70, Attr_Dribbling=60, Attr_Passing=58,
                Attr_Rebounding_Off=62, Attr_Rebounding_Def=68,
                Attr_PerimeterDefense=72, Attr_InteriorDefense=50
            },
            new Player {
                Name="Isaiah Hartenstein", Team="New York Knicks", JerseyNumber=55, Position=Position.C, Age=26,
                Height=84, Strength=80, Speed=55, Jumping=60, Endurance=80,
                Attr_Inside=72, Attr_Dunks=58, Attr_FreeThrow=68, Attr_MidRange=48, Attr_ThreePoint=20,
                Attr_BasketballIQ=78, Attr_Dribbling=40, Attr_Passing=72,
                Attr_Rebounding_Off=65, Attr_Rebounding_Def=78,
                Attr_PerimeterDefense=42, Attr_InteriorDefense=80
            },
            new Player {
                Name="Miles McBride", Team="New York Knicks", JerseyNumber=2, Position=Position.PG, Age=23,
                Height=74, Strength=62, Speed=78, Jumping=62, Endurance=80,
                Attr_Inside=55, Attr_Dunks=28, Attr_FreeThrow=76, Attr_MidRange=60, Attr_ThreePoint=65,
                Attr_BasketballIQ=70, Attr_Dribbling=72, Attr_Passing=60,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=30,
                Attr_PerimeterDefense=68, Attr_InteriorDefense=28
            },
            new Player {
                Name="Precious Achiuwa", Team="New York Knicks", JerseyNumber=5, Position=Position.PF, Age=24,
                Height=80, Strength=80, Speed=72, Jumping=78, Endurance=82,
                Attr_Inside=65, Attr_Dunks=65, Attr_FreeThrow=58, Attr_MidRange=45, Attr_ThreePoint=30,
                Attr_BasketballIQ=62, Attr_Dribbling=48, Attr_Passing=42,
                Attr_Rebounding_Off=68, Attr_Rebounding_Def=70,
                Attr_PerimeterDefense=52, Attr_InteriorDefense=68
            },
            new Player {
                Name="Landry Shamet", Team="New York Knicks", JerseyNumber=14, Position=Position.SG, Age=26,
                Height=76, Strength=58, Speed=68, Jumping=55, Endurance=76,
                Attr_Inside=42, Attr_Dunks=20, Attr_FreeThrow=82, Attr_MidRange=62, Attr_ThreePoint=75,
                Attr_BasketballIQ=72, Attr_Dribbling=60, Attr_Passing=55,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=28,
                Attr_PerimeterDefense=58, Attr_InteriorDefense=25
            },
            new Player {
                Name="Deuce McBride", Team="New York Knicks", JerseyNumber=8, Position=Position.PG, Age=22,
                Height=73, Strength=60, Speed=76, Jumping=60, Endurance=78,
                Attr_Inside=50, Attr_Dunks=25, Attr_FreeThrow=72, Attr_MidRange=55, Attr_ThreePoint=62,
                Attr_BasketballIQ=68, Attr_Dribbling=70, Attr_Passing=58,
                Attr_Rebounding_Off=20, Attr_Rebounding_Def=28,
                Attr_PerimeterDefense=65, Attr_InteriorDefense=25
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
            new Player {
                Name="Shai Gilgeous-Alexander", Team="Oklahoma City Thunder", JerseyNumber=2, Position=Position.PG, Age=25,
                Height=79, Strength=70, Speed=85, Jumping=72, Endurance=92,
                Attr_Inside=88, Attr_Dunks=55, Attr_FreeThrow=88, Attr_MidRange=90, Attr_ThreePoint=72,
                Attr_BasketballIQ=94, Attr_Dribbling=95, Attr_Passing=80,
                Attr_Rebounding_Off=30, Attr_Rebounding_Def=40,
                Attr_PerimeterDefense=80, Attr_InteriorDefense=42
            },
            new Player {
                Name="Luguentz Dort", Team="Oklahoma City Thunder", JerseyNumber=5, Position=Position.SG, Age=25,
                Height=76, Strength=82, Speed=78, Jumping=70, Endurance=88,
                Attr_Inside=62, Attr_Dunks=50, Attr_FreeThrow=68, Attr_MidRange=60, Attr_ThreePoint=66,
                Attr_BasketballIQ=72, Attr_Dribbling=65, Attr_Passing=48,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=45,
                Attr_PerimeterDefense=95, Attr_InteriorDefense=60
            },
            new Player {
                Name="Jalen Williams", Team="Oklahoma City Thunder", JerseyNumber=8, Position=Position.SF, Age=22,
                Height=78, Strength=72, Speed=80, Jumping=70, Endurance=86,
                Attr_Inside=78, Attr_Dunks=58, Attr_FreeThrow=84, Attr_MidRange=80, Attr_ThreePoint=70,
                Attr_BasketballIQ=85, Attr_Dribbling=80, Attr_Passing=68,
                Attr_Rebounding_Off=35, Attr_Rebounding_Def=42,
                Attr_PerimeterDefense=68, Attr_InteriorDefense=45
            },
            new Player {
                Name="Chet Holmgren", Team="Oklahoma City Thunder", JerseyNumber=7, Position=Position.PF, Age=22,
                Height=84, Strength=55, Speed=68, Jumping=78, Endurance=75,
                Attr_Inside=70, Attr_Dunks=65, Attr_FreeThrow=80, Attr_MidRange=72, Attr_ThreePoint=76,
                Attr_BasketballIQ=82, Attr_Dribbling=50, Attr_Passing=62,
                Attr_Rebounding_Off=48, Attr_Rebounding_Def=68,
                Attr_PerimeterDefense=55, Attr_InteriorDefense=88
            },
            new Player {
                Name="Isaiah Hartenstein", Team="Oklahoma City Thunder", JerseyNumber=55, Position=Position.C, Age=26,
                Height=84, Strength=80, Speed=55, Jumping=60, Endurance=80,
                Attr_Inside=72, Attr_Dunks=58, Attr_FreeThrow=68, Attr_MidRange=48, Attr_ThreePoint=20,
                Attr_BasketballIQ=78, Attr_Dribbling=40, Attr_Passing=72,
                Attr_Rebounding_Off=65, Attr_Rebounding_Def=78,
                Attr_PerimeterDefense=42, Attr_InteriorDefense=80
            },
            new Player {
                Name="Aaron Wiggins", Team="Oklahoma City Thunder", JerseyNumber=21, Position=Position.SG, Age=24,
                Height=77, Strength=68, Speed=76, Jumping=66, Endurance=82,
                Attr_Inside=60, Attr_Dunks=42, Attr_FreeThrow=74, Attr_MidRange=62, Attr_ThreePoint=72,
                Attr_BasketballIQ=70, Attr_Dribbling=62, Attr_Passing=52,
                Attr_Rebounding_Off=30, Attr_Rebounding_Def=42,
                Attr_PerimeterDefense=74, Attr_InteriorDefense=42
            },
            new Player {
                Name="Kenrich Williams", Team="Oklahoma City Thunder", JerseyNumber=34, Position=Position.SF, Age=28,
                Height=79, Strength=74, Speed=70, Jumping=62, Endurance=85,
                Attr_Inside=60, Attr_Dunks=40, Attr_FreeThrow=70, Attr_MidRange=58, Attr_ThreePoint=60,
                Attr_BasketballIQ=75, Attr_Dribbling=58, Attr_Passing=60,
                Attr_Rebounding_Off=45, Attr_Rebounding_Def=55,
                Attr_PerimeterDefense=76, Attr_InteriorDefense=52
            },
            new Player {
                Name="Josh Giddey", Team="Oklahoma City Thunder", JerseyNumber=3, Position=Position.PG, Age=21,
                Height=81, Strength=68, Speed=70, Jumping=58, Endurance=80,
                Attr_Inside=62, Attr_Dunks=38, Attr_FreeThrow=65, Attr_MidRange=58, Attr_ThreePoint=50,
                Attr_BasketballIQ=80, Attr_Dribbling=70, Attr_Passing=82,
                Attr_Rebounding_Off=50, Attr_Rebounding_Def=58,
                Attr_PerimeterDefense=55, Attr_InteriorDefense=40
            },
            new Player {
                Name="Ousmane Dieng", Team="Oklahoma City Thunder", JerseyNumber=13, Position=Position.SF, Age=21,
                Height=82, Strength=62, Speed=74, Jumping=68, Endurance=76,
                Attr_Inside=55, Attr_Dunks=45, Attr_FreeThrow=65, Attr_MidRange=55, Attr_ThreePoint=58,
                Attr_BasketballIQ=65, Attr_Dribbling=62, Attr_Passing=55,
                Attr_Rebounding_Off=38, Attr_Rebounding_Def=45,
                Attr_PerimeterDefense=55, Attr_InteriorDefense=42
            },
            new Player {
                Name="Jaylin Williams", Team="Oklahoma City Thunder", JerseyNumber=6, Position=Position.C, Age=22,
                Height=82, Strength=72, Speed=58, Jumping=62, Endurance=78,
                Attr_Inside=60, Attr_Dunks=48, Attr_FreeThrow=62, Attr_MidRange=42, Attr_ThreePoint=25,
                Attr_BasketballIQ=68, Attr_Dribbling=38, Attr_Passing=55,
                Attr_Rebounding_Off=58, Attr_Rebounding_Def=65,
                Attr_PerimeterDefense=40, Attr_InteriorDefense=65
            },
            new Player {
                Name="Isaiah Joe", Team="Oklahoma City Thunder", JerseyNumber=11, Position=Position.SG, Age=24,
                Height=76, Strength=58, Speed=72, Jumping=62, Endurance=76,
                Attr_Inside=45, Attr_Dunks=28, Attr_FreeThrow=78, Attr_MidRange=58, Attr_ThreePoint=76,
                Attr_BasketballIQ=68, Attr_Dribbling=62, Attr_Passing=50,
                Attr_Rebounding_Off=22, Attr_Rebounding_Def=30,
                Attr_PerimeterDefense=62, Attr_InteriorDefense=28
            },
            new Player {
                Name="Tre Mann", Team="Oklahoma City Thunder", JerseyNumber=23, Position=Position.PG, Age=23,
                Height=76, Strength=60, Speed=74, Jumping=64, Endurance=76,
                Attr_Inside=55, Attr_Dunks=32, Attr_FreeThrow=72, Attr_MidRange=65, Attr_ThreePoint=68,
                Attr_BasketballIQ=68, Attr_Dribbling=68, Attr_Passing=58,
                Attr_Rebounding_Off=25, Attr_Rebounding_Def=32,
                Attr_PerimeterDefense=58, Attr_InteriorDefense=28
            },
        ]
    };
}
