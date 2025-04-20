using System;

namespace HandballManager.Data
{
    [Serializable]
    public class BaseData
    {
        // Technical
        public int ShootingPower = 50;
        public int ShootingAccuracy = 50;
        public int Passing = 50;
        public int Dribbling = 50;
        public int Technique = 50;
        public int Tackling = 50;
        public int Blocking = 50;

        // Physical
        public int Speed = 50;
        public int Agility = 50;
        public int Strength = 50;
        public int Jumping = 50;
        public int Stamina = 50;
        public int NaturalFitness = 50;
        public int Resilience = 50;

        // Mental
        public int Aggression = 50;
        public int Bravery = 50;
        public int Composure = 50;
        public int Concentration = 50;
        public int Anticipation = 50;
        public int DecisionMaking = 50;
        public int Teamwork = 50;
        public int WorkRate = 50;
        public int Leadership = 50;
        public int Positioning = 50;
        public int Determination = 50;

        // Goalkeeping (optional)
        public int Reflexes = 20;
        public int Handling = 20;
        public int PositioningGK = 20;
        public int OneOnOnes = 20;
        public int PenaltySaving = 20;
        public int Throwing = 20;
        public int Communication = 20;

        // Potential (optional)
        public int PotentialAbility = 60;
    }
}
