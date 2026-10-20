using System;

// Immutable quantities captured before a simulation step.
// The snapshot describes influence; it does not own or consume resources.
public readonly struct EnvironmentSnapshot
{
    public int Gold { get; }
    public int Wood { get; }
    public int Water { get; }
    public int Fire { get; }
    public int Earth { get; }

    public EnvironmentSnapshot(int gold, int wood, int water, int fire, int earth)
    {
        Gold = Math.Max(0, gold);
        Wood = Math.Max(0, wood);
        Water = Math.Max(0, water);
        Fire = Math.Max(0, fire);
        Earth = Math.Max(0, earth);
    }

    public int this[Element element]
    {
        get
        {
            switch (element)
            {
                case Element.Gold: return Gold;
                case Element.Wood: return Wood;
                case Element.Water: return Water;
                case Element.Fire: return Fire;
                case Element.Earth: return Earth;
                default: throw new ArgumentOutOfRangeException(nameof(element));
            }
        }
    }

    public EnvironmentSnapshot Add(EnvironmentSnapshot other)
    {
        return new EnvironmentSnapshot(
            Gold + other.Gold, Wood + other.Wood, Water + other.Water,
            Fire + other.Fire, Earth + other.Earth);
    }

    public override string ToString()
    {
        return $"Gold:{Gold} Wood:{Wood} Water:{Water} Fire:{Fire} Earth:{Earth}";
    }
}
