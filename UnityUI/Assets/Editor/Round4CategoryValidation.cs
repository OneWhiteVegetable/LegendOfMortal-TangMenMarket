using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static class Round4CategoryValidation
    {
        public static void BatchValidate()
        {
            string output = OutputPath();
            Round4CategoryReport report = new Round4CategoryReport();
            report.unityVersion = Application.unityVersion;
            report.runtimeStatus = "NOT_RUN_IN_THIS_AUTHORING_CHECK: runtime integration is evaluated by the native harness; no game process is started.";
            try
            {
                Run(report);
                report.status = report.failures.Count == 0 ? "AUTHORING_PASS" : "AUTHORING_FAIL";
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true) + "\n");
                Debug.Log("Round4 category authoring validation: " + report.status);
                EditorApplication.Exit(report.failures.Count == 0 ? 0 : 2);
            }
            catch (Exception exception)
            {
                report.status = "AUTHORING_ERROR";
                report.failures.Add(exception.ToString());
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true) + "\n");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Run(Round4CategoryReport report)
        {
            UiAssets.ConfigureAndValidate();
            Check(report, UiAssets.Entries.Count() == 165, "V4 manifest includes all sprites and the loan countdown numerals.");
            Check(report, !UiAssets.Has("card_goods_pressed"), "Pressed retains the normal sprite instead of expanding the manifest.");

            FieldInfo mapField = typeof(UiBuilder).GetField("currentMap", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo rootField = typeof(UiBuilder).GetField("currentRoot", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo buildMethod = typeof(UiBuilder).GetMethod("BuildGoodsCard", BindingFlags.Static | BindingFlags.NonPublic);
            if (mapField == null || rootField == null || buildMethod == null)
                throw new MissingMethodException("GoodsCard authoring entry points were not found.");

            RectTransform root = null;
            try
            {
                mapField.SetValue(null, new UiMapBuilder("GoodsCard"));
                rootField.SetValue(null, null);
                root = buildMethod.Invoke(null, null) as RectTransform;
                if (root == null)
                    throw new InvalidOperationException("BuildGoodsCard returned no RectTransform.");
                Canvas.ForceUpdateCanvases();
                UiPrefabMap map = ((UiMapBuilder)mapField.GetValue(null)).Build();
                ValidateHierarchy(report, root, map);
            }
            finally
            {
                mapField.SetValue(null, null);
                rootField.SetValue(null, null);
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        private static void ValidateHierarchy(Round4CategoryReport report, RectTransform root, UiPrefabMap map)
        {
            Check(report, Mathf.Approximately(root.sizeDelta.x, 468f) && Mathf.Approximately(root.sizeDelta.y, 384f),
                "GoodsCard keeps its 468x384 authoring frame.");
            RectTransform category = root.Find("CategorySkew") as RectTransform;
            Check(report, category != null, "CategorySkew is a direct native RectTransform child.");
            if (category == null)
                return;

            report.category = new Round4CategoryHierarchy
            {
                width = category.rect.width,
                height = category.rect.height,
                rotation = category.localEulerAngles.z,
                scaleX = category.localScale.x,
                scaleY = category.localScale.y,
                componentCount = category.GetComponents<Component>().Length
            };
            Check(report, category.GetComponents<Component>().Length == 1,
                "CategorySkew contains no custom runtime script or graphic.");
            Check(report, category.localRotation == Quaternion.identity && category.localScale == Vector3.one,
                "CategorySkew is not rotated or whole-rectangle scaled.");

            RectTransform back = category.Find("CategoryBack") as RectTransform;
            Transform textTransform = category.Find("CategoryText");
            Text text = textTransform == null ? null : textTransform.GetComponent<Text>();
            Check(report, back != null && text != null, "CategoryBack and CategoryText share CategorySkew.");
            if (back == null || text == null)
                return;

            Check(report, IsStretched(back) && IsStretched(text.rectTransform),
                "CategoryBack and CategoryText stretch to the same parent rectangle.");
            Check(report, Mathf.Approximately(back.rect.width, text.rectTransform.rect.width) &&
                Mathf.Approximately(back.rect.height, text.rectTransform.rect.height),
                "CategoryBack and CategoryText have identical local rectangles.");
            Check(report, category.localRotation == Quaternion.identity &&
                back.localRotation == Quaternion.identity && text.rectTransform.localRotation == Quaternion.identity,
                "No label node substitutes rotation for screen-axis mesh skew.");

            UiNodeMap categoryBinding = map.nodes.SingleOrDefault(node => node.key == "CategorySkew");
            UiNodeMap textBinding = map.nodes.SingleOrDefault(node => node.key == "CategoryText");
            Check(report, categoryBinding != null && categoryBinding.path == "CategorySkew" &&
                categoryBinding.type == "RectTransform", "Binding CategorySkew -> CategorySkew (RectTransform).");
            Check(report, textBinding != null && textBinding.path == "CategorySkew/CategoryText" &&
                textBinding.type == "Text", "Binding CategoryText -> CategorySkew/CategoryText (Text).");
            report.category.bindingPath = categoryBinding == null ? string.Empty : categoryBinding.path;
            report.category.textBindingPath = textBinding == null ? string.Empty : textBinding.path;

            Button button = root.GetComponent<Button>();
            Sprite normal = UiAssets.Sprite("card_goods_normal");
            Sprite hover = UiAssets.Sprite("card_goods_hover");
            Sprite delisted = UiAssets.Sprite("card_goods_delisted");
            Check(report, button.image.sprite == normal && button.spriteState.highlightedSprite == normal,
                "Normal and highlighted states retain the existing normal-sprite behavior.");
            Check(report, button.spriteState.pressedSprite == null || button.spriteState.pressedSprite == normal,
                "Pressed state retains the normal sprite.");
            Check(report, button.spriteState.disabledSprite == delisted,
                "Delisted state retains card_goods_delisted.");
            Check(report, normal.border == new Vector4(24f, 24f, 24f, 24f) &&
                hover.border == new Vector4(16f, 32f, 32f, 32f),
                "Normal and hover nine-slice borders are unchanged.");

            report.integrationRequirements.Add(
                "MarketItemCard.UpdateCategoryPaper: resize/position CategorySkew only; CategoryBack and CategoryText remain stretched.");
            report.integrationRequirements.Add(
                "MarketItemCard.RegisterSkewEffects: apply +tan(7deg) with the existing local-Y formula to CategoryBack/CategoryText and exclude both from the dynamic _skewEffects array.");
            report.integrationRequirements.Add(
                "Capture normal, hover, pressed and delisted native crops after runtime integration; this authoring report is not a gameplay capture.");
        }

        private static bool IsStretched(RectTransform rect)
        {
            return rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
        }

        private static void Check(Round4CategoryReport report, bool passed, string description)
        {
            if (passed)
                report.passed.Add(description);
            else
                report.failures.Add(description);
        }

        private static string OutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == "-round4CategoryOutput")
                    return arguments[i + 1];
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Round4CategoryValidation");
        }
    }

    [Serializable]
    internal sealed class Round4CategoryReport
    {
        public int schemaVersion = 1;
        public string status;
        public string runtimeStatus;
        public string unityVersion;
        public bool generatedInMemoryOnly = true;
        public bool sharedPrefabWritten;
        public Round4CategoryHierarchy category = new Round4CategoryHierarchy();
        public List<string> integrationRequirements = new List<string>();
        public List<string> passed = new List<string>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    internal sealed class Round4CategoryHierarchy
    {
        public float width;
        public float height;
        public float rotation;
        public float scaleX;
        public float scaleY;
        public int componentCount;
        public string bindingPath;
        public string textBindingPath;
    }
}
