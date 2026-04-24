using UnityEngine;

public sealed class FruitDefinition {
    public FruitDefinition(string name, Color fruitColor) {
        Name = name;
        FruitColor = fruitColor;
    }

    public string Name { get; }
    public Color FruitColor { get; }
}
