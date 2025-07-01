using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class ResourcesManager : MonoBehaviour
{
    public static ResourcesManager instance;

    public string[][] inputFile;

    //culture used in the back end (whenever reading or saving dates for example)
    public CultureInfo cultureData = CultureInfo.CreateSpecificCulture("en-US");

    //cultures used for text shown to users:
    public CultureInfo cultureUS = CultureInfo.CreateSpecificCulture("en-US");
    public CultureInfo cultureFR = CultureInfo.CreateSpecificCulture("fr-FR");

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != null)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this.gameObject);
        }

    }

    public void LoadResources(string filePath)
    {
        inputFile = create2DStrArrayFromFile(filePath);
    }

    string[][] create2DStrArrayFromFile(string fileName)
    {
        var data = Resources.Load<TextAsset>(fileName);

        if (data == null)
        {
            Debug.Log("could not find the file: " + fileName);
        }

        return create2DStrArray(data);
    }

    string[][] create2DStrArray(TextAsset data)
    {
        string[] lines = data.text.Split(new char[] { '\n' });
        string[][] table = new string[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            string[] strArray = lines[i].Split('\t');
            table[i] = strArray;
        }
        return table;
    }


    //Create a list from column i of input table
    public List<string> ColumnExtractFromStrArray(string[][] table, int column, bool headerfilter)
    {
        List<string> list = new List<string>();

        int length = table.Length; //number of lines

        int j0 = 0;
        if (headerfilter == true) { j0 = 1; } else { j0 = 0; }

        for (int j = j0; j < length; j++)
        {
            list.Add(table[j][column]);
        }
        // when headerfilter is true j=0 is not added since its the title of the column 

        return list;
    }

    //Create a table of 2 columns from colum i and j from input table
    public string[][] TwoColumnExtractFromStrArray(string[][] table, int column1, int column2)
    {
        int length = table.Length;
        string[][] tableExtract = new string[length - 1][];

        for (int k = 1; k < length; k++)
        {
            string[] line = { table[k][column1], table[k][column2] };
            tableExtract[k - 1] = line;
        }
        // k=0 is not added since its the title of the column 

        return tableExtract;
    }

    public string[][] ThreeColumnExtractFromStrArray(string[][] table, int column1, int column2, int column3)
    {
        int length = table.Length;
        string[][] tableExtract = new string[length - 1][];
        for (int l = 1; l < length; l++)
        {
            string[] line = { table[l][column1], table[l][column2], table[l][column3] };
            tableExtract[l - 1] = line;
        }
        // k=0 is not added since its the title of the column 

        return tableExtract;
    }

    //Filter a table, selecting only lines for which value at given column is equal to filter or to defaultvalue (0) if includeDefaultValue is true
    public string[][] FilterTable(string[][] table, int column, string filter, bool includeDefaultValue)
    {
        int length = table.Length;
        bool test;
        List<int> lines = new List<int>();


        for (int i = 0; i < length; i++)
        {

            if (includeDefaultValue == true)
            {
                test = table[i][column] == filter || table[i][column] == "0";
            }
            else
            {
                test = table[i][column] == filter;
            }

            if (test == true)
            {
                lines.Add(i);
            }
        }

        string[][] filteredTable = new string[lines.Count + 1][];
        filteredTable[0] = table[0]; //to keep headers !

        for (int i = 0; i < lines.Count; i++)
        {
            filteredTable[i + 1] = table[lines[i]];
            //Debug.Log(string.Join(",",filteredTable[i]));
        }

        return filteredTable;

    }

    public int ColumnFinder(string[][] table, string columnName)
    {
        int c = System.Array.IndexOf(table[0], columnName);
        if (c < 0)
        {
            Debug.Log("Column finder error : could not find column :" + columnName);
        }
        return c;
    }

    public string LanguageRID(int languageID)
    {
        string str = "L" + languageID.ToString();

        return str;
    }

    public string ValueFinder(string[][] resources, string IDColName, string ValueColName, string id)
    {
        int column = ColumnFinder(resources, IDColName);
        string[][] table = FilterTable(resources, column, id.ToString(), false);
        int column2 = ColumnFinder(resources, ValueColName);
        return table[1][column2];
    }

    //Extract ID column and text in given language from resource
    public string[][] Create2ColExtract(string[][] resources, string lang, string IDtype)
    {
        int col = ColumnFinder(resources, lang);
        int col2 = ColumnFinder(resources, IDtype);
        return TwoColumnExtractFromStrArray(resources, col2, col);
    }

    //Extract ID column and secondary ID column and text in given language from resource
    public string[][] Create3ColExtract(string[][] resources, string lang, string IDtype, string IDtype2)
    {
        int col = ColumnFinder(resources, lang);
        int col2 = ColumnFinder(resources, IDtype);
        int col3 = ColumnFinder(resources, IDtype2);
        return ThreeColumnExtractFromStrArray(resources, col2, col3, col);
    }
}
