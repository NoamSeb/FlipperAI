using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [SerializeField] private Dictionary<Recompenses, float> recompenseScore;

    public Dictionary<Recompenses, float> RecompenseScore => recompenseScore;
    public Action<Recompenses, int> OnRecompenses;

    List<AgentRewards> scoreByAgent = new List<AgentRewards>();
    private List<string> csvParams = new List<string>();

    private float GlobalScore = 0f;
    private string filePath;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void Start()
    {
        OnRecompenses += UpdateGlobalScore;
        for (int i = 0; i < FlipperAgent.NextAgentId; i++)
        {
            scoreByAgent.Add(new AgentRewards());
        }

        filePath = Application.persistentDataPath + "/ResultTrainning_" + DateTime.Now.ToString("yyyyMMddHHmmss") +
                   ".csv";
        csvParams.Add("");
        csvParams.Add("");
        csvParams[0] = "Agent";
        csvParams[1] = "Score Value";

        for (int i = 0; i < Enum.GetValues(typeof(Recompenses)).Length; i++)
        {
            csvParams.Add(Enum.GetValues(typeof(Recompenses)).GetValue(i).ToString());
        }
    }

    private void OnDestroy()
    {
        OnRecompenses -= UpdateGlobalScore;
        ExportGlobalScoreToCSV();
    }

    private void UpdateGlobalScore(Recompenses recompenseType, int agentID)
    {
        GlobalScore += recompenseScore.GetValueOrDefault(recompenseType);
        Debug.Log($"[GLOBAL SCORE] --> {GlobalScore}");

        scoreByAgent[agentID].cumulativeReward = GlobalScore;
    }

    private void ExportGlobalScoreToCSV()
    {
        StreamWriter writer = new StreamWriter(filePath);

        writer.WriteLine("Agent, Score value");

        writer.WriteLine("GLOBAL SCORE" + ";" + GlobalScore);

        for (int j = 1; j < scoreByAgent.Count; ++j)
        {
            writer.Write($"Agent {j}" + ";" + scoreByAgent[j].cumulativeReward+";");
            writer.WriteLine(string.Join(';', scoreByAgent[j].rewardsByType.ToArray()));
            // for (int i = 0; i < Enum.GetValues(typeof(Recompenses)).Length; i++)
            // {
            // }
            //
            // for (int i = 0; i < csvParams.Count; i++)
            // {
            //     writer.WriteLine($"Agent {j}" + ";" + scoreByAgent[j]);
            // }
        }


        writer.Flush();
        writer.Close();

        Debug.LogError($"DATA SAVED AT : {filePath}");
    }
}


public enum Recompenses
{
    UltraNegative = 0,
    Negative = 1,
    Moyenne = 2,
    Forte = 3
}

public class AgentRewards
{
    public float[] rewardsByType = new float[Enum.GetValues(typeof(Recompenses)).Length];
    public float cumulativeReward;
}