namespace BasketballSim.Models;

public class PlayerConfig
{
    public required string   Name     { get; init; }
    public required string   Team     { get; init; }
    public required Position Position { get; init; }

    // Physical
    public int Height    { get; set; } = 78;
    public int Strength  { get; set; } = 72;
    public int Speed     { get; set; } = 72;
    public int Jumping   { get; set; } = 66;
    public int Endurance { get; set; } = 83;

    // Shooting
    public int Inside     { get; set; } = 64;
    public int Dunks      { get; set; } = 46;
    public int FreeThrow  { get; set; } = 74;
    public int MidRange   { get; set; } = 63;
    public int ThreePoint { get; set; } = 61;

    // Skill
    public int BasketballIQ  { get; set; } = 75;
    public int Dribbling     { get; set; } = 63;
    public int Passing       { get; set; } = 61;
    public int ReboundingOff { get; set; } = 41;
    public int ReboundingDef { get; set; } = 51;

    // Defense
    public int PerimeterDefense { get; set; } = 63;
    public int InteriorDefense  { get; set; } = 51;

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
