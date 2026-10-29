using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;


public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [SerializeField] private Dictionary<Recompenses, float> recompenseScore;

    public Dictionary<Recompenses, float> RecompenseScore => recompenseScore;
    public Action<Recompenses, int> OnRecompenses;

    List<AgentRewards> scoreByAgent = new List<AgentRewards>();
    private List<string> csvParams = new List<string>();

    private AgentRewards GlobalRewards = new AgentRewards();
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

        filePath = Application.persistentDataPath + "/ResultTrainning_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") +
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
        float scoreToAdd = recompenseScore.GetValueOrDefault(recompenseType);
        GlobalRewards.cumulativeReward += scoreToAdd;
        Debug.Log($"[GLOBAL SCORE] --> {GlobalRewards.cumulativeReward}");

        GlobalRewards.rewardsByType[(int)recompenseType] += scoreToAdd;
        scoreByAgent[agentID].rewardsByType[(int)recompenseType] += scoreToAdd;
        scoreByAgent[agentID].cumulativeReward += scoreToAdd;
    }

    private void ExportGlobalScoreToCSV()
    {
        StreamWriter writer = new StreamWriter(filePath);

        string rewardNames = string.Join(";", Enum.GetNames(typeof(Recompenses)));
        writer.WriteLine($"Agent, Score value, {rewardNames}");

        writer.WriteLine("GLOBAL SCORE" + ";" + GlobalRewards);

        for (int j = 1; j < scoreByAgent.Count; ++j)
        {
            writer.Write($"Agent {j}" + ";" + scoreByAgent[j].cumulativeReward + ";");
            writer.WriteLine(string.Join(';', scoreByAgent[j].rewardsByType.ToArray()));
        }


        writer.Flush();
        writer.Close();

        Debug.LogError($"DATA SAVED AT : {filePath}");
    }
}


public enum Recompenses
{
    TouchDrain = 0,
    TriggerResetZone = 1,
    NoHit = 2,
    CloseToTarget = 3,
    HitTarget = 4
}

public class AgentRewards
{
    public float[] rewardsByType = new float[Enum.GetValues(typeof(Recompenses)).Length];
    public float cumulativeReward;
}