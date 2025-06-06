using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WordleSimulator;

class SummaryEntry
{
    public string Word;
    public int Attempts;
    public bool Solved;

    public SummaryEntry(string word, int attempts, bool solved)
    {
        Word = word;
        Attempts = attempts;
        Solved = solved;
    }
}

class DetailedLog
{
    public string Word;
    public bool Solved;
    public int? Attempts;
    public List<GuessLogEntry> GuessLog;

    public DetailedLog(string word, bool solved, int? attempts, List<GuessLogEntry> guessLog)
    {
        Word = word;
        Solved = solved;
        Attempts = attempts;
        GuessLog = guessLog;
    }
}

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Loading words...");
        List<string> wordList = await SimulationUtils.LoadWordleWords();
        Dictionary<char, double> letterProbs = SimulationUtils.GetLetterProbs(wordList);
        Dictionary<string, double> bigramProbs = SimulationUtils.GetNgramProbs(wordList, 2);
        Dictionary<string, double> trigramProbs = SimulationUtils.GetNgramProbs(wordList, 3);
        List<Dictionary<char, double>> posProbs = SimulationUtils.GetPositionalProbs(wordList);

        Weights weights = new Weights
        {
            Letter = 1.0,
            Bigram = 0.5,
            Trigram = 0.25,
            Position = 0.75,
            RepeatPenalty = 0.9
        };

        List<SummaryEntry> summary = new List<SummaryEntry>();
        List<DetailedLog> detailedLogs = new List<DetailedLog>();
        List<int> results = new List<int>();
        List<string> failed = new List<string>();

        int testLimit = 100;
        for (int i = 0; i < testLimit && i < wordList.Count; i++)
        {
            string word = wordList[i];
            Console.WriteLine("Testing " + word + "...");

            SimulationResult result = SimulationUtils.SimulateGame(
                word, wordList, weights, letterProbs, bigramProbs, trigramProbs, posProbs
            );

            int attemptCount = result.Attempts.HasValue ? result.Attempts.Value : result.Log.Count;
            bool solved = result.Attempts.HasValue;

            summary.Add(new SummaryEntry(word, attemptCount, solved));
            detailedLogs.Add(new DetailedLog(word, solved, result.Attempts, result.Log));

            if (solved)
            {
                results.Add(attemptCount);
            }
            else
            {
                failed.Add(word);
            }
        }

        // Write CSV
        using (var writer = new StreamWriter("wordle_summary.csv"))
        {
            writer.WriteLine("word,attempts,solved");
            for (int i = 0; i < summary.Count; i++)
            {
                var entry = summary[i];
                writer.WriteLine(entry.Word + "," + entry.Attempts + "," + entry.Solved);
            }
        }

        // Write JSON
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(detailedLogs, jsonOptions);
        File.WriteAllText("wordle_detailed_logs.json", json);

        Console.WriteLine("Done! Results written to wordle_summary.csv and wordle_detailed_logs.json");
    }
}
