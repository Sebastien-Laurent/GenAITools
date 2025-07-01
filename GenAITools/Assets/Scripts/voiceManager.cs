using ElevenLabs.Voices;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using ElevenLabs;
using TMPro;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using System.Diagnostics;

public class voiceManager : MonoBehaviour
{
    [SerializeField] private string configPath;

    [SerializeField]
    private Voice voice;
    private Model model;
    [SerializeField]
    private List<Speaker> speakers;

    [TextArea(3, 10)]
    [SerializeField]
    private string message;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private TMP_InputField dialogueIF;
    [SerializeField]
    private TextMeshProUGUI dialogueText;

    [SerializeField]
    private TextMeshProUGUI tokens;
    [SerializeField]
    private TextMeshProUGUI dialogueGenerated;

    [SerializeField]
    private TMP_Dropdown speakerDropdown;

    [SerializeField]
    private TMP_Dropdown languageDropdown;

    [SerializeField]
    private TMP_InputField outputFilePathField;
    [SerializeField]
    private TMP_InputField tableFileNameField;

    [SerializeField]
    private TMP_InputField sourcefilePathField;

    [SerializeField]
    private string outputfilePath = "";

    [SerializeField]
    private string sourcefilePath = "";

    [SerializeField]
    private string tableFileName = "";

    private string[][] dialogueClipTable;
    [SerializeField]
    private List<DialogueClip> dialogueClips;

    private CancellationTokenSource lifetimeCancellationTokenSource;

    [SerializeField] private Toggle onlyAddNewDialoguesToggle;

    [SerializeField] Image statusIcon;

    private ElevenLabsClient Api;

    private ElevenLabs.History.HistoryInfo historyInfo;

    private void OnValidate()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private async void Start()
    {

        dialogueClips = new List<DialogueClip>();
        LoadOutputPath();
        LoadTableFileName();

        int s = 0;
        dialogueGenerated.text = s.ToString();

        OnValidate();
        lifetimeCancellationTokenSource = new CancellationTokenSource();

        try
        {
            string key = GetAPIKeyFromConfigFile(configPath);
            UnityEngine.Debug.Log("key:" + key);
            Api = new ElevenLabsClient(new ElevenLabsAuthentication(key));
            var userInfo = await Api.UserEndpoint.GetUserInfoAsync();
            tokens.text = userInfo.SubscriptionInfo.CharacterCount.ToString();
            statusIcon.color = Color.green;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError(e);
        }
    }

    private string GetAPIKeyFromConfigFile(string directory, string filename = ".elevenlabs")
    {
        var currentDirectory = new DirectoryInfo(directory);

        var filePath = Path.Combine(currentDirectory.FullName, filename);

        if (File.Exists(filePath))
        {

            var lines = File.ReadAllLines(filePath);
            string apiKey = null;

            foreach (var line in lines)
            {
                var parts = line.Split('=', ':');

                for (var i = 0; i < parts.Length - 1; i++)
                {
                    var part = parts[i];
                    var nextPart = parts[i + 1];
                    UnityEngine.Debug.Log(part);
                    UnityEngine.Debug.Log(nextPart);
                    apiKey = part.Trim() switch
                    {
                        "apiKey" => nextPart.Trim(),
                        _ => apiKey
                    };
                }
            }
            UnityEngine.Debug.Log("hi");
            return apiKey;
        }

        return "";
    }

    public void LoadSourceFile()
    {
        ResourcesManager.instance.LoadResources(sourcefilePath);
    }

