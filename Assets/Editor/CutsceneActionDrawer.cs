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
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("useTweening")) + 2f;
            if (property.FindPropertyRelative("useTweening").boolValue)
            {
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("tweenCurve")) + 2f;
            }
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
        else if (typeIndex == (int)CutsceneActionType.SetAnimationBool) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetAnimator")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("animationBoolName")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("animationBoolValue")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.PlayAnimationState) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetAnimator")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("animationClip")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.SetActive) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("targetGameObject")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("setActiveState")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.CameraShake) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("shakeDuration")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("shakeMagnitude")) + 2f;
        }
        else if (typeIndex == (int)CutsceneActionType.ChangeCameraTarget) 
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("virtualCamera")) + 2f;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("cameraFollowTarget")) + 2f;
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
                DrawProp(ref rect, property.FindPropertyRelative("useTweening"));
                if (property.FindPropertyRelative("useTweening").boolValue)
                {
                    DrawProp(ref rect, property.FindPropertyRelative("tweenCurve"));
                }
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
            else if (typeIndex == (int)CutsceneActionType.SetAnimationBool) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetAnimator"));
                DrawProp(ref rect, property.FindPropertyRelative("animationBoolName"));
                DrawProp(ref rect, property.FindPropertyRelative("animationBoolValue"));
            }
            else if (typeIndex == (int)CutsceneActionType.PlayAnimationState) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetAnimator"));
                DrawProp(ref rect, property.FindPropertyRelative("animationClip"));
            }
            else if (typeIndex == (int)CutsceneActionType.SetActive) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("targetGameObject"));
                DrawProp(ref rect, property.FindPropertyRelative("setActiveState"));
            }
            else if (typeIndex == (int)CutsceneActionType.CameraShake) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("shakeDuration"));
                DrawProp(ref rect, property.FindPropertyRelative("shakeMagnitude"));
            }
            else if (typeIndex == (int)CutsceneActionType.ChangeCameraTarget) 
            {
                DrawProp(ref rect, property.FindPropertyRelative("virtualCamera"), new GUIContent("Virtual Camera"));
                DrawProp(ref rect, property.FindPropertyRelative("cameraFollowTarget"), new GUIContent("Follow Target"));
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
