using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Voyager.UI;
using Object = UnityEngine.Object;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Arma en la escena abierta TODA la pantalla del juego, con colores provisorios y todo conectado:
    /// cabecera (día, tema, condición, barra de tolerancia), mesa con 4 sobres, detalle, sellos,
    /// y los carteles de narrativa, resultado del día y final.
    /// Después se pueden cambiar colores por sprites, mover cosas, etc. sin tocar código.
    /// Menú: Voyager → Crear pantalla del juego.
    /// </summary>
    public static class GameScreenBuilder
    {
        const string CanvasName = "Canvas_Oficina";
        const string RealConfigPath = "Assets/_Voyager/Data/CampaignConfig.asset";
        const string TolerancePath = "Assets/_Voyager/Data/Stats/Stat_Tolerancia.asset";
        const int CardCount = 4;

        // ---- Paleta provisoria (oficina burocrática de la NASA) ----
        internal static readonly Color Navy = Hex("1B2536");
        internal static readonly Color Wood = Hex("5A3E2B");
        internal static readonly Color HeaderBg = new Color(0.07f, 0.09f, 0.14f, 0.95f);
        internal static readonly Color Paper = Hex("F2E8CF");
        internal static readonly Color Ink = Hex("2B2B2B");
        internal static readonly Color Gold = Hex("F2C14E");
        internal static readonly Color Green = Hex("2E8B57");
        internal static readonly Color Red = Hex("C0392B");
        internal static readonly Color Dim = new Color(0f, 0f, 0f, 0.65f);

        [MenuItem("Voyager/Crear pantalla del juego", priority = 2)]
        public static void Build()
        {
            if (!EnsureTmpEssentials(Build)) return;

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
            {
                if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Pantalla del juego",
                        $"Ya existe '{CanvasName}' en la escena. ¿Borrarlo y crearlo de nuevo?", "Crear de nuevo", "Cancelar"))
                    return;
                Undo.DestroyObjectImmediate(existing);
            }

            var campaign = EnsureGameSystems();
            var proposals = campaign.GetComponent<ProposalManager>();
            EnsureEventSystem();

            // ---------------- Canvas ----------------
            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Crear pantalla del juego");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // ---------------- Fondo ----------------
            Stretch(Img("Img_Fondo", root, Navy).rectTransform);
            var desk = Img("Img_Escritorio", root, Wood);
            Stretch(desk.rectTransform, 0, 0, 170, 0);

            // ---------------- Cabecera ----------------
            var header = UIObj("UI_Header", root);
            var headerRt = Rt(header);
            headerRt.anchorMin = new Vector2(0, 1); headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1); headerRt.sizeDelta = new Vector2(0, 170); headerRt.anchoredPosition = Vector2.zero;
            Stretch(Img("Fondo_Header", header.transform, HeaderBg).rectTransform);

            var txtDia = Txt("Txt_Dia", header.transform, "DÍA 1 / 8", 44, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
            Place(txtDia.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(420, 60));
            var txtCat = Txt("Txt_Categoria", header.transform, "CATEGORÍA", 30, TextAlignmentOptions.Left, Gold, FontStyles.Bold);
            Place(txtCat.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -85), new Vector2(420, 50));

            var txtCond = Txt("Txt_Condicion", header.transform, "Condición del día", 28, TextAlignmentOptions.Center, Color.white);
            Place(txtCond.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(900, 140));
            AutoSize(txtCond, 18, 30);

            var bar = Slider("Barra_Tolerancia", header.transform, out var fill);
            Place(Rt(bar.gameObject), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -25), new Vector2(460, 36));
            var txtTol = Txt("Txt_Tolerancia", header.transform, "Tolerancia del Comité", 24, TextAlignmentOptions.Right, Color.white);
            Place(txtTol.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -65), new Vector2(460, 40));
            var txtCount = Txt("Txt_Contadores", header.transform, "Aprobadas 0 · Rechazadas 0", 22, TextAlignmentOptions.Right, new Color(1, 1, 1, 0.75f));
            Place(txtCount.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -110), new Vector2(460, 40));

            var headerUI = header.AddComponent<DayHeaderUI>();
            headerUI.campaign = campaign;
            headerUI.dayText = txtDia; headerUI.categoryText = txtCat; headerUI.conditionText = txtCond; headerUI.countersText = txtCount;

            var tolBar = header.AddComponent<StatBarUI>();
            tolBar.campaign = campaign;
            tolBar.stat = AssetDatabase.LoadAssetAtPath<StatData>(TolerancePath);
            tolBar.slider = bar;
            tolBar.fillImage = fill;
            tolBar.valueText = txtTol;
            if (tolBar.stat == null) Debug.LogWarning("[Voyager] No encontré Stat_Tolerancia: la barra queda sin variable. Ejecutá Voyager → Actualizar tolerancia, finales y reacciones y asignala en UI_Header.");

            // ---------------- Mesa con sobres ----------------
            var mesa = UIObj("Mesa", root);
            Place(Rt(mesa), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -200), new Vector2(1600, 400));
            var layout = mesa.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 50; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var board = mesa.AddComponent<ProposalBoardUI>();
            board.proposalManager = proposals;
            for (int i = 0; i < CardCount; i++) board.cards.Add(Card($"Sobre_{i + 1}", mesa.transform));

            // ---------------- Detalle de la propuesta seleccionada ----------------
            var detail = Img("Panel_Detalle", root, Paper, raycast: true);
            Place(detail.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(60, 50), new Vector2(1180, 400));
            var dImg = Img("Img_Propuesta", detail.transform, Color.white);
            Place(dImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(300, 300));
            dImg.preserveAspect = true;
            var dTitle = Txt("Txt_Titulo", detail.transform, "Seleccioná un sobre", 40, TextAlignmentOptions.TopLeft, Ink, FontStyles.Bold);
            TopBand(dTitle.rectTransform, 350, 30, 25, 60);
            AutoSize(dTitle, 24, 40);
            var dRegion = Txt("Txt_Region", detail.transform, "", 28, TextAlignmentOptions.TopLeft, Ink);
            TopBand(dRegion.rectTransform, 350, 400, 95, 40);
            var dCat = Txt("Txt_Categoria", detail.transform, "", 24, TextAlignmentOptions.TopRight, new Color(0.3f, 0.3f, 0.3f), FontStyles.Italic);
            TopBand(dCat.rectTransform, 700, 30, 95, 40);
            var dDesc = Txt("Txt_Descripcion", detail.transform, "", 26, TextAlignmentOptions.TopLeft, Ink);
            Stretch(dDesc.rectTransform, 350, 30, 150, 30);
            var dOk = Stamp("Sello_Aprobado", detail.transform, "APROBADO", Green, -12);
            Place(Rt(dOk), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 90));
            var dNo = Stamp("Sello_Rechazado", detail.transform, "RECHAZADO", Red, 10);
            Place(Rt(dNo), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 90));

            dOk.SetActive(false);
            dNo.SetActive(false);
            var detailUI = detail.gameObject.AddComponent<ProposalCardUI>();
            detailUI.followSelection = true;
            detailUI.proposalManager = proposals;
            detailUI.titleText = dTitle; detailUI.regionText = dRegion; detailUI.categoryText = dCat; detailUI.descriptionText = dDesc;
            detailUI.proposalImage = dImg;
            detailUI.approvedStamp = dOk; detailUI.rejectedStamp = dNo;

            // ---------------- Sellos ----------------
            var sellos = UIObj("Sellos", root);
            Place(Rt(sellos), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 50), new Vector2(560, 400));
            var vl = sellos.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 30; vl.childControlWidth = vl.childControlHeight = true; vl.childForceExpandWidth = vl.childForceExpandHeight = true;
            var approve = Btn("Btn_Aprobar", sellos.transform, "SELLO APROBAR", Green, 44);
            var reject = Btn("Btn_Rechazar", sellos.transform, "SELLO DESAPROBAR", Red, 44);
            var stamps = sellos.AddComponent<StampButtonsUI>();
            stamps.proposalManager = proposals; stamps.approveButton = approve; stamps.rejectButton = reject;

            // ---------------- Cartel de narrativa ----------------
            var uiNarr = FullScreenHolder("UI_Narrativa", root);
            var narrPanel = Img("Panel_Narrativa", uiNarr.transform, Dim, raycast: true);
            Stretch(narrPanel.rectTransform);
            var narrBox = Box(narrPanel.transform, new Vector2(1000, 480));
            var narrText = Txt("Txt_Narrativa", narrBox, "", 32, TextAlignmentOptions.Center, Ink);
            Stretch(narrText.rectTransform, 50, 50, 50, 150);
            AutoSize(narrText, 20, 34);
            var narrBtn = Btn("Btn_CerrarNarrativa", narrBox, "Continuar", Navy, 32);
            Place(Rt(narrBtn.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 90));
            var narrUI = uiNarr.AddComponent<NarrativePopupUI>();
            narrUI.campaign = campaign; narrUI.panel = narrPanel.gameObject; narrUI.bodyText = narrText; narrUI.closeButton = narrBtn;
            narrPanel.gameObject.SetActive(false);

            // ---------------- Cartel de resultado del día ----------------
            var uiRes = FullScreenHolder("UI_ResultadoDia", root);
            var resPanel = Img("Panel_ResultadoDia", uiRes.transform, Dim, raycast: true);
            Stretch(resPanel.rectTransform);
            var resBox = Box(resPanel.transform, new Vector2(1100, 640));
            var resTitle = Txt("Txt_TituloResultado", resBox, "DÍA 1 — CONDICIÓN CUMPLIDA", 46, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            TopBand(resTitle.rectTransform, 40, 40, 40, 70);
            AutoSize(resTitle, 28, 46);
            var resSummary = Txt("Txt_Resumen", resBox, "", 30, TextAlignmentOptions.Center, Ink);
            TopBand(resSummary.rectTransform, 60, 60, 125, 130);
            var resNarr = Txt("Txt_NarrativaDia", resBox, "", 28, TextAlignmentOptions.Center, new Color(0.25f, 0.25f, 0.25f), FontStyles.Italic);
            Stretch(resNarr.rectTransform, 60, 60, 270, 150);
            var resBtn = Btn("Btn_Continuar", resBox, "Siguiente día", Navy, 32);
            Place(Rt(resBtn.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(400, 90));
            var resUI = uiRes.AddComponent<DayResultUI>();
            resUI.campaign = campaign; resUI.panel = resPanel.gameObject;
            resUI.titleText = resTitle; resUI.summaryText = resSummary; resUI.narrativeText = resNarr; resUI.continueButton = resBtn;
            resPanel.gameObject.SetActive(false);

            // ---------------- Pantalla final ----------------
            var uiFinal = FullScreenHolder("UI_Final", root);
            var finalPanel = Img("Panel_Final", uiFinal.transform, Navy, raycast: true);
            Stretch(finalPanel.rectTransform);
            var fTitle = Txt("Txt_TituloFinal", finalPanel.transform, "Título del final", 66, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            TopBand(fTitle.rectTransform, 100, 100, 50, 100);
            // Contenido apilado: si no hay imagen, el texto sube solo.
            var content = UIObj("Contenido_Final", finalPanel.transform);
            Stretch(Rt(content), 220, 220, 170, 170);
            var col = content.AddComponent<VerticalLayoutGroup>();
            col.spacing = 40; col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            var fImg = Img("Img_Final", content.transform, Color.white);
            fImg.preserveAspect = true;
            fImg.gameObject.AddComponent<LayoutElement>().preferredHeight = 260;
            var fDesc = Txt("Txt_DescripcionFinal", content.transform, "", 30, TextAlignmentOptions.Top, Color.white);
            var fReact = Txt("Txt_Reacciones", content.transform, "", 26, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.85f));
            var fBtn = Btn("Btn_Reiniciar", finalPanel.transform, "Jugar de nuevo", Green, 32);
            Place(Rt(fBtn.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(420, 90));
            var finalUI = uiFinal.AddComponent<EndingScreenUI>();
            finalUI.campaign = campaign; finalUI.panel = finalPanel.gameObject;
            finalUI.titleText = fTitle; finalUI.descriptionText = fDesc; finalUI.endingImage = fImg; finalUI.restartButton = fBtn;
            finalUI.reactionsText = fReact;
            finalPanel.gameObject.SetActive(false);

            AddMenuButtons(root, MainMenuBuilder.MenuSceneName);
            ArtSetup.ApplyToGame(root); // si existen Fondo y Texto Titulo en Assets/Art/UI

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[Voyager] Pantalla del juego creada. Guardá la escena (Ctrl+S) y apretá Play.");
        }

        // =====================================================================
        // Preparativos
        // =====================================================================
        /// <summary>Importa TMP Essential Resources si faltan (sin eso los textos no se ven).</summary>
        internal static bool EnsureTmpEssentials(Action whenReady)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;

            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string package = info != null ? info.resolvedPath + "/Package Resources/TMP Essential Resources.unitypackage" : null;
            if (package == null || !System.IO.File.Exists(package))
            {
                EditorUtility.DisplayDialog("Falta TextMeshPro",
                    "Importá los recursos de TextMeshPro: Window → TextMeshPro → Import TMP Essential Resources.\n" +
                    "Después volvé a apretar Voyager → Crear pantalla del juego.", "OK");
                return false;
            }

            Debug.Log("[Voyager] Importando TMP Essential Resources... Se continúa sola cuando termine.");
            pendingAfterTmp = whenReady;
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(package, false);
            return false;
        }

        static Action pendingAfterTmp;

        internal static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            var next = pendingAfterTmp;
            pendingAfterTmp = null;
            if (next != null) EditorApplication.delayCall += () => next();
        }

        // =====================================================================
        // Volver al menú: pausa con ESC (durante la partida) y botón en la pantalla final
        // =====================================================================
        /// <summary>
        /// Deja la escena del juego al día SIN rehacer la pantalla:
        /// quita el viejo botón "Menú" de la cabecera, agrega la pausa (ESC) y el botón
        /// "Menú principal" de la pantalla final. Lo que ya existe no se duplica.
        /// </summary>
        internal static void AddMenuButtons(Transform canvasRoot, string menuSceneName)
        {
            var oldHeaderButton = canvasRoot.Find("UI_Header/Btn_Menu");
            if (oldHeaderButton != null) Undo.DestroyObjectImmediate(oldHeaderButton.gameObject);

            AddPauseMenu(canvasRoot, menuSceneName);

            var finalPanel = canvasRoot.Find("UI_Final/Panel_Final");
            if (finalPanel != null && finalPanel.Find("Btn_MenuPrincipal") == null)
            {
                var restart = finalPanel.Find("Btn_Reiniciar") as RectTransform;
                if (restart != null) restart.anchoredPosition = new Vector2(-240, restart.anchoredPosition.y);
                var b = Btn("Btn_MenuPrincipal", finalPanel, "Menú principal", Hex("33415C"), 32);
                Place(Rt(b.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 50), new Vector2(420, 90));
                b.gameObject.AddComponent<BackToMenuButton>().menuSceneName = menuSceneName;
                Undo.RegisterCreatedObjectUndo(b.gameObject, "Botón menú principal");
            }
        }

        /// <summary>Pantalla de pausa: se abre con ESC. Va última en el Canvas para quedar encima de todo.</summary>
        static void AddPauseMenu(Transform canvasRoot, string menuSceneName)
        {
            if (canvasRoot.Find("UI_Pausa") != null) return;

            var holder = FullScreenHolder("UI_Pausa", canvasRoot);
            holder.transform.SetAsLastSibling();
            var panel = Img("Panel_Pausa", holder.transform, Dim, raycast: true);
            Stretch(panel.rectTransform);
            var box = Box(panel.transform, new Vector2(700, 560));
            var title = Txt("Txt_Pausa", box, "PAUSA", 64, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            TopBand(title.rectTransform, 40, 40, 50, 90);
            var hint = Txt("Txt_Ayuda", box, "Presioná ESC para volver al juego", 24, TextAlignmentOptions.Center,
                           new Color(0.35f, 0.35f, 0.35f), FontStyles.Italic);
            TopBand(hint.rectTransform, 40, 40, 140, 40);

            var buttons = UIObj("Botones", box);
            Place(Rt(buttons), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(460, 230));
            var col = buttons.AddComponent<VerticalLayoutGroup>();
            col.spacing = 30; col.childControlWidth = col.childControlHeight = true; col.childForceExpandWidth = col.childForceExpandHeight = true;
            var resume = Btn("Btn_Reanudar", buttons.transform, "Reanudar", Green, 36);
            var menu = Btn("Btn_MenuPrincipal", buttons.transform, "Menú principal", Hex("33415C"), 36);

            var pause = holder.AddComponent<PauseMenuUI>();
            pause.campaign = Object.FindAnyObjectByType<CampaignManager>();
            pause.panel = panel.gameObject;
            pause.resumeButton = resume;
            pause.menuButton = menu;
            pause.menuSceneName = menuSceneName;
            panel.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(holder, "Pantalla de pausa");
        }

        internal static CampaignManager EnsureGameSystems()
        {
            var cm = Object.FindAnyObjectByType<CampaignManager>();
            if (cm == null)
            {
                ExampleContentGenerator.CreateSystemsInScene();
                cm = Object.FindAnyObjectByType<CampaignManager>();
            }
            if (cm.GetComponent<ProposalManager>() == null)
            {
                var pm = Undo.AddComponent<ProposalManager>(cm.gameObject);
                var spm = new SerializedObject(pm);
                spm.FindProperty("campaign").objectReferenceValue = cm;
                spm.ApplyModifiedProperties();
            }

            // Usar la campaña real si existe.
            var real = AssetDatabase.LoadAssetAtPath<CampaignConfig>(RealConfigPath);
            var so = new SerializedObject(cm);
            var configProp = so.FindProperty("config");
            if (real != null && configProp.objectReferenceValue != real)
            {
                configProp.objectReferenceValue = real;
                Debug.Log($"[Voyager] {cm.name} ahora usa la campaña real ({RealConfigPath}).");
            }
            so.FindProperty("proposalManager").objectReferenceValue = cm.GetComponent<ProposalManager>();
            so.ApplyModifiedProperties();

            if (real != null && AssetDatabase.LoadAssetAtPath<StatData>(TolerancePath) == null)
                EndingsSetup.RunFromMenu(); // convierte Impresión → Tolerancia y carga finales/reacciones
            return cm;
        }

        /// <summary>El proyecto usa el Input System nuevo: el EventSystem necesita InputSystemUIInputModule.</summary>
        internal static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
                es = go.GetComponent<EventSystem>();
            }

            var moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType == null)
            {
                if (es.GetComponent<BaseInputModule>() == null) Undo.AddComponent<StandaloneInputModule>(es.gameObject);
                return;
            }
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            if (es.GetComponent(moduleType) == null) Undo.AddComponent(es.gameObject, moduleType);
        }

        // =====================================================================
        // Piezas
        // =====================================================================
        internal static ProposalCardUI Card(string name, Transform parent)
        {
            var card = UIObj(name, parent);
            Rt(card).sizeDelta = new Vector2(330, 390);

            var hl = Img("Highlight_Seleccion", card.transform, Gold);
            Stretch(hl.rectTransform, -10, -10, -10, -10);
            var bg = Img("Fondo", card.transform, Paper, raycast: true);
            Stretch(bg.rectTransform);

            var band = Img("Franja_Region", card.transform, Navy);
            TopBand(band.rectTransform, 0, 0, 0, 70);
            var icon = Img("Img_Region", band.transform, Color.white);
            Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(50, 50));
            icon.preserveAspect = true;
            var region = Txt("Txt_Region", band.transform, "REGIÓN", 28, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(region.rectTransform, 60, 10, 0, 0);

            var title = Txt("Txt_Titulo", card.transform, "Propuesta", 34, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            Stretch(title.rectTransform, 20, 20, 90, 80);
            AutoSize(title, 18, 36);
            var cat = Txt("Txt_Categoria", card.transform, "", 22, TextAlignmentOptions.Bottom, new Color(0.35f, 0.35f, 0.35f), FontStyles.Italic);
            Stretch(cat.rectTransform, 20, 20, 300, 20);

            var ok = Stamp("Sello_Aprobado", card.transform, "APROBADO", Green, -15);
            Place(Rt(ok), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(280, 90));
            var no = Stamp("Sello_Rechazado", card.transform, "RECHAZADO", Red, 12);
            Place(Rt(no), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(280, 90));

            var button = card.AddComponent<Button>();
            button.targetGraphic = bg;

            var ui = card.AddComponent<ProposalCardUI>();
            ui.titleText = title; ui.regionText = region; ui.categoryText = cat;
            ui.regionIcon = icon; ui.regionColorTarget = band;
            ui.selectButton = button; ui.selectedHighlight = hl.gameObject;
            ui.approvedStamp = ok; ui.rejectedStamp = no;
            hl.gameObject.SetActive(false);
            ok.SetActive(false);
            no.SetActive(false);
            return ui;
        }

        internal static GameObject Stamp(string name, Transform parent, string label, Color color, float angle)
        {
            var img = Img(name, parent, new Color(color.r, color.g, color.b, 0.9f));
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            var t = Txt("Txt_" + name, img.transform, label, 40, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(t.rectTransform);
            return img.gameObject;
        }

        internal static Slider Slider(string name, Transform parent, out Image fill)
        {
            var bg = Img(name, parent, new Color(1, 1, 1, 0.15f), raycast: false);
            var slider = bg.gameObject.AddComponent<Slider>();
            var area = UIObj("Fill Area", bg.transform);
            Stretch(Rt(area), 4, 4, 4, 4);
            fill = Img("Fill", area.transform, Green);
            Stretch(fill.rectTransform);
            slider.fillRect = fill.rectTransform;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0; slider.maxValue = 100; slider.value = 40;
            return slider;
        }

        internal static Transform Box(Transform parent, Vector2 size)
        {
            var box = Img("Caja", parent, Paper, raycast: true);
            Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return box.transform;
        }

        internal static GameObject FullScreenHolder(string name, Transform parent)
        {
            var go = UIObj(name, parent);
            Stretch(Rt(go));
            return go;
        }

        // =====================================================================
        // Helpers de UI
        // =====================================================================
        internal static GameObject UIObj(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        internal static Image Img(string name, Transform parent, Color color, bool raycast = false)
        {
            var img = UIObj(name, parent).AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        internal static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, TextAlignmentOptions align,
                                   Color color, FontStyles style = FontStyles.Normal)
        {
            var t = UIObj(name, parent).AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        internal static void AutoSize(TextMeshProUGUI t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        internal static Button Btn(string name, Transform parent, string label, Color color, float fontSize)
        {
            var img = Img(name, parent, color, raycast: true);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            b.colors = colors;
            var t = Txt("Txt_" + name.Replace("Btn_", ""), img.transform, label, fontSize, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Stretch(t.rectTransform, 10, 10, 5, 5);
            AutoSize(t, 18, fontSize);
            return b;
        }

        internal static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Franja pegada arriba: margen izq/der, distancia desde arriba y alto.</summary>
        internal static void TopBand(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