    //max page is 1000
    private async Task<ElevenLabs.History.HistoryInfo> GetHistory(int page, string startID = "")
    {
        if (startID != "")
        {
            ElevenLabs.History.HistoryInfo info = await Api.HistoryEndpoint.GetHistoryAsync(page, startID);

            UnityEngine.Debug.Log("items:" + info.HistoryItems.Count + " last id " + info.HistoryItems[info.HistoryItems.Count - 1].Id + " text:" + info.HistoryItems[info.HistoryItems.Count - 1].Text + " voice:" + info.HistoryItems[info.HistoryItems.Count - 1].VoiceName);
            return info;
        }
        else
        {
            ElevenLabs.History.HistoryInfo info = await Api.HistoryEndpoint.GetHistoryAsync(page);

            UnityEngine.Debug.Log("items:" + info.HistoryItems.Count + " last id " + info.HistoryItems[info.HistoryItems.Count - 1].Id + " text:" + info.HistoryItems[info.HistoryItems.Count - 1].Text + " voice:" + info.HistoryItems[info.HistoryItems.Count - 1].VoiceName);
            return info;
        }
    }

    private async Task<ElevenLabs.History.HistoryItem> GetHistoryItem(string id)
    {
        Stopwatch stopwatch = new Stopwatch(); // Create a stopwatch instance
        stopwatch.Start(); // Start the stopwatch

        var item = await Api.HistoryEndpoint.GetHistoryItemAsync(id);
        
        stopwatch.Stop(); // Stop the stopwatch
        UnityEngine.Debug.Log(id+": "+item.Text);
        UnityEngine.Debug.Log($"GetClipFromAPI took {stopwatch.ElapsedMilliseconds} ms.");
        
        return item;
    }

    private async Task<VoiceClip> GetHistoryClip(ElevenLabs.History.HistoryItem item)
    {
        return await Api.HistoryEndpoint.DownloadHistoryAudioAsync(item);
    }

    [SerializeField] private List<ElevenLabs.History.HistoryItem> historyItems;

    public async void DownloadHistory()
    {
        await DownloadHistoryTask();
    }

    public async Task DownloadHistoryTask()
    {
        UnityEngine.Debug.Log("starting to download history");
        statusIcon.color = Color.yellow;
        historyItems = new List<ElevenLabs.History.HistoryItem>();
        ElevenLabs.History.HistoryInfo info = await GetHistory(1000);
        historyItems.AddRange(info.HistoryItems);
        int nItems = info.HistoryItems.Count;
        while (nItems == 1000)
        {
            info = await GetHistory(1000, info.HistoryItems.Last().Id);
            nItems = info.HistoryItems.Count;
            historyItems.AddRange(info.HistoryItems);
            UnityEngine.Debug.Log("history size:"+historyItems.Count);
        }
        UnityEngine.Debug.Log("history downloaded");
        statusIcon.color = Color.green;
    }


    private void PrintIDs()
    {
        foreach (var item in historyInfo.HistoryItems.OrderBy(historyItem => historyItem.Date))
        {
            UnityEngine.Debug.Log(item.Id);
        }

    }

    private bool ClipAvailableInHistory()
    {
        if(historyItems == null)
        {
            return false;
        }

        foreach (var item in historyItems.OrderBy(historyItem => historyItem.Date))
        {
            if (item.Text == message && item.VoiceId == voice.Id)
            {
                return true;
            }
        }

        return false;
    }


    private ElevenLabs.History.HistoryItem HistoryItem(string text, string voice)
    {
        foreach (var item in historyItems.OrderBy(historyItem => historyItem.Date))
        {
            if (item.Text == text && item.VoiceId == voice)
            {
                return item;
            }
        }
        return default;
    }

