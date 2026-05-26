using BasketballSim.Models;

namespace BasketballSim.Services;

/// <summary>
/// Real 2025-26 NBA head coach profiles for all 30 teams.
/// OffRtg/DefRtg averaged ~61/62 across the league to preserve calibration baseline.
/// </summary>
public static class CoachFactory
{
    public static Coach GetCoach(string teamName) => teamName switch
    {
        "Atlanta Hawks" => new Coach
        {
            Name = "Quin Snyder", Age = 58, YearsCoached = 11, Potential = 76,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 55, StarterLoadPref = 55,
            OffensiveRating = 74, DefensiveRating = 62, HelpDefenseAmount = 50,
        },
        "Boston Celtics" => new Coach
        {
            Name = "Joe Mazzulla", Age = 36, YearsCoached = 3, Potential = 88,
            OffStyle = OffensiveStyle.PaceAndSpace,  DefStyle = DefensiveStyle.StopTheThree,
            PacePref = 102, RotationDepthPref = 40, VetPreference = 55, StarterLoadPref = 65,
            OffensiveRating = 80, DefensiveRating = 78, HelpDefenseAmount = 75,
        },
        "Brooklyn Nets" => new Coach
        {
            Name = "Jordi Fernández", Age = 40, YearsCoached = 1, Potential = 70,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 65, VetPreference = 35, StarterLoadPref = 45,
            OffensiveRating = 55, DefensiveRating = 52, HelpDefenseAmount = 42,
        },
        "Charlotte Hornets" => new Coach
        {
            Name = "Charles Lee", Age = 40, YearsCoached = 1, Potential = 72,
            OffStyle = OffensiveStyle.PaceAndSpace,  DefStyle = DefensiveStyle.Balanced,
            PacePref = 101, RotationDepthPref = 60, VetPreference = 30, StarterLoadPref = 50,
            OffensiveRating = 55, DefensiveRating = 52, HelpDefenseAmount = 42,
        },
        "Chicago Bulls" => new Coach
        {
            Name = "Billy Donovan", Age = 59, YearsCoached = 9, Potential = 72,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 50, VetPreference = 62, StarterLoadPref = 58,
            OffensiveRating = 66, DefensiveRating = 60, HelpDefenseAmount = 55,
        },
        "Cleveland Cavaliers" => new Coach
        {
            Name = "Kenny Atkinson", Age = 52, YearsCoached = 5, Potential = 72,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 50, StarterLoadPref = 55,
            OffensiveRating = 68, DefensiveRating = 65, HelpDefenseAmount = 58,
        },
        "Detroit Pistons" => new Coach
        {
            Name = "J.B. Bickerstaff", Age = 44, YearsCoached = 5, Potential = 58,
            OffStyle = OffensiveStyle.GritAndGrind,  DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 96, RotationDepthPref = 30, VetPreference = 72, StarterLoadPref = 68,
            OffensiveRating = 50, DefensiveRating = 72, HelpDefenseAmount = 65,
        },
        "Indiana Pacers" => new Coach
        {
            Name = "Rick Carlisle", Age = 65, YearsCoached = 23, Potential = 78,
            OffStyle = OffensiveStyle.PaceAndSpace,  DefStyle = DefensiveStyle.Balanced,
            PacePref = 108, RotationDepthPref = 55, VetPreference = 62, StarterLoadPref = 60,
            OffensiveRating = 76, DefensiveRating = 52, HelpDefenseAmount = 44,
        },
        "Milwaukee Bucks" => new Coach
        {
            Name = "Taylor Jenkins", Age = 42, YearsCoached = 6, Potential = 70,
            OffStyle = OffensiveStyle.PickAndRollHeavy, DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 50, VetPreference = 50, StarterLoadPref = 60,
            OffensiveRating = 65, DefensiveRating = 62, HelpDefenseAmount = 55,
        },
        "Miami Heat" => new Coach
        {
            Name = "Erik Spoelstra", Age = 54, YearsCoached = 17, Potential = 82,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 97, RotationDepthPref = 62, VetPreference = 70, StarterLoadPref = 55,
            OffensiveRating = 70, DefensiveRating = 82, HelpDefenseAmount = 78,
        },
        "New York Knicks" => new Coach
        {
            Name = "Mike Brown", Age = 55, YearsCoached = 8, Potential = 72,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 99, RotationDepthPref = 45, VetPreference = 62, StarterLoadPref = 62,
            OffensiveRating = 70, DefensiveRating = 74, HelpDefenseAmount = 68,
        },
        "Orlando Magic" => new Coach
        {
            Name = "Jamahl Mosley", Age = 47, YearsCoached = 4, Potential = 62,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 99, RotationDepthPref = 58, VetPreference = 42, StarterLoadPref = 52,
            OffensiveRating = 52, DefensiveRating = 64, HelpDefenseAmount = 65,
        },
        "Philadelphia 76ers" => new Coach
        {
            Name = "Nick Nurse", Age = 57, YearsCoached = 6, Potential = 80,
            OffStyle = OffensiveStyle.PaceAndSpace,  DefStyle = DefensiveStyle.StopTheThree,
            PacePref = 101, RotationDepthPref = 55, VetPreference = 55, StarterLoadPref = 60,
            OffensiveRating = 62, DefensiveRating = 80, HelpDefenseAmount = 70,
        },
        "Toronto Raptors" => new Coach
        {
            Name = "Darko Rajaković", Age = 45, YearsCoached = 2, Potential = 68,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.StopTheThree,
            PacePref = 99, RotationDepthPref = 62, VetPreference = 40, StarterLoadPref = 50,
            OffensiveRating = 56, DefensiveRating = 66, HelpDefenseAmount = 60,
        },
        "Washington Wizards" => new Coach
        {
            Name = "Brian Keefe", Age = 45, YearsCoached = 2, Potential = 50,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 99, RotationDepthPref = 65, VetPreference = 35, StarterLoadPref = 45,
            OffensiveRating = 42, DefensiveRating = 42, HelpDefenseAmount = 38,
        },
        "Denver Nuggets" => new Coach
        {
            Name = "David Adelman", Age = 45, YearsCoached = 2, Potential = 75,
            OffStyle = OffensiveStyle.PickAndRollHeavy, DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 55, StarterLoadPref = 60,
            OffensiveRating = 70, DefensiveRating = 66, HelpDefenseAmount = 58,
        },
        "Minnesota Timberwolves" => new Coach
        {
            Name = "Chris Finch", Age = 55, YearsCoached = 5, Potential = 68,
            OffStyle = OffensiveStyle.PickAndRollHeavy, DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 97, RotationDepthPref = 50, VetPreference = 58, StarterLoadPref = 62,
            OffensiveRating = 60, DefensiveRating = 74, HelpDefenseAmount = 70,
        },
        "Oklahoma City Thunder" => new Coach
        {
            Name = "Mark Daigneault", Age = 39, YearsCoached = 5, Potential = 85,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 102, RotationDepthPref = 62, VetPreference = 30, StarterLoadPref = 55,
            OffensiveRating = 76, DefensiveRating = 76, HelpDefenseAmount = 68,
        },
        "Portland Trail Blazers" => new Coach
        {
            Name = "Tiago Splitter", Age = 40, YearsCoached = 1, Potential = 55,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 99, RotationDepthPref = 65, VetPreference = 30, StarterLoadPref = 45,
            OffensiveRating = 48, DefensiveRating = 48, HelpDefenseAmount = 38,
        },
        "Utah Jazz" => new Coach
        {
            Name = "Will Hardy", Age = 36, YearsCoached = 3, Potential = 78,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 101, RotationDepthPref = 65, VetPreference = 25, StarterLoadPref = 45,
            OffensiveRating = 60, DefensiveRating = 54, HelpDefenseAmount = 52,
        },
        "Golden State Warriors" => new Coach
        {
            Name = "Steve Kerr", Age = 59, YearsCoached = 11, Potential = 82,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 62, StarterLoadPref = 55,
            OffensiveRating = 80, DefensiveRating = 68, HelpDefenseAmount = 45,
        },
        "Los Angeles Clippers" => new Coach
        {
            Name = "Tyronn Lue", Age = 47, YearsCoached = 5, Potential = 68,
            OffStyle = OffensiveStyle.IsoHeavy,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 45, VetPreference = 62, StarterLoadPref = 60,
            OffensiveRating = 66, DefensiveRating = 60, HelpDefenseAmount = 55,
        },
        "Los Angeles Lakers" => new Coach
        {
            Name = "JJ Redick", Age = 40, YearsCoached = 1, Potential = 65,
            OffStyle = OffensiveStyle.PaceAndSpace,  DefStyle = DefensiveStyle.Balanced,
            PacePref = 101, RotationDepthPref = 50, VetPreference = 45, StarterLoadPref = 55,
            OffensiveRating = 56, DefensiveRating = 48, HelpDefenseAmount = 38,
        },
        "Phoenix Suns" => new Coach
        {
            Name = "Jordan Ott", Age = 40, YearsCoached = 1, Potential = 60,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 50, StarterLoadPref = 50,
            OffensiveRating = 50, DefensiveRating = 50, HelpDefenseAmount = 42,
        },
        "Sacramento Kings" => new Coach
        {
            Name = "Doug Christie", Age = 54, YearsCoached = 1, Potential = 58,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 55, StarterLoadPref = 55,
            OffensiveRating = 50, DefensiveRating = 55, HelpDefenseAmount = 45,
        },
        "Dallas Mavericks" => new Coach
        {
            Name = "Jason Kidd", Age = 52, YearsCoached = 6, Potential = 65,
            OffStyle = OffensiveStyle.Heliocentric,  DefStyle = DefensiveStyle.Balanced,
            PacePref = 101, RotationDepthPref = 45, VetPreference = 60, StarterLoadPref = 65,
            OffensiveRating = 62, DefensiveRating = 56, HelpDefenseAmount = 52,
        },
        "Houston Rockets" => new Coach
        {
            Name = "Ime Udoka", Age = 47, YearsCoached = 3, Potential = 70,
            OffStyle = OffensiveStyle.GritAndGrind,  DefStyle = DefensiveStyle.ProtectThePaint,
            PacePref = 103, RotationDepthPref = 55, VetPreference = 50, StarterLoadPref = 60,
            OffensiveRating = 54, DefensiveRating = 78, HelpDefenseAmount = 72,
        },
        "Memphis Grizzlies" => new Coach
        {
            Name = "Tuomas Iisalo", Age = 42, YearsCoached = 1, Potential = 65,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 101, RotationDepthPref = 60, VetPreference = 40, StarterLoadPref = 55,
            OffensiveRating = 56, DefensiveRating = 56, HelpDefenseAmount = 45,
        },
        "New Orleans Pelicans" => new Coach
        {
            Name = "James Borrego", Age = 49, YearsCoached = 4, Potential = 52,
            OffStyle = OffensiveStyle.Balanced,      DefStyle = DefensiveStyle.Balanced,
            PacePref = 100, RotationDepthPref = 55, VetPreference = 50, StarterLoadPref = 50,
            OffensiveRating = 42, DefensiveRating = 42, HelpDefenseAmount = 42,
        },
        "San Antonio Spurs" => new Coach
        {
            Name = "Mitch Johnson", Age = 36, YearsCoached = 1, Potential = 74,
            OffStyle = OffensiveStyle.MotionFlow,    DefStyle = DefensiveStyle.Balanced,
            PacePref = 99, RotationDepthPref = 65, VetPreference = 25, StarterLoadPref = 50,
            OffensiveRating = 60, DefensiveRating = 56, HelpDefenseAmount = 42,
        },

        _ => new Coach { Name = "Staff Coach", OffensiveRating = 55, DefensiveRating = 55 },
    };
}
