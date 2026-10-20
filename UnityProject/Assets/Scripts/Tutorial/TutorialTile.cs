using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName="五行世界/教学 Tile")]
public class TutorialTile : Tile
{
    [Header("每次铺设时提供的初始资源；运行数量按格子独立保存")]
    [Min(0)] public float gold,wood,water,fire,earth;
    public float elevation=3;
    public bool blocksSimulation;
}

[System.Serializable]
public class TutorialCellSeed
{
    public Vector3Int cell;
    public float gold,wood,water,fire,earth,elevation;
    public CellState State => new CellState {Gold=gold,Wood=wood,Water=water,Fire=fire,Earth=earth};
}
