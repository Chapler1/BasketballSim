namespace BasketballSim.Models;

public class PlayerConfig
{
    public required string   Name     { get; init; }
    public required string   Team     { get; init; }
    public required Position Position { get; init; }

    // Physical
    public int Height    { get; set; } = 50;
    public int Strength  { get; set; } = 50;
    public int Speed     { get; set; } = 50;
    public int Jumping   { get; set; } = 50;
    public int Endurance { get; set; } = 50;

    // Shooting
    public int Inside     { get; set; } = 50;
    public int Dunks      { get; set; } = 50;
    public int FreeThrow  { get; set; } = 50;
    public int MidRange   { get; set; } = 50;
    public int ThreePoint { get; set; } = 50;

    // Skill
    public int BasketballIQ  { get; set; } = 50;
    public int Dribbling     { get; set; } = 50;
    public int Passing       { get; set; } = 50;
    public int ReboundingOff { get; set; } = 50;
    public int ReboundingDef { get; set; } = 50;

    // Defense
    public int PerimeterDefense { get; set; } = 50;
    public int InteriorDefense  { get; set; } = 50;

    public Player ToPlayer() => new()
    {
        Name = Name, Team = Team, Position = Position,
        Height = Height, Strength = Strength, Speed = Speed,
        Jumping = Jumping, Endurance = Endurance,
        Attr_Inside = Inside, Attr_Dunks = Dunks, Attr_FreeThrow = FreeThrow,
        Attr_MidRange = MidRange, Attr_ThreePoint = ThreePoint,
        Attr_BasketballIQ = BasketballIQ, Attr_Dribbling = Dribbling,
        Attr_Passing = Passing,
        Attr_Rebounding_Off = ReboundingOff, Attr_Rebounding_Def = ReboundingDef,
        Attr_PerimeterDefense = PerimeterDefense, Attr_InteriorDefense = InteriorDefense
    };
}
