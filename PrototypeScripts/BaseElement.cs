using System;

public enum Element
{
    Gold,
    Wood,
    Water,
    Fire,
    Earth
}

// Pure simulation data. This is not a MonoBehaviour.
[Serializable]
public class BaseElement
{
    public int count;
    public Element myElement;

    // Persist fractional changes so weak effects still accumulate over time.
    public float reactionRemainder;
    public float growthRemainder;
    public float metalExposureSeconds;

    public BaseElement(int num, Element element)
    {
        if ((int)element < 0 || (int)element > (int)Element.Earth)
            throw new ArgumentOutOfRangeException(nameof(element));

        count = Math.Max(0, num);
        myElement = element;
    }

    public BaseElement Clone()
    {
        return new BaseElement(count, myElement)
        {
            reactionRemainder = reactionRemainder,
            growthRemainder = growthRemainder,
            metalExposureSeconds = metalExposureSeconds
        };
    }
}
