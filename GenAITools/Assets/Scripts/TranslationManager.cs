using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Threads;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System;
using Utilities.WebRequestRest.Interfaces;
using TMPro;
using System.IO;

public class TranslationManager : MonoBehaviour
{
    [SerializeField] private string configPath;
    [SerializeField] private bool stop;

    private OpenAIClient api;
    private AssistantResponse assistant;
    private ThreadResponse currentThread;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private TMP_Dropdown fileDropdown;
    [SerializeField] private TMP_InputField instructionsField;
    [SerializeField] private TMP_InputField inputFilePathField;

    private void Start()
    {
        OpenAIAuthentication auth = new OpenAIAuthentication().LoadFromPath(configPath);

        Debug.Log(auth.Info.ApiKey);

        api = new OpenAIClient(auth);

        LoadOutputPath();
        LoadTableFileName();
    }

    public void LoadInputFile()
    {
        if (string.IsNullOrWhiteSpace(inputFilePathField.text))
        {
            agentOutput.text= "missing input file name";

            return;
        }

        ResourcesManager.instance.LoadResources(inputFilePathField.text);

        agentOutput.text = "input file loaded of size:" + ResourcesManager.instance.inputFile.Length;
    }


    private string[][] SelectedFile()
    {

        if(ResourcesManager.instance.inputFile == null)
        {
            agentOutput.text = "input file " + inputFilePathField.text + " is empty";
        }

        return ResourcesManager.instance.inputFile;
    }

    private string SelectedLanguage()
    {

        if(languageDropdown.value==0)
            return "L1";
        if (languageDropdown.value == 1)
            return "L2";
        if (languageDropdown.value == 2)
            return "L3";
        if (languageDropdown.value == 3)
            return "L4";
        if (languageDropdown.value == 4)
            return "L5";
        if (languageDropdown.value == 5)
            return "L6";
        if (languageDropdown.value == 6)
            return "L7";
        Debug.LogWarning("unknown selected language");
        return "L2";
    }

    private string SelectedLanguageFullName()
    {
        if (languageDropdown.value == 0)
            return "french";
        if (languageDropdown.value == 1)
            return "english";
        if (languageDropdown.value == 2)
            return "spanish";
        if (languageDropdown.value == 3)
            return "japanese";
        if (languageDropdown.value == 4)
            return "german";
        if (languageDropdown.value == 5)
            return "italian";
        if (languageDropdown.value == 6)
            return "brazilian portuguese";

        Debug.LogWarning("unknown selected language");
        return "english";
    }

    private string FileInstruction()
    {
        if (string.IsNullOrWhiteSpace(instructionsField.text))
        {
            agentOutput.text += "\nNo instructions provided";
        }

        return " Context : "+instructionsField.text;
    }


    private async Task CreateAssistant()
    {
        assistant = await api.AssistantsEndpoint.CreateAssistantAsync(
            new CreateAssistantRequest(
                name: "Translator",
                instructions: "You re an agent conceived to translate text, user will send you text to translate in various languages, use the tool Translate to output the translated texts",
                model: OpenAI.Models.Model.GPT4oMini,
                tools: Tools()
                )
            );

        Debug.Log("assistant created");
    }

    [SerializeField] private TMP_InputField userInput;
    [SerializeField] private TextMeshProUGUI agentOutput;
    private bool agentIsBusy = false;

    public async void SendMessage()
    {

        if(assistant == null)
        {
            await CreateAssistant();
        }

        if (!agentIsBusy)
        {
            agentIsBusy = true;
            await Request(userInput.text);
        }
        else
        {
            Debug.Log("Can't send message, agent is busy");
        }
    }

    [SerializeField] private int currentLine = 1;
    [SerializeField] private int batchSize = 10;

    public void TranslateBatch()
    {
        TranslateAsync(Mathf.Min(currentLine+batchSize, SelectedFile().Length));
    }

