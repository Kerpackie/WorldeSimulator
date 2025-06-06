import os
import json
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

# ---------- PLOT FUNCTIONS ----------

def plot_attempt_distribution(df, output_path):
    plt.figure(figsize=(6, 4))
    df[df["solved"] == True]["attempts"].hist(
        bins=range(1, df["attempts"].max() + 2), align='left', rwidth=0.8
    )
    plt.title("Distribution of Solve Attempts")
    plt.xlabel("Attempts")
    plt.ylabel("Number of Words")
    plt.xticks(range(1, df["attempts"].max() + 1))
    plt.tight_layout()
    plt.savefig(output_path)
    print(f"✅ Saved: {os.path.basename(output_path)}")


def plot_top_hardest_words(df, output_path, top_n=20):
    top = df.sort_values("attempts", ascending=False).head(top_n)
    plt.figure(figsize=(10, 5))
    plt.bar(top["word"], top["attempts"], color='orange')
    plt.title(f"Top {top_n} Hardest Words")
    plt.ylabel("Attempts")
    plt.xticks(rotation=45)
    plt.tight_layout()
    plt.savefig(output_path)
    print(f"✅ Saved: {os.path.basename(output_path)}")


def plot_guess_length_histogram(detailed, output_path):
    guess_lengths = [len(entry.get("guessLog", [])) for entry in detailed]
    plt.figure(figsize=(6, 4))
    plt.hist(guess_lengths, bins=range(1, max(guess_lengths) + 2), align='left', rwidth=0.8)
    plt.title("Total Guesses per Word (Solved + Failed)")
    plt.xlabel("Number of Guesses")
    plt.ylabel("Word Count")
    plt.tight_layout()
    plt.savefig(output_path)
    print(f"✅ Saved: {os.path.basename(output_path)}")


def plot_avg_candidates_per_guess(detailed, output_path):
    valid_steps = []
    for entry in detailed:
        for i, step in enumerate(entry.get("guessLog", [])):
            if isinstance(step, dict) and "remaining" in step:
                if len(valid_steps) <= i:
                    valid_steps.append([])
                valid_steps[i].append(step["remaining"])

    if valid_steps:
        avg_remaining = [np.mean(step) for step in valid_steps]
        plt.figure(figsize=(8, 4))
        plt.plot(range(1, len(avg_remaining) + 1), avg_remaining, marker='o')
        plt.title("Average Remaining Candidate Words per Guess Round (Log Scale)")
        plt.xlabel("Guess Number")
        plt.ylabel("Average Remaining Words")
        plt.yscale("log")
        plt.grid(True)
        plt.tight_layout()
        plt.savefig(output_path)
        print(f"✅ Saved: {os.path.basename(output_path)}")
    else:
        print(f"⚠️ Skipped: {os.path.basename(output_path)} (no 'remaining' data)")


def plot_sample_pool_reduction(detailed, output_path, sample_size=5):
    plt.figure(figsize=(8, 4))
    sampled = 0
    for entry in detailed:
        log = entry.get("guessLog", [])
        if log and "remaining" in log[0]:
            start = log[0]["remaining"]
            if start == 0:
                continue
            reductions = [step["remaining"] / start for step in log if "remaining" in step]
            plt.plot(range(1, len(reductions) + 1), reductions, label=entry["word"])
            sampled += 1
        if sampled >= sample_size:
            break

    if sampled > 0:
        plt.title("Candidate Pool Reduction (Sample Words, % of Start)")
        plt.xlabel("Guess")
        plt.ylabel("Remaining Candidates (% of first)")
        plt.yscale("log")
        plt.legend()
        plt.grid(True)
        plt.tight_layout()
        plt.savefig(output_path)
        print(f"✅ Saved: {os.path.basename(output_path)}")
    else:
        print(f"⚠️ Skipped: {os.path.basename(output_path)} (no usable data)")


