using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Voyager.UI;
using static Voyager.EditorTools.GameScreenBuilder;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Estética de menús "máquina de escribir": texto crema; al elegir, barra granulada + flechita ▶.
    /// Crea botones nuevos con ese estilo y convierte los que ya existen (sin rehacer las pantallas).
    /// </summary>
    public static class MenuStyleKit
    {
        const string GeneratedFolder = "Assets/_Voyager/Art/Generated";
        const string ArrowPath = GeneratedFolder + "/UI_Flecha.png";
        const string BarPath = GeneratedFolder + "/UI_BarraGranulada.png";

        public static readonly Color Cream = new Color32(216, 204, 170, 255);
        public static readonly Color CreamPressed = new Color32(184, 170, 133, 255);
        public static readonly Color DarkInk = new Color32(27, 23, 18, 255);
        public static readonly Color DarkPanel = new Color32(20, 17, 13, 252);

        /// <summary>Colores para botones sobre fondo oscuro (menú, pausa, final) o sobre papel claro.</summary>
        public struct Palette
        {
            public Color text, highlight, highlightText, pressed, arrow;
            public static Palette OnDark => new Palette { text = Cream, highlight = Cream, highlightText = DarkInk, pressed = CreamPressed, arrow = Cream };
            public static Palette OnPaper => new Palette
            {
                text = new Color32(43, 43, 43, 255), highlight = new Color32(43, 39, 33, 255),
                highlightText = new Color32(242, 232, 207, 255), pressed = new Color32(74, 68, 58, 255), arrow = new Color32(43, 39, 33, 255)
            };
        }

        // =====================================================================
        // Crear
        // =====================================================================
        /// <summary>Botón de menú nuevo: texto a la izquierda, barra detrás y flechita afuera, a la izquierda.</summary>
        internal static Button CreateMenuButton(string name, Transform parent, string text, Palette p, float fontSize = 40)
        {
            var bar = Img(name, parent, Color.clear, raycast: true);
            bar.sprite = Bar;
            bar.type = Image.Type.Simple;
            var button = bar.gameObject.AddComponent<Button>();
            button.targetGraphic = bar;

            var label = Txt("Txt_" + name.Replace("Btn_", ""), bar.transform, text, fontSize, TextAlignmentOptions.Left, p.text);
            Stretch(label.rectTransform, 22, 12, 0, 0);
            label.characterSpacing = 6;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            AddStyle(button, bar, label, p);
            return button;
        }

        /// <summary>Barra con valor (para Sonido), con los mismos colores del menú.</summary>
        internal static Slider CreateSlider(string name, Transform parent)
        {
            var root = UIObj(name, parent);
            var slider = root.AddComponent<Slider>();
            var track = Img("Pista", root.transform, new Color(Cream.r, Cream.g, Cream.b, 0.25f), raycast: true);
            Stretch(track.rectTransform, 0, 0, 14, 14);
            var fillArea = UIObj("Fill Area", root.transform);
            Stretch(Rt(fillArea), 0, 0, 14, 14);
            var fill = Img("Fill", fillArea.transform, Cream);
            Stretch(fill.rectTransform);
            var handleArea = UIObj("Handle Area", root.transform);
            Stretch(Rt(handleArea), 10, 10, 0, 0);
            var handle = Img("Handle", handleArea.transform, Cream, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(20, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            var colors = slider.colors;
            colors.highlightedColor = Color.white; colors.pressedColor = CreamPressed; colors.selectedColor = Color.white;
            slider.colors = colors;
            return slider;
        }

        // =====================================================================
        // Convertir botones que ya existen
        // =====================================================================
        /// <summary>Convierte un botón hecho con GameScreenBuilder.Btn al estilo nuevo. Si ya lo tiene, no hace nada.</summary>
        internal static void Restyle(Button button, Palette p)
        {
            if (button == null || button.GetComponent<MenuButtonStyle>() != null) return;
            Undo.RegisterFullObjectHierarchyUndo(button.gameObject, "Estilo de botón");

            var bar = button.GetComponent<Image>();
            bar.sprite = Bar;
            bar.color = Color.clear;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.Left;
                label.color = p.text;
                label.fontStyle = FontStyles.Normal;
                label.characterSpacing = 6;
                label.text = label.text.ToUpperInvariant();
                Stretch(label.rectTransform, 22, 12, 0, 0);
            }
            AddStyle(button, bar, label, p);
        }

        /// <summary>Pausa, pantalla final y carteles del juego con el estilo nuevo.</summary>
        internal static void RestyleGameScene(Transform canvas)
        {
            // Pausa: caja oscura + botones sobre oscuro
            var pauseBox = canvas.Find("UI_Pausa/Panel_Pausa/Caja");
            if (pauseBox != null)
            {
                var img = pauseBox.GetComponent<Image>();
                Undo.RecordObject(img, "Pausa"); img.color = DarkPanel;
                SetTextColor(pauseBox.Find("Txt_Pausa"), Cream);
                SetTextColor(pauseBox.Find("Txt_Ayuda"), new Color(Cream.r, Cream.g, Cream.b, 0.6f));
                Restyle(Button(pauseBox, "Botones/Btn_Reanudar"), Palette.OnDark);
                Restyle(Button(pauseBox, "Botones/Btn_MenuPrincipal"), Palette.OnDark);
            }

            // Pantalla final (sobre el fondo de estrellas)
            Restyle(Button(canvas, "UI_Final/Panel_Final/Btn_Reiniciar"), Palette.OnDark);
            Restyle(Button(canvas, "UI_Final/Panel_Final/Btn_MenuPrincipal"), Palette.OnDark);

            // Carteles de papel
            Restyle(Button(canvas, "UI_ResultadoDia/Panel_ResultadoDia/Caja/Btn_Continuar"), Palette.OnPaper);
            Restyle(Button(canvas, "UI_Narrativa/Panel_Narrativa/Caja/Btn_CerrarNarrativa"), Palette.OnPaper);
        }

        static Button Button(Transform root, string path)
        {
            var t = root.Find(path);
            return t != null ? t.GetComponent<Button>() : null;
        }

        static void SetTextColor(Transform t, Color c)
        {
            if (t == null || t.GetComponent<TMP_Text>() is not TMP_Text text) return;
            Undo.RecordObject(text, "Color"); text.color = c;
        }

        static void AddStyle(Button button, Image bar, TMP_Text label, Palette p)
        {
            button.transition = Selectable.Transition.None;

            var arrow = Img("Img_Flecha", button.transform, new Color(p.arrow.r, p.arrow.g, p.arrow.b, 0f));
            arrow.sprite = Arrow;
            arrow.preserveAspect = true;
            Place(arrow.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(24, 24));

            var style = button.gameObject.AddComponent<MenuButtonStyle>();
            style.highlight = bar;
            style.label = label;
            style.arrow = arrow;
            style.normalText = p.text;
            style.highlightText = p.highlightText;
            style.highlightColor = p.highlight;
            style.pressedColor = p.pressed;
        }

        // =====================================================================
        // Sprites generados (flechita y barra con grano, como papel viejo)
        // =====================================================================
        internal static Sprite Arrow => LoadOrCreate(ArrowPath, () =>
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // Triángulo apuntando a la derecha, con borde suavizado
                    float half = (s - 1) / 2f;
                    float maxDy = half * (1f - x / (float)(s - 1));
                    float d = maxDy - Mathf.Abs(y - half);
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(d + 0.5f)));
                }
            return tex;
        });

        internal static Sprite Bar => LoadOrCreate(BarPath, () =>
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float grain = 0.86f + 0.14f * (float)rng.NextDouble();
                    float edge = (y < 2 || y > h - 3) ? 0.9f : 1f; // bordes apenas gastados
                    tex.SetPixel(x, y, new Color(grain * edge, grain * edge, grain * edge, 1));
                }
            return tex;
        });

        static Sprite LoadOrCreate(string path, System.Func<Texture2D> draw)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;

            Directory.CreateDirectory(GeneratedFolder);
            var tex = draw();
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