    public void FullTranslation()
    {
        TranslateAsync(SelectedFile().Length);
    }

    private Dictionary<string, int> textIDsMapping;

    private async void TranslateAsync(int endLine)
    {
        agentOutput.text += "\n Starting translation";

        if (assistant == null)
        {
            await CreateAssistant();
        }

        if (!agentIsBusy)
        {
            agentIsBusy = true;
            var tR = SelectedFile();

            int colL1 = -1;
            int colLT = FindOrCreateLanguageColumn(tR, SelectedLanguage());

            textIDsMapping = new();

            for (int i = 0; i < tR[0].Length; i++)
            {
                if (tR[0][i] == "L1")
                {
                    colL1 = i;
                }
            }

            if (colL1 == -1)
            {
                Debug.LogWarning("could not find the original text column");
                agentIsBusy = false;
                return;
            }

            while (currentLine < endLine)
            {
                string instructions = "Translate the following textsin " + SelectedLanguageFullName() + ". Each line is the textID and the associated text to translate that is within [START] and [END]. Keep the tags <xxxx>, ##xxxxx and $xxxx unmodified. Keep \" characters if present ";
                string translationStringBatch = instructions + FileInstruction() + "\n";

                int nLines = 0;
                int i = currentLine;
                while (nLines < batchSize && i < endLine)
                {
                    if (string.IsNullOrEmpty(tR[i][colLT]) && !string.IsNullOrEmpty(tR[i][colL1]))
                    {
                        translationStringBatch += "Text_" + i + ": [START]" + tR[i][colL1].Replace("\\n", "<LB>") + "[END]\n";
                        textIDsMapping["Text_" + i] = i;
                        nLines += 1;
                    }
                    i += 1;
                }
                currentLine = i;

                Debug.Log(translationStringBatch);

                await Request(translationStringBatch);

                if (stop)
                {
                    break;
                }
            }
        }
        else
        {
            Debug.Log("Can't send message, agent is busy");
        }
    }



    private RunResponse currentRun;

    private TaskCompletionSource<bool> runCompletionSource;

