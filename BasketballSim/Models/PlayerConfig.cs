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
    public int oBBIQ         { get; set; } = 50;  // Offensive Basketball IQ
    public int dBBIQ         { get; set; } = 50;  // Defensive Basketball IQ
    public int Hustle        { get; set; } = 50;
    public int Dribbling     { get; set; } = 50;
    public int Passing       { get; set; } = 50;
    public int ReboundingOff { get; set; } = 50;
    public int ReboundingDef { get; set; } = 50;

    // Defense
    public int PerimeterDefense { get; set; } = 50;
    public int InteriorDefense  { get; set; } = 50;
    // 5 = very clean, 95 = very foul-prone (mapped from real foul/36 data)
    public int FoulTendency     { get; set; } = 50;

    // Tendencies — Offense
    public int Tend_Touches  { get; set; } = 50;
    public int Tend_Drive    { get; set; } = 50;
    public int Tend_PostUp   { get; set; } = 50;
    public int Tend_Iso      { get; set; } = 50;
    public int Tend_Cut      { get; set; } = 50;
    public int Tend_PullUp   { get; set; } = 50;
    public int Tend_MidRange { get; set; } = 50;
    public int Tend_ThreePt  { get; set; } = 50;
    public int Tend_OffReb   { get; set; } = 50;

    // Tendencies — Defense
    public int Tend_Steal    { get; set; } = 50;
    public int Tend_Block    { get; set; } = 50;

    public Player ToPlayer() => new()
    {
        Name = Name, Team = Team, Position = Position,
        Height = Height, Strength = Strength, Speed = Speed,
        Jumping = Jumping, Endurance = Endurance,
        Attr_Inside = Inside, Attr_Dunks = Dunks, Attr_FreeThrow = FreeThrow,
        Attr_MidRange = MidRange, Attr_ThreePoint = ThreePoint,
        Attr_oBBIQ = oBBIQ, Attr_dBBIQ = dBBIQ, Attr_Hustle = Hustle,
        Attr_Dribbling = Dribbling,
        Attr_Passing = Passing,
        Attr_Rebounding_Off = ReboundingOff, Attr_Rebounding_Def = ReboundingDef,
        Attr_PerimeterDefense = PerimeterDefense, Attr_InteriorDefense = InteriorDefense,
        Attr_FoulTendency = FoulTendency,
        Tendencies = new PlayerTendencies
        {
            Touches = Tend_Touches, Drive = Tend_Drive, PostUp = Tend_PostUp,
            Iso = Tend_Iso, Cut = Tend_Cut, PullUp = Tend_PullUp,
            MidRange = Tend_MidRange, ThreePt = Tend_ThreePt,
            OffRebound = Tend_OffReb,
            Steal = Tend_Steal, Block = Tend_Block
        }
    };
}
