# Chapter 5 — AI Evaluation (simple version)

This is the same lesson as `05-AIEvaluation.md`, written in plain words.
The original file is still there. Use this one to understand the ideas.
Use the original when you want the exact formulas and code names.

Run the app and choose **9**.

The models this chapter uses are set in `appsettings.json`
(`OpenAI:ChatModel` and `OpenAI:EmbeddingModel`). If you change them,
your scores change too, so only compare runs that used the same models.

---

## The one idea

You already built a system that searches documents and writes an answer.
Reading one nice answer does not tell you if the system is good.

You need a small test you can run again after every change.

Ask four separate questions:

1. Did the search find the right documents?
2. Were those documents actually about the question?
3. Did the answer talk about the question?
4. Is every fact in the answer written in those documents?

A smooth, confident answer can still be wrong.
A correct document can still be useless if search buried it.

---

## Keep the test still

A fair test has parts you do not touch, and one part you do touch.

Leave these alone while you experiment:

- the documents
- the questions
- the list of correct document names (the answer key)

Change only one thing, then run the test again:

- the search method
- the instructions you give the model
- the model itself

This demo has 6 questions. That is enough to learn.
It is not enough to say a real product is ready.
The habit is what matters: same questions, new change, compare the numbers.

If you edit a document and break a fact this lesson depends on, the demo
stops at startup and tells you which question broke. Put the document back,
or update the check in the same edit.

---

## The practice documents

There are 9 short fake company documents.

The first 3 are distractions. They are about onboarding, on-call, and office snacks.
A dumb search returns those 3 and never reads the question.
On this test, that dumb search scores 0. It shows the bottom.
The real search methods should do better than that.

The 6 questions:

| Question | Why it is in the test |
| --- | --- |
| How many paid days off in the first year? | The question never says "vacation". A setup checklist uses many of the same words. The right file is still the vacation policy. |
| What does policy EXP-75 cover? | That code is in only one file. Word search is good at exact codes. |
| How long can someone stay home with pay after a baby is born? | The question says baby, born, pay. The file says child, birth, paid. No shared word. |
| Who answers extension 9911? | That number is in only one file. |
| Rules for vacation and for working from home | Two correct files. A good search must find both. |
| I am locked out of my account | The question never says "password". The help file never says "locked". |

The answer key is a document name, such as `hr-vacation`.
It is not the sentence the model should write.
This part of the test only asks: did search bring back the right file?

---

## 1. Did search find the right files?

In the demo, choose **1**.

The app searches in three ways and shows a table.

| Name | What it does |
| --- | --- |
| Naive | Returns the first 3 files. Ignores the question. |
| Keyword | Looks for the same words. |
| Vector | Looks for similar meaning, even when the words differ. |

It keeps only the top 3 results. That number is called **k**. Here, k = 3.

### Four numbers, in plain words

Imagine the correct files are the answer key.
Imagine search hands you 3 files.

**Hit** — Did at least one correct file appear?

- 1 means yes
- 0 means no

**Recall** — Of all the correct files, how many did we find?

- 1 correct file, and we found it: recall = 1
- 2 correct files, and we found 1: recall = 1/2 = 0.50

**Precision** — Of the 3 slots, how many were correct?

This lesson always divides by 3, even if search returned fewer files.
Empty slots count as wrong.

- 1 correct file and 2 wrong files: precision = 1/3 = 0.33
- 1 correct file and nothing else: precision is still 1/3, because two slots are empty

**Rank score (RR)** — How high was the first correct file?

- Correct file in position 1: score = 1
- Correct file in position 2: score = 1/2 = 0.50
- Correct file in position 3: score = 1/3 = 0.33
- No correct file: score = 0

**MRR** is just the average of that rank score across all 6 questions.

### Which number should you look at?

| Your question | Look at |
| --- | --- |
| Did we find anything correct? | Hit |
| Was the correct file near the top? | Rank score, then the average (MRR) |
| How much of the top 3 is junk? | Precision |
| Did we find every correct file? | Recall |

### What you should see on this test

Word search has no shared word with the baby question or the locked-out question.
So its recall on those two rows is 0.
Meaning search can still match them, because the meaning is close.

