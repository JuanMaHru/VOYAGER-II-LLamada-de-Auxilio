using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Muestra en el Inspector SOLO los campos que usa cada tipo de Requirement.
    /// Línea 1: Tipo. Línea 2: [Objetivo] [Operador] [Valor].
    /// </summary>
    [CustomPropertyDrawer(typeof(Requirement))]
    public class RequirementDrawer : PropertyDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Line * 2 + Gap * 2;

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(pos, label, property);
            var type = property.FindPropertyRelative("type");
            var comparison = property.FindPropertyRelative("comparison");
            var value = property.FindPropertyRelative("value");

            var r1 = new Rect(pos.x, pos.y, pos.width, Line);
            EditorGUI.PropertyField(r1, type, GUIContent.none);

            var r2 = new Rect(pos.x, pos.y + Line + Gap, pos.width, Line);
            var t = (RequirementType)type.enumValueIndex;

            string target = TargetField(t);
            bool numeric = t != RequirementType.FlagSet && t != RequirementType.FlagNotSet;

            if (target != null && numeric)
            {
                float w = r2.width;
                var a = new Rect(r2.x, r2.y, w * 0.5f - 2, Line);
                var b = new Rect(r2.x + w * 0.5f, r2.y, w * 0.2f - 2, Line);
                var c = new Rect(r2.x + w * 0.7f, r2.y, w * 0.3f, Line);
                EditorGUI.PropertyField(a, property.FindPropertyRelative(target), GUIContent.none);
                DrawComparison(b, comparison);
                EditorGUI.PropertyField(c, value, GUIContent.none);
            }
            else if (numeric)
            {
                float w = r2.width;
                DrawComparison(new Rect(r2.x, r2.y, w * 0.3f - 2, Line), comparison);
                EditorGUI.PropertyField(new Rect(r2.x + w * 0.3f, r2.y, w * 0.7f, Line), value, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(r2, property.FindPropertyRelative("flag"), new GUIContent("Flag"));
            }
            EditorGUI.EndProperty();
        }

        static readonly string[] Symbols = { ">=", ">", "==", "!=", "<=", "<" };

        static void DrawComparison(Rect r, SerializedProperty comparison)
            => comparison.enumValueIndex = EditorGUI.Popup(r, comparison.enumValueIndex, Symbols);

        static string TargetField(RequirementType t)
        {
            switch (t)
            {
                case RequirementType.StatValue: return "stat";
                case RequirementType.RegionRelation:
                case RequirementType.ApprovedTodayFromRegion:
                case RequirementType.RejectedTodayFromRegion:
                case RequirementType.ConsecutiveRejectionsOfRegion: return "region";
                case RequirementType.ApprovedTodayInCategory: return "category";
                default: return null;
            }
        }
    }

    /// <summary>Muestra en el Inspector SOLO los campos que usa cada tipo de Consequence.</summary>
    [CustomPropertyDrawer(typeof(Consequence))]
    public class ConsequenceDrawer : PropertyDrawer
    {
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;
        const int NarrativeLines = 3;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var t = (ConsequenceType)property.FindPropertyRelative("type").enumValueIndex;
            float second = t == ConsequenceType.ShowNarrative ? Line * NarrativeLines : Line;
            return Line + Gap + second + Gap;
        }

        public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(pos, label, property);
            var type = property.FindPropertyRelative("type");
            var amount = property.FindPropertyRelative("amount");

            EditorGUI.PropertyField(new Rect(pos.x, pos.y, pos.width, Line), type, GUIContent.none);
            var r2 = new Rect(pos.x, pos.y + Line + Gap, pos.width, Line);
            float w = r2.width;

            switch ((ConsequenceType)type.enumValueIndex)
            {
                case ConsequenceType.ModifyRegion:
                    EditorGUI.PropertyField(new Rect(r2.x, r2.y, w * 0.65f - 2, Line), property.FindPropertyRelative("region"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(r2.x + w * 0.65f, r2.y, w * 0.35f, Line), amount, GUIContent.none);
                    break;
                case ConsequenceType.ModifyStat:
                    EditorGUI.PropertyField(new Rect(r2.x, r2.y, w * 0.65f - 2, Line), property.FindPropertyRelative("stat"), GUIContent.none);
                    EditorGUI.PropertyField(new Rect(r2.x + w * 0.65f, r2.y, w * 0.35f, Line), amount, GUIContent.none);
                    break;
                case ConsequenceType.ModifyAllRegions:
                    EditorGUI.PropertyField(r2, amount, new GUIContent("Cantidad"));
                    break;
                case ConsequenceType.SetFlag:
                case ConsequenceType.ClearFlag:
                    EditorGUI.PropertyField(r2, property.FindPropertyRelative("flag"), new GUIContent("Flag"));
                    break;
                case ConsequenceType.ShowNarrative:
                    var text = property.FindPropertyRelative("text");
                    r2.height = Line * NarrativeLines;
                    var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                    text.stringValue = EditorGUI.TextArea(r2, text.stringValue, style);
                    break;
                case ConsequenceType.EndCampaign:
                    EditorGUI.PropertyField(r2, property.FindPropertyRelative("ending"), new GUIContent("Final (vacío = evaluar)"));
                    break;
            }
            EditorGUI.EndProperty();
        }
    }
}
