using System;

public readonly struct EnvironmentSnapshot
{
    public float Gold { get; }
    public float Wood { get; }
    public float Water { get; }
    public float Fire { get; }
    public float Earth { get; }
    public EnvironmentSnapshot(float gold, float wood, float water, float fire, float earth)
    {
        Gold = Math.Max(0, gold); Wood = Math.Max(0, wood); Water = Math.Max(0, water);
        Fire = Math.Max(0, fire); Earth = Math.Max(0, earth);
    }
    public float this[Element element]
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
    public EnvironmentSnapshot Add(EnvironmentSnapshot other) => new EnvironmentSnapshot(
        Gold + other.Gold, Wood + other.Wood, Water + other.Water, Fire + other.Fire, Earth + other.Earth);
}