def plot_collapse_steps_histogram(detailed, output_path):
    collapse_steps = []
    for entry in detailed:
        reductions = [step["remaining"] for step in entry.get("guessLog", []) if "remaining" in step]
        if reductions:
            for i, val in enumerate(reductions):
                if val <= 1:
                    collapse_steps.append(i + 1)
                    break

    if collapse_steps:
        plt.figure(figsize=(6, 4))
        plt.hist(collapse_steps, bins=range(1, max(collapse_steps) + 2), align='left', rwidth=0.8)
        plt.title("Guesses Until Only 1 Candidate Remained")
        plt.xlabel("Guess Number")
        plt.ylabel("Word Count")
        plt.tight_layout()
        plt.savefig(output_path)
        print(f"✅ Saved: {os.path.basename(output_path)}")
    else:
        print(f"⚠️ Skipped: {os.path.basename(output_path)} (no valid collapse data)")


def plot_avg_pool_reduction_only(detailed, output_path):
    all_reductions = []
    max_depth = 0

    for entry in detailed:
        log = entry.get("guessLog", [])
        if log and isinstance(log[0], dict) and "remaining" in log[0]:
            start = log[0]["remaining"]
            if start == 0:
                continue
            path = [step["remaining"] / start for step in log if "remaining" in step]
            max_depth = max(max_depth, len(path))
            all_reductions.append(path)

    if not all_reductions:
        print(f"⚠️ Skipped: {os.path.basename(output_path)} (no usable data)")
        return

    for i in range(len(all_reductions)):
        last_val = all_reductions[i][-1]
        all_reductions[i] += [last_val] * (max_depth - len(all_reductions[i]))

    all_reductions = np.array(all_reductions)
    avg = np.mean(all_reductions, axis=0)
    std = np.std(all_reductions, axis=0)

    x = range(1, max_depth + 1)

    plt.figure(figsize=(8, 4))
    plt.plot(x, avg, color='blue', label='Average Pool Reduction', linewidth=2)
    plt.fill_between(x, avg - std, avg + std, color='blue', alpha=0.2, label='±1 Std Dev')
    plt.title("Average Normalized Pool Reduction (Log Scale)")
    plt.xlabel("Guess Number")
    plt.ylabel("Remaining Candidates (% of Start)")
    plt.yscale("log")
    plt.grid(True)
    plt.legend()
    plt.tight_layout()
    plt.savefig(output_path)
    print(f"✅ Saved: {os.path.basename(output_path)}")


# ---------- MAIN EXECUTION ----------

def main():
    data_dir = "data"
    graph_dir = "graphs"
    summary_csv = os.path.join(data_dir, "wordle_summary.csv")
    details_json = os.path.join(data_dir, "wordle_detailed_logs.json")

    os.makedirs(graph_dir, exist_ok=True)

    print(f"📄 Loading {summary_csv}...")
    df = pd.read_csv(summary_csv)

    print(f"📄 Loading {details_json}...")
    with open(details_json) as f:
        detailed = json.load(f)

    plot_attempt_distribution(df, os.path.join(graph_dir, "attempt_distribution.png"))
    plot_top_hardest_words(df, os.path.join(graph_dir, "top20_hardest_words.png"))
    plot_guess_length_histogram(detailed, os.path.join(graph_dir, "guess_length_histogram.png"))
    plot_avg_candidates_per_guess(detailed, os.path.join(graph_dir, "avg_candidates_per_guess.png"))
    plot_sample_pool_reduction(detailed, os.path.join(graph_dir, "sample_pool_reduction.png"))
    plot_collapse_steps_histogram(detailed, os.path.join(graph_dir, "collapse_steps_histogram.png"))
    plot_avg_pool_reduction_only(detailed, os.path.join(graph_dir, "avg_pool_reduction.png"))

    print("\n📊 All graphs saved to 'graphs/'")

if __name__ == "__main__":
    main()
