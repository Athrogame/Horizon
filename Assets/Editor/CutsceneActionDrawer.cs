using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CutsceneAction))]
public class CutsceneActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        float h = EditorGUIUtility.singleLineHeight + 2f; // Base Foldout

        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("actionType")) + 2f;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("waitToFinish")) + 4f;

        int typeIndex = property.FindPropertyRelative("actionType").enumValueIndex;

        if (typeIndex == (int)CutsceneActionType.Wait) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("waitDuration")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.MoveToTransform) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetToMove")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("destinationNode")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("moveSpeed")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.TeleportObject) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetToMove")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("destinationNode")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.ShowDialogue) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("dialogueBox")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("dialogueLines"), true) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.SetAnimationTrigger) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetAnimator")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("animationTriggerName")) + 2f;
        }

        return h + 4f; // Bottom padding
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        Rect rect = position;
        rect.height = EditorGUIUtility.singleLineHeight;

        int typeIndex = property.FindPropertyRelative("actionType").enumValueIndex;
        string dynamicLabel = "Action: " + property.FindPropertyRelative("actionType").enumDisplayNames[typeIndex];
        
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, dynamicLabel);
        
        if (property.isExpanded)
        {
            rect.y += EditorGUIUtility.singleLineHeight + 2f;

            SerializedProperty actionType = property.FindPropertyRelative("actionType");
            DrawProp(ref rect, actionType);
            rect.y += 2f; // Extra spacing

            SerializedProperty waitToFinish = property.FindPropertyRelative("waitToFinish");
            DrawProp(ref rect, waitToFinish);

            if (typeIndex == (int)CutsceneActionType.Wait) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("waitDuration"));
            }
            else if (typeIndex == (int)CutsceneActionType.MoveToTransform) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetToMove"), new GUIContent("Who To Move"));
                DrawProp(ref rect, property.FindPropertyRelative("destinationNode"), new GUIContent("Destination Waypoint"));
                DrawProp(ref rect, property.FindPropertyRelative("moveSpeed"));
            }
            else if (typeIndex == (int)CutsceneActionType.TeleportObject) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetToMove"), new GUIContent("Who To Teleport"));
                DrawProp(ref rect, property.FindPropertyRelative("destinationNode"), new GUIContent("Destination Node"));
            }
            else if (typeIndex == (int)CutsceneActionType.ShowDialogue) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("dialogueBox"));
                DrawProp(ref rect, property.FindPropertyRelative("dialogueLines"), null, true);
            }
            else if (typeIndex == (int)CutsceneActionType.SetAnimationTrigger) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetAnimator"));
                DrawProp(ref rect, property.FindPropertyRelative("animationTriggerName"));
            }
        }
        EditorGUI.EndProperty();
    }

    private void DrawProp(ref Rect rect, SerializedProperty prop, GUIContent content = null, bool includeChildren = false)
    {
        rect.height = EditorGUI.GetPropertyHeight(prop, includeChildren);
        if (content != null)
            EditorGUI.PropertyField(rect, prop, content, includeChildren);
        else
            EditorGUI.PropertyField(rect, prop, includeChildren);
        
        rect.y += rect.height + 2f;
    }
}
