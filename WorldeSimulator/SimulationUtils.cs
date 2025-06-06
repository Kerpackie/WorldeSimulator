using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace WordleSimulator;

/// <summary>
/// Represents a letter and its position in a word.
/// </summary>
public struct LetterPosition
{
    public char Letter; // The letter.
    public int Index;   // The position of the letter in the word.

    /// <summary>
    /// Initializes a new instance of the LetterPosition struct.
    /// </summary>
    /// <param name="letter">The letter.</param>
    /// <param name="index">The position of the letter in the word.</param>
    public LetterPosition(char letter, int index)
    {
        Letter = letter;
        Index = index;
    }
}

/// <summary>
/// Represents feedback for a Wordle guess.
/// </summary>
public class Feedback
{
    public Dictionary<int, char> Green; // Correct letters in the correct positions.
    public List<LetterPosition> Yellow; // Correct letters in incorrect positions.
    public HashSet<char> Gray;          // Incorrect letters.

    /// <summary>
    /// Initializes a new instance of the Feedback class.
    /// </summary>
    public Feedback()
    {
        Green = new Dictionary<int, char>();
        Yellow = new List<LetterPosition>();
        Gray = new HashSet<char>();
    }
}

/// <summary>
/// Represents a log entry for a Wordle guess.
/// </summary>
public struct GuessLogEntry
{
    public string Guess;   // The guessed word.
    public int Remaining;  // The number of remaining possible words.

    /// <summary>
    /// Initializes a new instance of the GuessLogEntry struct.
    /// </summary>
    /// <param name="guess">The guessed word.</param>
    /// <param name="remaining">The number of remaining possible words.</param>
    public GuessLogEntry(string guess, int remaining)
    {
        Guess = guess;
        Remaining = remaining;
    }
}

/// <summary>
/// Represents the result of a Wordle simulation.
/// </summary>
public class SimulationResult
{
    public int? Attempts;              // The number of attempts taken to guess the word, or null if unsuccessful.
    public List<GuessLogEntry> Log;    // The log of guesses made during the simulation.

    /// <summary>
    /// Initializes a new instance of the SimulationResult class.
    /// </summary>
    /// <param name="attempts">The number of attempts taken to guess the word.</param>
    /// <param name="log">The log of guesses made during the simulation.</param>
    public SimulationResult(int? attempts, List<GuessLogEntry> log)
    {
        Attempts = attempts;
        Log = log;
    }
}

/// <summary>
/// Represents weights for scoring words in the simulation.
/// </summary>
public struct Weights
{
    public double Letter;         // Weight for letter probabilities.
    public double Bigram;         // Weight for bigram probabilities.
    public double Trigram;        // Weight for trigram probabilities.
    public double Position;       // Weight for positional probabilities.
    public double RepeatPenalty;  // Penalty for repeated letters.
}

/// <summary>
/// Utility methods for Wordle simulation.
/// </summary>
public static class SimulationUtils
{
    /// <summary>
    /// Loads the list of Wordle words from a remote source.
    /// </summary>
    /// <returns>A list of valid Wordle words.</returns>
    public static async Task<List<string>> LoadWordleWords()
    {
        var client = new HttpClient();
        var list = new List<string>();
        string url = "https://raw.githubusercontent.com/tabatkins/wordle-list/main/words";
        string content = await client.GetStringAsync(url);
        string[] lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string word = lines[i].Trim();
            if (word.Length == 5)
            {
                list.Add(word);
            }
        }