    private async Task Play(string id, int page)
    {
        lifetimeCancellationTokenSource = new CancellationTokenSource();

        try
        {
            if (voice == null)
            {
                voice = (await Api.VoicesEndpoint.GetAllVoicesAsync(lifetimeCancellationTokenSource.Token)).FirstOrDefault();
            }

            
            ElevenLabs.Models.Model _model = default;
            if(model == Model.v1)
             _model = new ElevenLabs.Models.Model("eleven_multilingual_v1");
            else
            {
              _model = new ElevenLabs.Models.Model("eleven_multilingual_v2");
            }
            var clipOffset = 0;
            var streamCallbackSuccessful = false;

            VoiceClip clip = default;


            if (ClipAvailableInHistory())
            {
                string destinationFilePath = Path.Combine(DestinationFolderPath(), HistoryItem(message, voice).Id+".mp3");
                if (!File.Exists(destinationFilePath))
                {
                    UnityEngine.Debug.Log("Clip missing, loading clip from history");
                    clip = await GetHistoryClip(HistoryItem(message, voice));
                }
                else
                {
                    return;
                }
            }
            else
            {
                clip = await Api.TextToSpeechEndpoint.TextToSpeechAsync(
                               message,
                               voice,
                               null,
                               _model);
            }

            string clipPath = clip.CachedPath;
            UnityEngine.Debug.Log("clip id : "+clip.Id+ " clip path:"+clipPath);
            audioSource.clip = clip.AudioClip;

            string fileName = clip.Id;//clipPath.Remove(0, outputfilePath.Count());
            UnityEngine.Debug.Log(fileName);
            int l = languageDropdown.value;
            //save clip info to dialoguetable
            if (dialogueClips.Find(x => x.DialogueID == id & x.Page == page) == null)
            {
                dialogueClips.Add(new DialogueClip(id, page, voice, fileName + ".mp3", l));
            }
            else
            {
                dialogueClips.Find(x => x.DialogueID == id & x.Page == page).ClipNames[l] = fileName+".mp3";
            }

            //copy generated file to production folder
            CopyFileToDestination(clipPath, DestinationFolderPath());

            //play clip
            if (streamCallbackSuccessful)
            {
                UnityEngine.Debug.Log($"Stream complete {clip.AudioClip.samples}");

                if (clipOffset != clip.AudioClip.samples)
                {
                    UnityEngine.Debug.LogWarning($"offset by {clip.AudioClip.samples - clipOffset}");
                }
            }
            else
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.PlayOneShot(clip.AudioClip);
                }
            }

