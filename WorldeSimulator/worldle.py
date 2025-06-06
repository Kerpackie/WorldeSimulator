import urllib.request
from collections import Counter
import matplotlib.pyplot as plt
import numpy as np
import csv
import json

# --- Load Official Wordle Answer List ---
def load_wordle_words():
    url = "https://raw.githubusercontent.com/tabatkins/wordle-list/main/words"
    response = urllib.request.urlopen(url)
    return [line.decode("utf-8").strip() for line in response if len(line.decode("utf-8").strip()) == 5]

# --- Frequency Analysis ---
def get_letter_probs(words):
    all_letters = ''.join(words)
    counts = Counter(all_letters)
    total = sum(counts.values())
    return {ch: count / total for ch, count in counts.items()}

def get_ngram_probs(words, n):
    ngrams = Counter()
    for word in words:
        for i in range(len(word) - n + 1):
            ngrams[word[i:i+n]] += 1
    total = sum(ngrams.values())
    return {ng: count / total for ng, count in ngrams.items()}

def get_positional_probs(words):
    pos_counts = [Counter() for _ in range(5)]
    for word in words:
        for i, ch in enumerate(word):
            pos_counts[i][ch] += 1
    probs = []
    for counter in pos_counts:
        total = sum(counter.values())
        probs.append({ch: count / total for ch, count in counter.items()})
    return probs

# --- Feedback Simulation ---
def get_feedback(guess, actual):
    green = {}
    yellow = []
    gray = set()
    for i, ch in enumerate(guess):
        if ch == actual[i]:
            green[i] = ch
        elif ch in actual:
            if guess.count(ch) <= actual.count(ch):
                yellow.append((ch, i))
            else:
                gray.add(ch)
        else:
            gray.add(ch)
    return green, yellow, gray

# --- Word Filtering ---
def filter_words(words, green=None, yellow=None, gray=None):
    green = green or {}
    yellow = yellow or []
    gray = gray or set()
    filtered = []

    for word in words:
        valid = True

        for pos, char in green.items():
            if word[pos] != char:
                valid = False
                break
        if not valid:
            continue

        for char, pos in yellow:
            if char not in word or word[pos] == char:
                valid = False
                break
        if not valid:
            continue

        for char in gray:
            if char in word and char not in green.values() and all(char != y[0] for y in yellow):
                valid = False
                break
        if not valid:
            continue

        filtered.append(word)

    return filtered

# --- Word Scoring ---
def score_word(word, letter_probs, bigram_probs, trigram_probs, pos_probs, weights):
    unique_letters = set(word)
    repeat_penalty = weights['repeat_penalty'] if len(unique_letters) < 5 else 1.0
    letter_score = sum(letter_probs.get(c, 0) for c in unique_letters)
    bigram_score = sum(bigram_probs.get(word[i:i+2], 0) for i in range(4))
    trigram_score = sum(trigram_probs.get(word[i:i+3], 0) for i in range(3))
    position_score = sum(pos_probs[i].get(word[i], 0) for i in range(5))
    return repeat_penalty * (
        weights['letter'] * letter_score +
        weights['bigram'] * bigram_score +
        weights['trigram'] * trigram_score +
        weights['position'] * position_score
    )

# --- Game Simulation ---
def simulate_game_stats(target_word, word_list, weights, letter_probs, bigram_probs, trigram_probs, pos_probs):
    if target_word not in word_list:
        print(f"⚠️ Target word '{target_word}' is not in the list. Skipping.")
        return None, []

    green, yellow, gray = {}, [], set()
    attempts = 0
    max_attempts = 12
    possible_words = word_list.copy()
    used_guesses = set()
    guess_log = []

    while True:
        if not possible_words:
            print(f"\n❌ '{target_word}': no possible words left after filtering.")
            return None, guess_log

        scored = [
            w for w in sorted(possible_words, key=lambda w: score_word(w, letter_probs, bigram_probs, trigram_probs, pos_probs, weights), reverse=True)
            if w not in used_guesses
        ]

        if not scored:
            print(f"\n❌ '{target_word}': No new candidates after filtering.")
            return None, guess_log

        guess = scored[0]
        used_guesses.add(guess)
        attempts += 1
        guess_log.append((guess, len(possible_words)))

        if guess == target_word:
            return attempts, guess_log

        if attempts >= max_attempts:
            print(f"\n❌ Gave up on '{target_word}' after {max_attempts} attempts.")
            return None, guess_log

        g, y, r = get_feedback(guess, target_word)
        green.update(g)
        yellow += y
        gray.update(r)
        possible_words = filter_words(possible_words, green, yellow, gray)