        return list;
    }

    /// <summary>
    /// Calculates the probabilities of each letter appearing in the given list of words.
    /// </summary>
    /// <param name="words">The list of words.</param>
    /// <returns>A dictionary mapping letters to their probabilities.</returns>
    public static Dictionary<char, double> GetLetterProbs(List<string> words)
    {
        var counts = new Dictionary<char, int>();
        int total = 0;

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            for (int j = 0; j < word.Length; j++)
            {
                char c = word[j];
                if (!counts.ContainsKey(c)) counts[c] = 0;
                counts[c]++;
                total++;
            }
        }

        var result = new Dictionary<char, double>();
        foreach (var pair in counts)
        {
            result[pair.Key] = (double)pair.Value / total;
        }

        return result;
    }

    /// <summary>
    /// Calculates the probabilities of n-grams appearing in the given list of words.
    /// </summary>
    /// <param name="words">The list of words.</param>
    /// <param name="n">The size of the n-gram.</param>
    /// <returns>A dictionary mapping n-grams to their probabilities.</returns>
    public static Dictionary<string, double> GetNgramProbs(List<string> words, int n)
    {
        var counts = new Dictionary<string, int>();
        int total = 0;

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            for (int j = 0; j <= word.Length - n; j++)
            {
                string ngram = word.Substring(j, n);
                if (!counts.ContainsKey(ngram)) counts[ngram] = 0;
                counts[ngram]++;
                total++;
            }
        }

        var result = new Dictionary<string, double>();
        foreach (var pair in counts)
        {
            result[pair.Key] = (double)pair.Value / total;
        }

        return result;
    }

    /// <summary>
    /// Calculates the probabilities of letters appearing in specific positions across the given list of words.
    /// </summary>
    /// <param name="words">The list of words.</param>
    /// <returns>A list of dictionaries mapping letters to their probabilities for each position.</returns>
    public static List<Dictionary<char, double>> GetPositionalProbs(List<string> words)
    {
        var posCounts = new List<Dictionary<char, int>>();
        for (int i = 0; i < 5; i++) posCounts.Add(new Dictionary<char, int>());

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            for (int j = 0; j < 5; j++)
            {
                char ch = word[j];
                if (!posCounts[j].ContainsKey(ch)) posCounts[j][ch] = 0;
                posCounts[j][ch]++;
            }
        }

        var posProbs = new List<Dictionary<char, double>>();
        for (int i = 0; i < 5; i++)
        {
            var posProb = new Dictionary<char, double>();
            int total = 0;
            foreach (var val in posCounts[i].Values) total += val;
            foreach (var pair in posCounts[i])
            {
                posProb[pair.Key] = (double)pair.Value / total;
            }
            posProbs.Add(posProb);
        }

        return posProbs;
    }

    /// <summary>
    /// Generates feedback for a Wordle guess compared to the actual target word.
    /// </summary>
    /// <param name="guess">The guessed word.</param>
    /// <param name="actual">The actual target word.</param>
    /// <returns>A Feedback object containing the results of the comparison.</returns>
    public static Feedback GetFeedback(string guess, string actual)
    {
        var feedback = new Feedback();

        for (int i = 0; i < guess.Length; i++)
        {
            char g = guess[i];
            char a = actual[i];

            if (g == a)
            {
                feedback.Green[i] = g;
            }
            else if (actual.Contains(g))
            {
                feedback.Yellow.Add(new LetterPosition(g, i));
            }
            else
            {
                feedback.Gray.Add(g);
            }
        }

        return feedback;
    }

    /// <summary>
    /// Filters the list of words based on feedback from previous guesses.
    /// </summary>
    /// <param name="words">The list of words to filter.</param>
    /// <param name="green">Correct letters in correct positions.</param>
    /// <param name="yellow">Correct letters in incorrect positions.</param>
    /// <param name="gray">Incorrect letters.</param>
    /// <returns>A filtered list of words.</returns>
    public static List<string> FilterWords(
        List<string> words,
        Dictionary<int, char> green,
        List<LetterPosition> yellow,
        HashSet<char> gray)
    {
        var filtered = new List<string>();

        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            bool isValid = true;

            foreach (var pair in green)
            {
                if (word[pair.Key] != pair.Value)
                {
                    isValid = false;
                    break;
                }
            }

            if (!isValid) continue;

            for (int j = 0; j < yellow.Count; j++)
            {
                var yp = yellow[j];
                if (!word.Contains(yp.Letter) || word[yp.Index] == yp.Letter)
                {
                    isValid = false;
                    break;
                }
            }

            if (!isValid) continue;

            for (int j = 0; j < word.Length; j++)
            {
                char ch = word[j];
                if (gray.Contains(ch))
                {
                    bool inGreen = false;
                    foreach (var val in green.Values)
                    {
                        if (val == ch) { inGreen = true; break; }
                    }

                    bool inYellow = false;
                    for (int k = 0; k < yellow.Count; k++)
                    {
                        if (yellow[k].Letter == ch) { inYellow = true; break; }
                    }

                    if (!inGreen && !inYellow)
                    {
                        isValid = false;
                        break;
                    }
                }
            }

            if (isValid) filtered.Add(word);
        }

        return filtered;
    }

    /// <summary>
    /// Scores a word based on various probabilities and weights.
    /// </summary>
    /// <param name="word">The word to score.</param>
    /// <param name="letterProbs">Letter probabilities.</param>
    /// <param name="bigramProbs">Bigram probabilities.</param>
    /// <param name="trigramProbs">Trigram probabilities.</param>
    /// <param name="posProbs">Positional probabilities.</param>
    /// <param name="weights">Weights for scoring components.</param>
    /// <returns>The score of the word.</returns>
    public static double ScoreWord(
        string word,
        Dictionary<char, double> letterProbs,
        Dictionary<string, double> bigramProbs,
        Dictionary<string, double> trigramProbs,
        List<Dictionary<char, double>> posProbs,
        Weights weights)
    {
        var seen = new HashSet<char>();
        for (int i = 0; i < word.Length; i++) seen.Add(word[i]);

        double repeatPenalty = seen.Count < 5 ? weights.RepeatPenalty : 1.0;

        double letterScore = 0;
        foreach (char c in seen)
        {
            if (letterProbs.ContainsKey(c))
                letterScore += letterProbs[c];
        }

        double bigramScore = 0;
        for (int i = 0; i < 4; i++)
        {
            string bg = word.Substring(i, 2);
            if (bigramProbs.ContainsKey(bg))
                bigramScore += bigramProbs[bg];
        }

        double trigramScore = 0;
        for (int i = 0; i < 3; i++)
        {
            string tg = word.Substring(i, 3);
            if (trigramProbs.ContainsKey(tg))
                trigramScore += trigramProbs[tg];
        }

        double positionScore = 0;
        for (int i = 0; i < 5; i++)
        {
            char ch = word[i];
            if (posProbs[i].ContainsKey(ch))
                positionScore += posProbs[i][ch];
        }

        return repeatPenalty * (
            weights.Letter * letterScore +
            weights.Bigram * bigramScore +
            weights.Trigram * trigramScore +
            weights.Position * positionScore
        );
    }

    /// <summary>
    /// Simulates a Wordle game to guess the target word.
    /// </summary>
    /// <param name="target">The target word to guess.</param>
    /// <param name="words">The list of possible words.</param>
    /// <param name="weights">Weights for scoring words.</param>
    /// <param name="letterProbs">Letter probabilities.</param>
    /// <param name="bigramProbs">Bigram probabilities.</param>
    /// <param name="trigramProbs">Trigram probabilities.</param>
    /// <param name="posProbs">Positional probabilities.</param>
    /// <returns>A SimulationResult object containing the results of the simulation.</returns>
    public static SimulationResult SimulateGame(
        string target,
        List<string> words,
        Weights weights,
        Dictionary<char, double> letterProbs,
        Dictionary<string, double> bigramProbs,
        Dictionary<string, double> trigramProbs,
        List<Dictionary<char, double>> posProbs)
    {
        var green = new Dictionary<int, char>();
        var yellow = new List<LetterPosition>();
        var gray = new HashSet<char>();
        var used = new HashSet<string>();
        var possible = new List<string>(words);
        var log = new List<GuessLogEntry>();
        int attempts = 0;

        while (true)
        {
            if (attempts >= 12 || possible.Count == 0)
                return new SimulationResult(null, log);

            string bestGuess = null;
            double bestScore = -1;

            for (int i = 0; i < possible.Count; i++)
            {
                string word = possible[i];
                if (used.Contains(word)) continue;

                double score = ScoreWord(word, letterProbs, bigramProbs, trigramProbs, posProbs, weights);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestGuess = word;
                }
            }

            if (bestGuess == null)
                return new SimulationResult(null, log);

            used.Add(bestGuess);
            log.Add(new GuessLogEntry(bestGuess, possible.Count));
            attempts++;

            if (bestGuess == target)
                return new SimulationResult(attempts, log);

            Feedback feedback = GetFeedback(bestGuess, target);
            foreach (var kv in feedback.Green)
                green[kv.Key] = kv.Value;
            for (int i = 0; i < feedback.Yellow.Count; i++)
                yellow.Add(feedback.Yellow[i]);
            foreach (var ch in feedback.Gray)
                gray.Add(ch);

            possible = FilterWords(possible, green, yellow, gray);
        }
    }
}