            var userInfo = await Api.UserEndpoint.GetUserInfoAsync();
            tokens.text = userInfo.SubscriptionInfo.CharacterCount.ToString();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError(e);
        }

    }

    public async void PlayDialogue()
    {
        Dialogue dialogue = new Dialogue(dialogueIF.text);
        message = TextForVoiceGeneration(dialogue.Text(1, Language()));

        dialogueText.text = message;
        voice = speakers.Find(x => x.SpeakerID == dialogue.SpeakerID()).voice;

        if (ClipAvailableInHistory())
        {
            UnityEngine.Debug.Log("text already generated");
            return;
        }
        await Play(dialogueIF.text,1);
    }

    private void OnDestroy()
    {
        lifetimeCancellationTokenSource?.Cancel();
    }

    public void LoadSourcePath()
    {
        if (PlayerPrefs.GetString("sourcePath") != null)
        {
            sourcefilePath = PlayerPrefs.GetString("sourcePath");
            sourcefilePathField.text = outputfilePath;
        }
    }

    public void LoadOutputPath()
    {
        if (PlayerPrefs.GetString("outputFilePath") != null)
        {
            outputfilePath = PlayerPrefs.GetString("outputFilePath");
            outputFilePathField.text = outputfilePath;
        }
    }

    public void LoadTableFileName()
    {
        if (PlayerPrefs.GetString("tableFileName") != null)
        {
            tableFileName = PlayerPrefs.GetString("tableFileName");
            tableFileNameField.text = tableFileName;
        }
    }

    public void SaveOutputPath()
    {
        outputfilePath = outputFilePathField.text;
        PlayerPrefs.SetString("outputFilePath", outputfilePath);
        PlayerPrefs.Save();
    }

    public void SaveTableFileName()
    {
        tableFileName= tableFileNameField.text;
        PlayerPrefs.SetString("tableFileName", tableFileName);
        PlayerPrefs.Save();
    }

    public void SaveSourcePath()
    {
        sourcefilePath = sourcefilePathField.text;
        PlayerPrefs.SetString("sourcePath", sourcefilePath);
        PlayerPrefs.Save();
    }

    //generate a new table todo save full table
    public void SaveTable()
    {
        dialogueClipTable = new string[dialogueClips.Count + 1][];
        dialogueClipTable[0] = new string[] { "DialogueID", "VoiceID", "Page", "Clip_L1","Clip_L2", "Clip_L3" , "Clip_L4" , "Clip_L5" , "Clip_L6" };
        for (int i = 0; i < dialogueClips.Count; i++)
        {
            dialogueClipTable[i + 1] = dialogueClips[i].ToArray();
        }

        string fileName = outputfilePath + tableFileName;

        UnityEngine.Debug.Log("Saving data at path:" + fileName);
        TextWriter tw = new StreamWriter(fileName, false);

        int lineNumber = dialogueClipTable.Length;
        for (int i = 0; i < lineNumber; i++)
        {
            int lineLength = dialogueClipTable[i].Length;
            string lineToWrite = "";
            for (int j = 0; j < lineLength; j++)
            {
                lineToWrite += dialogueClipTable[i][j] + ";";
            }
            tw.WriteLine(lineToWrite);
        }
        tw.Close();
    }

    public void LoadTable()
    {
        // Initialize a List to store the data temporarily
        List<string[]> tempData = new List<string[]>();

        // Build the file path
        string fileName = outputfilePath + tableFileName;

        // Debug message to indicate the loading process
        UnityEngine.Debug.Log("Loading data from path: " + fileName);

        // Check if the file exists
        if (File.Exists(fileName))
        {
            // Read the file line by line
            using (StreamReader sr = new StreamReader(fileName))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    // Split the line into an array using ';' as a delimiter
                    string[] lineArray = line.Split(';');

                    // Add the array to the temporary data list
                    tempData.Add(lineArray);
                }
            }

            // Convert the list to an array
            dialogueClipTable = tempData.ToArray();

            // Debug message to indicate loading completion
            UnityEngine.Debug.Log("Data loaded successfully. Table of length" + dialogueClipTable.Length);

        }
        else
        {
            UnityEngine.Debug.LogError("File not found: " + fileName);
        }
    }

    private bool IsDialogueAlreadyGenerated(string dialogueID, string targetPage, int language)
    {
        int page = int.Parse(targetPage);

        if (ClipAvailableInHistory())
        {
            UnityEngine.Debug.Log("text same as in history, no need to regenerate the clip");

            //adding clip data in dialogueclips so that it gets saved in dialogue table
            if (dialogueClips.Find(x => x.DialogueID == dialogueID & x.Page == page) == null)
            {
                dialogueClips.Add(new DialogueClip(dialogueID, page, voice, HistoryItem(message, voice).Id + ".mp3", language - 1));
            }
            else
            {
                dialogueClips.Find(x => x.DialogueID == dialogueID & x.Page == page).ClipNames[language - 1] = HistoryItem(message, voice).Id + ".mp3";
            }

            return true;
        }
        else
        {
            UnityEngine.Debug.Log("text has changed, regenerate the clip is needed");
            return false;
        }
    }

    public async void GenerateAllDialogues()
    {
        ResourcesManager rM = ResourcesManager.instance;
        int s = 0;
        int pageColumn = ResourcesManager.instance.ColumnFinder(ResourcesManager.instance.inputFile, "Page");
        dialogueGenerated.text = s.ToString();
        for (int i = 1; i < rM.inputFile.Length; i++)
        {
            Dialogue d = new Dialogue(rM.inputFile[i][0]);

            if (rM.inputFile[i][pageColumn] == null || rM.inputFile[i][pageColumn] == "")
            {
                UnityEngine.Debug.LogError("Page missing at line" + i+ ". Line skipped");
                continue;
            }

            int page = int.Parse(rM.inputFile[i][pageColumn]);

            if (d.SpeakerID()==speakerDropdown.options[speakerDropdown.value].text)
            {   
                s += 1;
                dialogueGenerated.text = s.ToString();

                message = TextForVoiceGeneration(d.Text(page, Language()));

                dialogueText.text = message;
                voice = speakers.Find(x => x.SpeakerID == d.SpeakerID()).voice;
                model = speakers.Find(x => x.SpeakerID == d.SpeakerID()).model;
                UnityEngine.Debug.Log("Line "+i+" "+"Playing dialogue " + d.ID + " page " + page+" Language "+Language());
                statusIcon.color = Color.yellow;

                bool isGen = IsDialogueAlreadyGenerated(rM.inputFile[i][0], rM.inputFile[i][pageColumn], LanguageID());
                await Play(d.ID, page);

                statusIcon.color = Color.green;
            }
        }

        statusIcon.color = Color.green;
    }

    private string TextForVoiceGeneration(string input)
    {
        message = input.Replace("$childname", "");
        if (Language() == "L1")
        {
            message = message.Replace("$parentnickname", "ton parent");
            message = message.Replace("Ces ", "çé ");
            message = message.Replace("Ca ", "ça ");
            message = message.Replace(" ce ", " çe ");
            message = message.Replace("Ce ", " çe ");
            message = message.Replace("(e)", "");
        }
        else if (Language() == "L2")
        {
            message = message.Replace("$parentnickname", "your parent");
        }
        else if (Language() == "L3")
        {
            message = message.Replace("$parentnickname", "tu progenitor");
        }
        else if (Language() == "L4")
        {
            message = message.Replace("$parentnickname", "あなたの親");
        }
        else if (Language() == "L5")
        {
            message = message.Replace("$parentnickname", "deine Eltern");
        }
        else if (Language() == "L6")
        {
            message = message.Replace("$parentnickname", "il tuo genitore");
        }
        else if (Language() == "L7")
        {
            message = message.Replace("$parentnickname", "seu responsável");
        }

        message = message.Replace(Environment.NewLine, "");
        message = message.Replace("\\n", "");
        message = message.Replace("\n", "");
        message = message.Replace("\r", "");
        message = message.Replace("<1>", "");
        message = message.Replace("</1>", "");
        message = RemoveTags(message);

        return message;
    }

    private void CopyFileToDestination(string sourceFilePath, string destinationFolderPath)
    {
        // Check if the source file exists
        if (File.Exists(sourceFilePath))
        {
            // Get the source file name
            string sourceFileName = Path.GetFileName(sourceFilePath);

            // Create the destination path with the source file name
            string destinationFilePath = Path.Combine(destinationFolderPath, sourceFileName);

            if (File.Exists(destinationFilePath))
            {
                UnityEngine.Debug.Log("File already exists");
            }
            else
            {
                File.Copy(sourceFilePath, destinationFilePath, true);

                UnityEngine.Debug.Log("File copied successfully!");
            }
            // Copy the file to the destination folder
        }
        else
        {
            UnityEngine.Debug.LogError("Source file not found!");
        }
    }

    private string DestinationFolderPath()
    {
        return outputfilePath + "/" + Language() + "/";
    }

    public string Language()
    {
        return languageDropdown.options[languageDropdown.value].text;
    }

    public int LanguageID()
    {
        return languageDropdown.value+1;
    }

    public string RemoveTags(string input)
    {
        // Regular expression pattern to match tags in the form of "##some_tag "
        string pattern = @"##\w+\s";

        // Replace all matches of the pattern with an empty string
        return Regex.Replace(input, pattern, string.Empty);
    }

    [Serializable]
    public class Speaker
    {
        public string SpeakerID;
        public Voice voice;
        public Model model;
    }

    [Serializable]
    public class DialogueClip
    {
        public string DialogueID;
        public int Page;
        public string VoiceID;
        public List<string> ClipNames = new() { "", "", "", "", "", "", "" };

        public string[] ToArray()
        {
            List<string> l = new () { DialogueID, VoiceID,Page.ToString(), ClipNames[0], ClipNames[1], ClipNames[2], ClipNames[3], ClipNames[4], ClipNames[5], ClipNames[6] };
            return l.ToArray();
        }

        public DialogueClip(string dialogueID, int page, string voiceID, string clipName, int language)
        {
            DialogueID = dialogueID;
            Page = page;
            VoiceID = voiceID;
            ClipNames[language] = clipName;
        }
    }
}

public enum Model
{
    v1=1,
    v2=2
}
