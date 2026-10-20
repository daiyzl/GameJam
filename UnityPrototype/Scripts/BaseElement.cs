using System;

public enum Element { Gold, Wood, Water, Fire, Earth }

[Serializable]
public class BaseElement
{
    // Fractional quantities preserve weak fire damage and small water flows.
    public float count;
    public Element myElement;
    public float metalExposureSeconds;

    public BaseElement(float num, Element element)
    {
        if ((int)element < 0 || (int)element > 4) throw new ArgumentOutOfRangeException(nameof(element));
        count = Math.Max(0f, num);
        myElement = element;
    }
}