`EXP-75` and `9911` each appear in one file.
Word search should put that file first.

The vacation-and-home question has two correct files.
Recall is 1 only when both are inside the top 3.

The table also points at the row where word search and meaning search disagree the most.
Read the note under that row. It tells you why the question was written that way.

Word search and meaning search fail in different ways.
That is why chapter 3 uses both together.

---

## 2. The full path for one question

In the demo, choose **2**.

Pick a numbered question, or type your own.

The app does this:

```
Your question
   ↓
Meaning search returns 3 files
   ↓
Compare those files to the answer key
   ↓
Write an answer using only those files
   ↓
Give three scores
   ↓
Split the answer into small facts and check each one
```

If you type your own question, there is no answer key.
You still get the three scores.
You do not get precision or recall. Those need the answer key.

### The three scores (each from 1 to 5)

**Context relevance** — Are the retrieved files about the question?
The answer text is ignored.
Right answer plus wrong files still fails this score.

**Answer relevance** — Does the answer talk about the question?
Truth is ignored here.
"I don't know" can score low, because it did not answer.
That is still useful. The next score says whether "I don't know" was the honest reply.

**Faithfulness** — Is every fact in the answer present in the files?
If the model uses outside memory, this score goes down.
This is the score that catches a helpful-sounding lie.

The writing instruction says: if the files do not contain the fact, reply
"I don't know based on the provided documents."
The test checks whether the model actually does that.

---

## 3. Hallucination: a fact that is not in the files

A hallucination, in this chapter, is a specific sentence you can point at.

- **Supported** — the files say this
- **Contradicted** — the files say the opposite
- **Unsupported** — the files do not say this

The demo then does simple division:

```
bad claims / all labeled claims
```

Bad claims are the unsupported ones plus the contradicted ones.
Your code does the division. The model only labels each claim.

### Two fixed answers

Choose **3**, then **1**.

Both answers are checked against the vacation policy only.
Search is not involved, so a bad search cannot confuse you.

The good answer only repeats what the policy says: 15 days, and up to 10 days carried over.

The bad answer starts with that true sentence, then adds two inventions:
unused vacation is paid out in cash every December, and contractors get the same 15 days.
Those two things are not in the policy.

So the score should sit between 0 and 1.
Most of the answer can be true, and it can still contain a lie.
That is why "it looks right" is a weak test.

### Two instructions, one missing fact

Choose **3**, then **2**.

The question is: what is the CEO's favorite restaurant?
That fact is not in any file.

- The strict instruction should say it does not know.
- The other instruction tells the model to invent a detail. That instruction is only for the lesson. Do not use it in a real app. It is there so the checker has a lie to catch.

If both answers refuse, the model stayed honest anyway.
The fixed pair in option 1 is the clearer example.

**Groundedness** means the same thing from the other side: every fact can be traced back to the files.
The 1–5 faithfulness score is a summary. The claim list is the evidence under that summary.

---

## 4. Let another model grade the answer

Sometimes you cannot label every answer by hand.
You can ask a second model to grade, using a written rubric.

That grader is helpful. It is also imperfect.
Treat its number as a measurement that can be wrong.

Do not give the grader the answer key.
Search scores already used the answer key.
The grader is for softer questions: is this on topic, is this supported?

The grader must explain first, then give the number.
A bare number is hard to trust.
The demo asks for the same result every time (temperature 0, set by
`OpenAI:Temperature` in `appsettings.json`).
Small changes can still happen. That is normal.

### One answer at a time

Choose **4**, then **1**.

The file is the meal policy: up to $75, a receipt over $25, no alcohol.

- The true answer says that.
- The smooth lie says $150, receipts optional, and alcohol is covered.

The lie still answers the question, so **answer relevance** can stay high.
**Faithfulness** should fall, because the numbers are invented.

If faithfulness stays high, the grader missed the lie.
Write that down. A grader you have never seen fail is a grader you do not know yet.

The search "hit" number does not ask a model "did we find the right file?"
The answer key decides that. Models cannot talk their way out of it.

### Two answers, then swap them

Choose **4**, then **2**.

Both answers are true. They only differ in length.

