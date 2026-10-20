#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ElementTile))]
public class ElementTileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("这里配置地块的初始五行数量。运行中的数量由世界管理器统一计算；重置世界会重新读取这些初始值。", MessageType.Info);
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var c = ((ElementTile)target).Current;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("当前环境（只读）", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("金 / 木 / 水 / 火 / 土", $"{c.Gold:0.00} / {c.Wood:0.00} / {c.Water:0.00} / {c.Fire:0.00} / {c.Earth:0.00}");
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
#endif