# --- Main Execution ---
if __name__ == "__main__":
    print("🔁 Loading official Wordle answer list...")
    word_list = load_wordle_words()

    letter_probs = get_letter_probs(word_list)
    bigram_probs = get_ngram_probs(word_list, 2)
    trigram_probs = get_ngram_probs(word_list, 3)
    pos_probs = get_positional_probs(word_list)

    weights = {
        'letter': 1.0,
        'bigram': 0.5,
        'trigram': 0.25,
        'position': 0.75,
        'repeat_penalty': 0.9
    }

    print("⚙️ Running simulations...")
    sample_words = word_list[:100]  # full set or use [:100] for testing
    results = []
    failed_words = []
    summary_data = []
    detailed_logs = []

    for idx, word in enumerate(sample_words, 1):
        print(f"\n▶️ {idx}/{len(sample_words)}: Testing '{word}'...")

        attempts, guess_log = simulate_game_stats(
            word, word_list, weights,
            letter_probs, bigram_probs, trigram_probs, pos_probs
        )

        if attempts is not None:
            print(f"✅ Solved in {attempts} attempts.")
            results.append((word, attempts))
            summary_data.append({'word': word, 'attempts': attempts, 'solved': True})
        else:
            print("❌ Failed.")
            failed_words.append(word)
            summary_data.append({'word': word, 'attempts': len(guess_log), 'solved': False})

        detailed_logs.append({
            'word': word,
            'solved': attempts is not None,
            'attempts': attempts if attempts is not None else None,
            'guess_log': guess_log
        })

    # Stats
    attempt_counts = [a for _, a in results]
    avg_attempts = np.mean(attempt_counts)
    max_attempts = np.max(attempt_counts)
    min_attempts = np.min(attempt_counts)
    distribution = Counter(attempt_counts)

    print("\n📊 --- Simulation Summary ---")
    print(f"Words tested: {len(results)}")
    print(f"Average attempts: {avg_attempts:.2f}")
    print(f"Min attempts: {min_attempts}")
    print(f"Max attempts: {max_attempts}")
    print("Attempt distribution:", dict(distribution))

    if failed_words:
        print(f"\n❌ {len(failed_words)} words failed:")
        for word in failed_words:
            print(f"  - {word}")

    # Save results
    with open("wordle_summary.csv", "w", newline='') as f:
        writer = csv.DictWriter(f, fieldnames=["word", "attempts", "solved"])
        writer.writeheader()
        writer.writerows(summary_data)

    with open("wordle_detailed_logs.json", "w") as f:
        json.dump(detailed_logs, f, indent=2)

    print("\n📁 Results saved to 'wordle_summary.csv' and 'wordle_detailed_logs.json'.")

    # Plot results
    if results:
        words, attempts = zip(*results)
        plt.figure(figsize=(12, 6))
        plt.bar(words[:100], attempts[:100], color='lightblue')
        plt.xticks(rotation=90)
        plt.title("Attempts to Solve Each Word (first 100)")
        plt.ylabel("Attempts")
        plt.tight_layout()
        plt.show()

        plt.figure(figsize=(6, 4))
        plt.hist(attempt_counts, bins=range(1, max_attempts + 2), align='left', rwidth=0.8)
        plt.title("Distribution of Solve Attempts")
        plt.xlabel("Attempts")
        plt.ylabel("Number of Words")
        plt.xticks(range(1, max_attempts + 1))
        plt.tight_layout()
        plt.show()