- Pass 1: the short answer is called A, the longer one is called B.
- Pass 2: the labels are swapped.

| What happened | What it means |
| --- | --- |
| Both passes say tie | The order did not pick a winner. |
| Both passes pick the same text | The choice stayed the same after the swap. |
| The chosen text changes | The label A or B changed the grade. This is called position bias. |

One comparison is not stable until the swap agrees.
In a real system, people also flip the order, use a different model as the grader when they can, and keep some human grades so the grader itself can be checked.

---

## 5. Score every question in one go

Choose **5**.

You get the same search table as option 1.
Then the app asks before it spends more model calls.

If you say yes, it writes an answer for each of the 6 questions and grades it.
That is about 12 model calls.
The claim-by-claim check stays in option 3, so this screen stays a summary.

| Column | Meaning |
| --- | --- |
| Hit | Did meaning-search find a correct file? The grader cannot change this. |
| Ctx | Were the files about the question? |
| Ans | Did the answer talk about the question? |
| Faith | Were the facts in the files? |

A **0** in a grader column means the reply could not be read as a score.
It is a missing score, not a real zero.
It stays in the average so you notice the failure.

When you change the top-3 cutoff, the writing instructions, or the embedding model,
run option 5 again and compare the averages with the last run.
Save the console output. The difference is the result.

Two rows that look similar can be different bugs:

- Hit 0 and Faith 5: search missed, and the model honestly said it did not know.
- Hit 1 and Faith 2: the right file was there, and the answer still made things up.

---

## Words you will see elsewhere

Other articles use different names for these ideas.
Check the definition before you compare two numbers.

| In this chapter | Often called |
| --- | --- |
| Precision, recall, hit, rank | Retrieval scores. They need an answer key. |
| Context relevance | Was this text about the question? |
| Answer relevance | Did the answer address the question? |
| Faithfulness and the claim rate | Was the answer grounded in the files? |

You do not need a special Python library to learn this.
A library helps later, when the test is large.
You still decide what stays frozen, what the answer key is, and what the grader is allowed to see.

---

## Easy mistakes

| Mistake | Do this instead |
| --- | --- |
| One good answer, so the system is fine | Keep a fixed question list. Report the averages. Keep the questions that failed. |
| One number called "quality" | Keep search, relevance, and faithfulness separate. |
| Ask the same call to write the answer and grade it | Grade in a later call. Use the answer key where you have one. |
| Ask the model for the percentage | Let the model label each claim. Let your code divide. |
| Trust every grader number | A missing score is a 0. A smooth lie that scores 5 is a grader mistake. |
| Compare two answers only once | Swap which one is A. Trust the result when the same text still wins. |
| Change a document and keep yesterday's scores | Scores belong to a named set of files. |
| Tune these 6 questions until every cell is perfect | Add a new question you did not tune. |

---

## Try it

All of these start at menu **9**.

1. Choose **1**. Confirm the naive column is 0. Then look at the baby question and the locked-out question. Word-search recall is 0. See if meaning-search finds them. Then look at EXP-75 and 9911. Word search should rank the right file first.

2. On the vacation-and-home row, count how many of the two correct files appear in the top 3. Divide by 2. That is recall. Match it to the table.

3. Choose **3**, then **1**. On the invented answer, count the bad claims and divide by all the claims. Match the printed rate. The "15 days" sentence should be supported. The December cash payout should not.

4. Choose **4**, then **1**. On the smooth lie, write down answer relevance and faithfulness. The useful result is high relevance and low faithfulness.

5. Choose **4**, then **2**. Write down which text won each pass. If the winner changes after the swap, you have seen position bias.

6. Add the word `baby` to the parental-leave document in `Learning/AiEvaluationDemo/Data/LabeledSet.cs`. Run **9** again. The startup check should stop and name `parental-paraphrase`. Put the sentence back when you are done.

7. Choose **5** and let it grade. Read Hit and Faith together, using the two bug types above.

---

## What comes next

This chapter gives you a score.
The next chapter is observability: how many tokens you used, how long it took, what it cost, and which steps ran. That file is `06-Observability.md`, menu option **10**.

The full version of this lesson, with formulas and file names, is `05-AIEvaluation.md`.
