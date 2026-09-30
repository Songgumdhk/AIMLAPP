# Chapter 5 — AI Evaluation

> Plain-language version: [`05-AIEvaluation-Simple.md`](05-AIEvaluation-Simple.md)

> **Goal:** Stop judging a RAG system by reading one good answer, and start
> measuring it. Retrieval gets precision, recall, hit rate, and reciprocal
> rank on a frozen question set. Generation gets a faithfulness check, claim
> by claim. A separate model can grade, and you treat that grade as a
> measurement with error.
>
> The question this chapter answers: *how do you know it actually works well?*

---

## Table of Contents

1. [What you already have](#1-what-you-already-have)
2. [A score you can re-run](#2-a-score-you-can-re-run)
3. [The labeled set](#3-the-labeled-set)
4. [Retrieval evaluation](#4-retrieval-evaluation)
5. [RAG evaluation](#5-rag-evaluation)
6. [Hallucination detection](#6-hallucination-detection)
7. [LLM-as-judge](#7-llm-as-judge)
8. [The harness](#8-the-harness)
9. [Names you will see in papers](#9-names-you-will-see-in-papers)
10. [Common Pitfalls](#10-common-pitfalls)
11. [Practice Exercises](#11-practice-exercises)

> **Settings for this chapter** live in `appsettings.json`:
> `OpenAI:ChatModel` answers and judges, `OpenAI:EmbeddingModel` powers the
> vector retriever, and `OpenAI:Temperature` (0) keeps judge reruns steady.
> Scores are only comparable between runs that used the same models, so note
> these values next to any result you keep. The cutoff `k = 3`
> (`RetrievalMetrics.K`) stays in code on purpose: the frozen labeled set and
> its fixture checks are written for it.

---

## 1. What you already have

Chapter 3 retrieves chunks and writes an answer. Chapter 4's reviewer
scores a draft. Both are single runs you watch in the console.

That is how you debug. It is not how you know a change helped.

```
Question
   ↓
Retrieved documents
   ↓
Were the correct documents retrieved?     ← needs gold document ids
   ↓
Did the answer address the question?      ← answer relevance
   ↓
Was every claim supported by the context? ← faithfulness / hallucination
```

Those are different questions. A fluent wrong answer can pass the middle
one and fail the last one. A correct document buried at rank 8 fails
retrieval even when the model could have answered if you had shown it
that document.

Menu option **9** splits them into four demos, plus a full pass over the
labeled set.

---

## 2. A score you can re-run

An evaluation is three frozen things plus one thing you are allowed to change.

| Frozen                         | You change it when                          |
| ------------------------------ | ------------------------------------------- |
| The documents                  | You edit `LabeledSet.Documents` on purpose  |
| The questions                  | You edit `LabeledSet.Cases` on purpose      |
| The gold document ids          | Same file. They are the answer key          |
| Retriever, prompt, or model    | This is the experiment                      |

`FixtureCheck.Verify()` runs before the menu, with no API call. It locks
the keyword facts in this chapter (no shared token on two paraphrase
rows, exact-id rows ranked first, naive baseline overlapping no gold
id). If you edit a document and one of those facts stops being true, the
demo throws and names the row. That is the set defending itself.

Six questions will not tell you a system is ready for production. They
will tell you whether today's change helped **this** set. That is the
shape you keep when the set grows to hundreds of questions from real
failures.

---

## 3. The labeled set

Nine short Acme documents. The first three are distractors
(`hr-onboarding`, `eng-oncall`, `office-snacks`). The naive retriever
returns those three and ignores the question, so its hit rate on this
set is 0. It is the floor.

The six questions are chosen so each metric has something to do:

| Id                     | What it is for                                                                 |
| ---------------------- | ------------------------------------------------------------------------------ |
| `vacation-paraphrase`  | Onboarding shares *first, year, joining, paid, off*. Gold is the vacation doc, which the question never names. |
| `expense-code`         | `EXP-75` occurs in one document. Keyword search is built for that.            |
| `parental-paraphrase`  | Question says *baby, born, pay*. Document says *child, birth, paid*. No shared keyword. |
| `hotline-number`       | `9911` occurs in one document.                                                 |
| `time-off-and-remote`  | Two gold documents. Recall cares that **both** are in the top 3.              |
| `locked-out`           | Question never says *password*. The procedure never says *locked* or *account*. |

Gold labels are document ids, not "the answer text." Retrieval evaluation
asks *which documents should have been retrieved*, not *which sentence the
model should have written*.

---

## 4. Retrieval evaluation

Menu option **1** embeds the six questions in one request and scores three
retrievers at `k = 3` (`RetrievalMetrics.K`):

| Retriever | What it does                                              |
| --------- | --------------------------------------------------------- |
| Naive     | First 3 documents in `LabeledSet`. The question is unused. |
| Keyword   | BM25 over tokens, same idea as chapter 3.                 |
| Vector    | Cosine similarity with `OpenAI:EmbeddingModel` (`text-embedding-3-small`). |

One question, gold set `R`, retrieved list `D` (length at most 3):

```
Precision@3 = |D ∩ R| / 3
Recall@3    = |D ∩ R| / |R|
Hit@3       = 1 if D ∩ R is not empty, else 0
RR          = 1 / rank of the first gold id, else 0
MRR         = mean of RR across the six questions
```

Empty slots count as misses. One correct document and nothing else:

```
Retrieved: [hr-vacation]
Gold:      [hr-vacation]
Precision@3 = 1/3 = 0.33
Recall@3    = 1
Hit@3       = 1
RR          = 1
```

A list that finds one of two gold documents:

```
Retrieved: [hr-vacation, hr-onboarding, fin-expense]
Gold:      [hr-vacation, hr-remote]
Precision@3 = 1/3 ≈ 0.33
Recall@3    = 1/2 = 0.50
Hit@3       = 1
RR          = 1        (hr-vacation is rank 1)
```

`RetrievalMetrics.VerifyWorkedExample()` asserts those two cases, plus a
total miss (`Hit = 0`, `RR = 0`). The formulas live in one method so the
table cannot drift away from this page.

Which number moves when you care about what:

| You care about                              | Watch        |
| ------------------------------------------- | ------------ |
| Did any gold document show up?              | Hit@3        |
| Was the first gold document near the top?   | RR, then MRR |
| How much of the top 3 is junk?              | Precision@3  |
| Did we find every gold document?            | Recall@3     |

On this corpus, before you look at the vector column, keyword search is
already determined by the tokens:

- `parental-paraphrase` and `locked-out` share no keyword with their gold
  document, so keyword recall on those rows is 0.
- `hotline-number` and `expense-code` contain a token that appears once, so
  keyword rank 1 is that document.
- `time-off-and-remote` names *vacation* and *home*, which is enough for
  both gold documents to land in the keyword top 3.

The vector column is the experiment. Read it off the console. Where it
beats keyword, the embedding matched a paraphrase the tokens could not.
Where it loses, an exact token was the better tool. Chapter 3's hybrid
search exists because those two failures are different.

The printer then shows the row with the largest keyword-vs-vector gap and
the case note that says why the question was written.

---

## 5. RAG evaluation

Menu option **2** walks the full flow (retrieve, answer, check support) for one question. Pick a labeled
question by number, or type your own.

```
Question
   ↓
Vector top 3
   ↓
Gold comparison          (skipped if you typed an unlabeled question)
   ↓
Strict answer            (GroundedAnswerer.Strict)
   ↓
Rubric                   (context relevance, answer relevance, faithfulness)
   ↓
Claims                   (the faithfulness score, opened up)
```

Three model calls. Each has one job.

**Context relevance** — is the retrieved text about the question? The
answer is ignored. A perfect answer sitting on the wrong documents still
fails this.

**Answer relevance** — does the answer address the question? Truth and the
documents are ignored. "I don't know" can score low here even when it is
the faithful thing to say. That is useful. It tells you the reply ducked
the question. Faithfulness tells you whether the duck was justified.

**Faithfulness** — is every factual claim in the answer supported by the
context? Outside knowledge lowers the score. This is the one that catches
a model being helpful from memory.

The strict instruction tells the model to say *I don't know based on the
provided documents* when the context is empty of the fact. That sentence
is a product decision. Evaluation is how you find out whether the model
actually follows it.

An unlabeled question still gets judge scores. It does not get precision
or recall. Those need gold ids. Do not let a judge invent them.

---

## 6. Hallucination detection

A hallucination here means a factual claim in the answer that the context
does not support, or that the context contradicts. It is not "the model
felt wrong." You can point at the sentence.

`HallucinationChecker` asks the model to split the answer into atomic
claims and label each one:

| Verdict        | Meaning                                      |
| -------------- | -------------------------------------------- |
| `supported`    | The context states it.                       |
| `contradicted` | The context states the opposite.             |
| `unsupported`  | The context does not state it.               |

The rate is computed in `Summarize`. The model is not asked for a
percentage. If you let the model report the rate, you have graded the
grader's arithmetic.

```
hallucination rate = (unsupported + contradicted) / labeled claims
```

Menu option **3**, then **1**, runs two fixed answers against the vacation
policy. No retrieval, so a bad search cannot confuse the lesson.

The faithful answer only restates the 15 days and the 10-day carryover.

The invented answer keeps that true first sentence, then adds a December
cash payout and a contractor benefit. Neither is in the policy. Expect
the rate to land between 0 and 1. A mostly-true answer can still be partly
hallucinated, which is why "the answer looks right" is a weak test.

Option **3**, then **2**, asks for the CEO's favorite restaurant. That
fact is not in the corpus.

- `GroundedAnswerer.Strict` should refuse.
- `GroundedAnswerer.Ungrounded` is a negative control. It tells the model
  to invent a concrete policy. Do not ship that prompt. It exists so the
  checker has a lie to catch, the way a unit test needs a failing input.

If both answers refuse, the model stayed grounded anyway. The planted pair
in option 1 is the comparison that does not depend on that.

Groundedness is the same idea from the other direction: every claim traces
to the context. Faithfulness in the rubric is a 1–5 summary of it. The
claim list is the evidence under that summary.

---

## 7. LLM-as-judge

A judge is another model call with a rubric and a JSON schema. It did not
see the gold document ids unless you put them in the prompt. You should
not. Retrieval metrics already used the gold. The judge is for the parts
you cannot label by hand every time: is this paragraph supported, is it
on topic, is this context about the question.

The rubric text is `RagJudge.SystemPrompt`. Reasons are requested before
scores because a bare number is easy to emit and hard to audit. Temperature
is 0 (`OpenAI:Temperature`) so a rerun moves less. It will still move. A
judge is not a pure function.

### Pointwise

Option **4**, then **1**, grades two answers to the meal-expense question.
The context is policy EXP-75 ($75 cap, receipt over $25, no alcohol).

The true answer restates that. The fluent lie says $150, optional
receipts, and alcohol covered. It is specific and confident. Answer
relevance can stay high. Faithfulness should drop. That split is why the
rubric has both scores.

If faithfulness does not drop, write that down. The judge missed an
invented policy. Judge error is a normal result, which is why option 1's
hit rate does not ask a model whether the gold document was retrieved.

### Pairwise and position bias

Option **4**, then **2**, compares two answers that are both supported by
EXP-75. Pass 1 labels the short one as A. Pass 2 swaps them.

| Outcome                                      | What it means                                      |
| -------------------------------------------- | -------------------------------------------------- |
| Both passes tie                              | Order did not create a winner.                     |
| Both passes pick the same text               | The preference survived the swap.                  |
| The preferred text changes                   | Position bias. The label A or B moved the grade.  |

A single pairwise call is not a stable measurement until the swap agrees.
Production evals also rotate order, use a judge model different from the
generator when they can, and keep a slice of human labels so the judge
itself has a score.

---

## 8. The harness

Option **5** prints the same retrieval table as option 1, then asks before
spending generation calls. Grading is about two calls per question: a
strict answer from the **vector** top 3, then the rubric. Six questions,
twelve calls. The claim checker stays in option 3 so this pass stays a
summary.

The grade table has four columns:

| Column | Source                                      |
| ------ | ------------------------------------------- |
| Hit    | Gold document ids. The judge cannot edit it. |
| Ctx    | Judge: context relevance                    |
| Ans    | Judge: answer relevance                     |
| Faith  | Judge: faithfulness                         |

A `0` in a judge column means the JSON had no usable score. It is a
missing measurement, not a real zero. The mean still includes it so a
parse failure is visible instead of quietly dropped.

When you change `K`, the strict prompt, or the embedding model, you rerun
option 5 and read the means against the previous run. Save the console
output. The set is the test. The diff is the result.

---

## 9. Names you will see in papers

RAGAS and similar writeups use overlapping names. Compare definitions
before you compare numbers. In this chapter the definitions are the ones
above, in `RetrievalMetrics` and the judge prompts.

| This chapter                         | Nearby industry name                                      |
| ------------------------------------ | --------------------------------------------------------- |
| Precision@K, Recall@K, MRR, Hit@K    | Retrieval metrics. They need gold document ids.           |
| Context relevance (1–5)              | A reference-free stand-in for "was this context on topic?" |
| Answer relevance (1–5)               | Answer relevancy                                          |
| Faithfulness (1–5) and the claim rate | Faithfulness / groundedness. RAGAS faithfulness is the share of claims inferred from the context, which is what `Summarize` computes. |

You do not need the Python RAGAS library to learn the measurements. The
library matters later, when the set is large and you want a maintained
harness. The decisions — what is frozen, what is gold, what the judge is
allowed to see — stay yours.

---

## 10. Common Pitfalls

| Pitfall                                              | What to do instead                                                                 |
| ---------------------------------------------------- | ---------------------------------------------------------------------------------- |
| "I tried one question and the answer looked good."  | Freeze a set. Report means. Keep the question that failed.                        |
| One score called "quality"                           | Split retrieval, relevance, and faithfulness. They move for different reasons.    |
| Asking the generator to grade itself in the same call | A later call, with a rubric, and gold labels where you have them.                |
| Letting the judge compute the percentage             | The model labels claims. Your code divides.                                       |
| Treating judge JSON as truth                         | Score 0 is a parse miss. A fluent lie that scores 5 on faithfulness is judge error. |
| One pairwise order                                   | Swap A and B. Keep the result only if the preferred text stays put.               |
| Precision@K with a denominator of "however many came back" | This chapter divides by K. A one-item list caps precision at 1/3.            |
| Editing a document and keeping old scores            | `FixtureCheck` is the small version of this. Scores belong to a named corpus.     |
| Optimizing the six questions until every cell is 1  | You will overfit the demo. Add a question you did not tune on.                    |

---

## 11. Practice Exercises

All of these run from menu option **9**.

### Exercise 1 — Read the floor, then the gap

Option **1**.

Naive hit should be 0 on every row. The first three documents are
distractors.

Find `parental-paraphrase` and `locked-out`. Keyword recall is 0 because
there is no shared token. See whether vector hit is 1. If it is, that row
is the case for dense retrieval. If it is not, the embedding missed a
paraphrase too, and the table is telling you that.

Then read `expense-code` and `hotline-number`. Keyword reciprocal rank
should be 1.00. That is the case for keeping a keyword channel.

### Exercise 2 — Recall with two gold documents

Option **1**, row `time-off-and-remote`.

Gold is `hr-vacation` and `hr-remote`. From the detail lines (or by
rerunning option **2** and choosing 5), list the three retrieved ids and
compute recall yourself: how many of the two gold ids appear, divided by 2.
Match it to the table.

### Exercise 3 — A true sentence glued to a false one

Option **3**, then **1**.

Count unsupported or contradicted claims on the invented answer. Divide by
the number of labeled claims. Match the printed rate. The true sentence
about 15 days should be `supported`. The December payout should not.

### Exercise 4 — Separate the scores

Option **4**, then **1**.

On the fluent lie, write down answer relevance and faithfulness side by
side. The interesting result is a high relevance score with a low
faithfulness score. If you do not get that split, the judge failed this
item. Keep the item. A judge you have not caught failing is a judge you
do not understand yet.

### Exercise 5 — Swap the order

Option **4**, then **2**.

Record the winner letter and which text it maps to (`Brief` or
`Detailed`) on each pass. If the text changes, you have seen position
bias on two answers that are both faithful.

### Exercise 6 — Make the keyword overlap appear

Add the word `baby` to the parental-leave document in `LabeledSet.cs`.
Run option **9** again.

`FixtureCheck` should throw and name `parental-paraphrase`. The startup
check is the eval set noticing that a "no shared keyword" question now
shares one. Put the document back when you are done. If you meant to
change the set, change the check in the same edit so the chapter and the
corpus still agree.

### Exercise 7 — Grade the system you would actually ship

Option **5**. Let it grade.

The Hit column is the vector retriever. The Faith column is the strict
prompt on whatever that retriever returned. A row with Hit 0 and Faith 5
usually means the model said it did not know, and the judge agreed that
refusal was supported. A row with Hit 1 and Faith 2 means the right
document was on the page and the answer still wandered. Those are
different bugs.

---

## What's Next?

Once a change has a number on this set, the next chapter
is **Observability**: token usage, latency, and cost for the calls this
chapter just spent, and a trace of which retriever and which judge ran.
Evaluation tells you the score. Observability tells you what you paid for
it, and where the run went. That chapter is `06-Observability.md`, menu option **10**.
