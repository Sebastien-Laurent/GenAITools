using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class DialogueManager : MonoBehaviour
{

}

public class Dialogue
{
    private string iD;
    public string ID { get => iD; }

    private string[][] dialogueTable;

    public Dialogue(string dialogueID)
    {
        iD = dialogueID;
        dialogueTable = ResourcesManager.instance.FilterTable(ResourcesManager.instance.inputFile, 0, iD, false);
    }

    public int DialogueLength()
    {
        return dialogueTable.Length - 1;
    }

    public void MergeDialogue(string dialogueToMergeID)
    {
        dialogueTable = dialogueTable.Concat(ResourcesManager.instance.FilterTable(ResourcesManager.instance.inputFile, 0, dialogueToMergeID, false).Skip(1)).ToArray();
    }

    public string Text(int page,string language)
    {
        int textColumn = ResourcesManager.instance.ColumnFinder(ResourcesManager.instance.inputFile, language);
        return dialogueTable[page][textColumn];
    }

    public string SpeakerID(int page = 1)
    {
        int speakerColumn = ResourcesManager.instance.ColumnFinder(ResourcesManager.instance.inputFile, "SpeakerID");
        //Debug.Log(dialogueTable[page][speakerColumn]);
        return dialogueTable[page][speakerColumn];
    }

    public List<string> AnswerList()
    {
        int col = ResourcesManager.instance.ColumnFinder(ResourcesManager.instance.inputFile, "AnswerID");
        return dialogueTable[DialogueLength()][col].Split(",").ToList();
    }

    public string FollowUpDialogue()
    {
        int column = ResourcesManager.instance.ColumnFinder(ResourcesManager.instance.inputFile, "FollowUpDialogueID");
        return dialogueTable[DialogueLength()][column];
    }
}