    public async Task Request(string _message)
    {
        currentThread = await api.ThreadsEndpoint.CreateThreadAsync();


        var message = await currentThread.CreateMessageAsync(_message);

        runCompletionSource = new TaskCompletionSource<bool>();

        int maxRetries = 3;
        bool runSuccess = false;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Call the OpenAI endpoint (CreateRunAsync, etc.)
                Debug.Log("new run");
                currentRun = await currentThread.CreateRunAsync(assistant, StreamEventHandler);
                await runCompletionSource.Task;
                runSuccess = true;
                Debug.Log("run done");
                break; // If we get here, it means everything completed
            }
            catch (Exception e)
            {
                // Log the error
                Debug.LogWarning($"OpenAI call attempt #{attempt + 1} failed: {e.Message}");

                // Simple backoff delay
                await Task.Delay(2000);
            }
        }

        if (!runSuccess)
        {
            Debug.LogError("Failed to complete the run after all retries.");
        }
    }

    private IServerSentEvent previousStreamEvent;

    private async Task StreamEventHandler(IServerSentEvent streamEvent)
    {
        //Debug.Log($"Received StreamEvent: {streamEvent.ToJsonString()}");
        switch (streamEvent)
        {
            case ThreadResponse threadResponse:
                Debug.Log("ThreadResponse received.");
                currentThread = threadResponse;
                break;

            case RunResponse runResponse:
                Debug.Log($"RunResponse received. Status: {runResponse.Status}");
                if (runResponse.Status == RunStatus.RequiresAction)
                {
                    Debug.Log("Run requires action:" + runResponse.RequiredAction.SubmitToolOutputs.ToolCalls[0].FunctionCall.Name + " " + runResponse.RequiredAction.SubmitToolOutputs.ToolCalls[0].FunctionCall.Arguments);

                    var toolOutputs = await GetToolOutputsAsync(runResponse);
                    await runResponse.SubmitToolOutputsAsync(toolOutputs, StreamEventHandler);
                }
                if(runResponse.Status == RunStatus.Completed)
                {
                    runCompletionSource?.TrySetResult(true);
                    agentIsBusy = false;
                }
                break;

            case MessageResponse messageResponse:

                break;

            default:
                //Debug.Log($"Unhandled StreamEvent type: {streamEvent.GetType()}");
                break;
        }

        previousStreamEvent = streamEvent;
    }

    private List<string> outputLines = new List<string>();
    [SerializeField] private int maxLines = 30; // Set your desired max lines

    private string Translate(string textID, string translatedText)
    {
        string newLine = textID + " : " + SelectedLanguage() + " " + translatedText;
        outputLines.Add(newLine);

        // Remove excess lines
        while (outputLines.Count > maxLines)
        {
            outputLines.RemoveAt(0);
        }

        // Update the TextMeshProUGUI text
        agentOutput.text = string.Join("\n", outputLines);

        return AddTranslation(SelectedFile(), textID, SelectedLanguage(), translatedText.Replace("<LB>", "\\n"));
    }

    private string AddTranslation(string[][] textArray, string simplifiedTextID, string languageID, string translatedText)
    {
        // 1) Find or create the new column for languageID in the header row.
        int colIndex = FindOrCreateLanguageColumn(textArray, languageID);

        int textRow;

        if (textIDsMapping.ContainsKey(simplifiedTextID))
        {
            textRow = textIDsMapping[simplifiedTextID];
        }
        else
        {
            return "Text " + simplifiedTextID + " was not found in the database, you probably didn't return a correct textID";
        }

        textArray[textRow][colIndex] = translatedText;
        return "Text " + simplifiedTextID + " successfully translated"; // done
    }

    private int FindOrCreateLanguageColumn(string[][] textArray, string languageID)
    {
        // We assume row 0 is the header row.
        // Check if languageID already exists
        for (int i = 0; i < textArray[0].Length; i++)
        {
            if (textArray[0][i] == languageID)
            {
                return i; // Found existing column
            }
        }

        // Not found — create a new column at the end
        int newColumnIndex = textArray[0].Length;

        for (int row = 0; row < textArray.Length; row++)
        {
            // Resize the row array by 1
            Array.Resize(ref textArray[row], textArray[row].Length + 1);

            if (row == 0)
            {
                // Set the new language in the header row
                textArray[row][newColumnIndex] = languageID;
            }
            else
            {
                // Initialize with an empty string, or you can use placeholder text
                textArray[row][newColumnIndex] = string.Empty;
            }
        }

        return newColumnIndex;
    }

    [SerializeField]
    private TMP_InputField outputFilePathField;
    [SerializeField]
    private TMP_InputField tableFileNameField;

    public void SaveTable()
    {
        string[][] array = SelectedFile();


        string fileName = outputFilePathField.text + tableFileNameField.text;

        Debug.Log("Saving data at path:" + fileName);
        TextWriter tw = new StreamWriter(fileName, false);

        int colToSkip = -1;

        for (int i = 0; i < array[0].Length; i++)
        {
            if (array[0][i].Contains("KeepAsLastColumn"))
            {
                colToSkip = i;
            }
        }

        int lineNumber = array.Length;
        for (int i = 0; i < lineNumber; i++)
        {
            int lineLength = array[i].Length;
            string lineToWrite = "";
            for (int j = 0; j < lineLength; j++)
            {
                if (j == colToSkip)
                {
                    //lineToWrite += "skip" + "\t";
                }
                else
                {
                    lineToWrite += array[i][j] + "\t";
                }
            }
            tw.WriteLine(lineToWrite);
        }
        tw.Close();
    }


    public void LoadOutputPath()
    {
        if (PlayerPrefs.GetString("translationOutputFilePath") != null)
        {
            outputFilePathField.text = PlayerPrefs.GetString("translationOutputFilePath");
        }
    }

    public void LoadTableFileName()
    {
        if (PlayerPrefs.GetString("translationTableFileName") != null)
        {
            tableFileNameField.text = PlayerPrefs.GetString("translationTableFileName");
        }
    }

    public void SaveOutputPath()
    {
        PlayerPrefs.SetString("translationOutputFilePath", outputFilePathField.text);
        PlayerPrefs.Save();
    }

    public void SaveTableFileName()
    {
        PlayerPrefs.SetString("translationTableFileName", tableFileNameField.text);
        PlayerPrefs.Save();
    }


    public List<Tool> Tools()
    {

        Tool translateText =
            new Function(
            nameof(Translate),
            "A tool to check how you feel, to see what you might need to do to feel better and survive longer", new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["textID"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "the ID of text you are translating (usually of the form TID_XXXX or DID_XXXX or AID_XXXX)"
                    },
                    ["translatedText"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "the translated text"
                    },
                },
                ["required"] = new JArray { "textID","translatedText" }
            });

        List<Tool> tools = new List<Tool>
        {
                    translateText
        };

        return tools;
    }

    public async Task<SubmitToolOutputsRequest> GetToolOutputsAsync(RunResponse response)
    {
        List<ToolOutput> outputs = new();
        foreach (OpenAI.Threads.ToolCall call in response.RequiredAction.SubmitToolOutputs.ToolCalls)
        {
            string toolName = call.FunctionCall.Name;
            string arguments = call.FunctionCall.Arguments;
            string toolCallId = call.Id;

            if (stop)
            {
                outputs.Add(new ToolOutput(toolCallId, "STOP what you are doing"));
            }
            else
            {
                var output = await CallTool(toolName, arguments);
                Debug.Log("tool output:" + output);
                outputs.Add(new ToolOutput(toolCallId, output));
            }
        }

        return new SubmitToolOutputsRequest(outputs);
    }
    public async Task<string> CallTool(string toolName, string arguments)
    {
        // Parse JSON arguments using Newtonsoft.Json
        Dictionary<string, string> argsDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);

        // Find method by name (private methods included)
        MethodInfo method = GetType().GetMethod(toolName, BindingFlags.NonPublic | BindingFlags.Instance);

        if (method == null)
        {
            Debug.LogWarning($"Tool '{toolName}' not found.");

            return $"Tool '{toolName}' does not exist.";
        }

        // Get parameters and map arguments
        ParameterInfo[] parameters = method.GetParameters();
        object[] paramValues = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            string paramName = parameters[i].Name;
            if (!argsDict.TryGetValue(paramName, out string value))
            {
                throw new Exception($"Missing argument: {paramName}");
            }
            paramValues[i] = value;
        }

        // Check if method is asynchronous
        if (typeof(Task<string>).IsAssignableFrom(method.ReturnType))
        {
            return await (Task<string>)method.Invoke(this, paramValues);
        }
        else
        {
            return (string)method.Invoke(this, paramValues);
        }
    }
}

public static class JSONParser
{
    public static string Value(string json)
    {
        // Deserialize JSON using Newtonsoft.Json
        Root root = JsonConvert.DeserializeObject<Root>(json);
        if (root.delta == null)
        {
            return null;
        }

        if (root.delta.content == null)
        {
            return null;
        }

        if (root.delta.content[0].text == null)
        {
            return null;
        }
        // Access the `value` field
        string value = JsonConvert.DeserializeObject<Root>(json).delta.content[0].text.value;

        // Set TMP text
        return value;
    }

    [System.Serializable]
    public class Root
    {
        public string id;
        public string @object;
        public Delta delta;
    }

    [System.Serializable]
    public class Delta
    {
        public string role;
        public Content[] content;
    }

    [System.Serializable]
    public class TextContent
    {
        public string value;
    }

    [System.Serializable]
    public class Content
    {
        public int index;
        public string type;
        public TextContent text;
    }
}
