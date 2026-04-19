using System.Collections.Generic;
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
    public List<QuestionOption> options = new List<QuestionOption>
    {
        new QuestionOption { label = "Yes" },
        new QuestionOption { label = "No" }
    };
}
