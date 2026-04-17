using UnityEngine.Events;

[System.Serializable]
public class QuestionOption
{
    public string label = "Yes";
    public UnityEvent onChosen;
}

[System.Serializable]
public class QuestionData
{
    public QuestionOption optionA = new QuestionOption { label = "Yes" };
    public QuestionOption optionB = new QuestionOption { label = "No" };
}
