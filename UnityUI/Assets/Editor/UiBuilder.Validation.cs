using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private static void ValidatePrefabs()
        {
            UiMapData map = JsonUtility.FromJson<UiMapData>(File.ReadAllText(BindingPath));
            if (map == null || map.prefabs == null || map.prefabs.Length != PrefabOrder.Length)
            {
                throw new InvalidOperationException("Binding map must contain every prefab in the generation contract.");
            }

            foreach (UiPrefabMap prefabMap in map.prefabs)
            {
                string path = PrefabRoot + "/" + prefabMap.name + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.name != prefabMap.name)
                {
                    throw new InvalidOperationException("Missing generated prefab: " + path);
                }

                RectTransform root = prefab.GetComponent<RectTransform>();
                if (root == null)
                {
                    throw new InvalidOperationException("Prefab root is not a RectTransform: " + prefabMap.name);
                }

                foreach (UiNodeMap node in prefabMap.nodes)
                {
                    Transform target = string.IsNullOrEmpty(node.path) ? prefab.transform : prefab.transform.Find(node.path);
                    if (target == null)
                    {
                        throw new InvalidOperationException("Broken binding " + prefabMap.name + ":" + node.key + " -> " + node.path);
                    }

                    if (node.type == "Text" && target.GetComponent<Text>() == null ||
                        node.type == "Image" && target.GetComponent<Image>() == null ||
                        node.type == "Button" && target.GetComponent<Button>() == null)
                    {
                        throw new InvalidOperationException("Binding component mismatch " + prefabMap.name + ":" + node.key);
                    }
                }

                if (prefab.GetComponentsInChildren<Slider>(true).Length != 0)
                {
                    throw new InvalidOperationException("Debug sliders are not allowed in " + prefabMap.name);
                }

                if (prefabMap.nodes.Any(node => node.key.StartsWith("Stamina", StringComparison.Ordinal) || node.type == "Slider"))
                {
                    throw new InvalidOperationException("Stamina debug binding found in " + prefabMap.name);
                }

                if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null))
                {
                    throw new InvalidOperationException("Missing script reference in " + prefabMap.name);
                }

                if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component.GetType().Namespace != null && component.GetType().Namespace.StartsWith("XingShang", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Custom runtime script found in " + prefabMap.name);
                }

                CanvasScaler scaler = prefab.GetComponent<CanvasScaler>();
                if (scaler != null && (scaler.referenceResolution != new Vector2(1920f, 1080f) || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize))
                {
                    throw new InvalidOperationException("Canvas scaling mismatch in " + prefabMap.name);
                }

                if (prefabMap.name == "MarketWindow" || prefabMap.name == "InventoryWindow" || prefabMap.name == "ItemDetail" || prefabMap.name == "Modal")
                {
                    RectTransform blocker = prefab.transform.Find("Blocker") as RectTransform;
                    if (blocker == null || blocker.anchorMin != Vector2.zero || blocker.anchorMax != Vector2.one)
                    {
                        throw new InvalidOperationException("Full-screen blocker must stretch in " + prefabMap.name);
                    }
                }
            }

            UiPrefabMap inventoryMap = map.prefabs.Single(prefab => prefab.name == "InventoryWindow");
            string[] inventoryTextKeys = { "CapacityCaptionText", "CapacityText", "WeightCaptionText", "WeightText" };
            foreach (string key in inventoryTextKeys)
            {
                if (!inventoryMap.nodes.Any(node => node.key == key && node.type == "Text"))
                {
                    throw new InvalidOperationException("Missing inventory capacity/weight text binding: " + key);
                }
            }

            GameObject chartHost = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/ChartHost.prefab");
            if (chartHost.GetComponents<Component>().Length != 1 || chartHost.GetComponent<RectTransform>() == null)
            {
                throw new InvalidOperationException("ChartHost must contain only a RectTransform.");
            }

            if (PlayerSettings.colorSpace != ColorSpace.Gamma || EditorUserBuildSettings.selectedStandaloneTarget != BuildTarget.StandaloneWindows64)
            {
                throw new InvalidOperationException("Project must use Gamma and StandaloneWindows64.");
            }

            Debug.Log("XingShang UI: prefab hierarchy, binding paths, native components, CanvasScaler and blockers validated.");
        }
    }
}
